#! /bin/bash
SCRIPT_DIR=$(get_script_dir)
KUBERNETES_DIR="$SCRIPT_DIR/kubernetes"

function all(){
    kubectl get all
}

function getName(){
    local NAME=$(kubectl get pods | grep $1 | cut -d ' ' -f 1 )
    if [ -z "$NAME" ]
    then
       NAME=$(getJobWorker $1)
    fi
    echo $NAME
}

function logs(){
    local NAME=$(getName $1)
    kubectl wait --for=condition=ready "$NAME" --timeout=60s
    kubectl logs $2 $NAME
}

function start() {
    local CURRENT_DIR=$(pwd)
    cd $KUBERNETES_DIR
    
    kubectl apply -k ./
    
    cd $CURRENT_DIR
}

function isRunning(){
    local APP_NAME=$(echo "$1")
    if [ "$(echo "$APP_NAME" | cut -d '-' -f1)" = "sync" ]
    then 
        echo "True"
    else
        if [ "$(echo "$APP_NAME" | cut -d '-' -f1)" = "sync" ][ "$(echo "$APP_NAME" | cut -d '-' -f1)" = "publish" ]
        then
            echo "True"
        else
            echo $(kubectl get pod/$1 -o 'jsonpath={..status.conditions[?(@.type=="Ready")].status}')
        fi
    fi
}

function awaitRunningState(){
    PODS=$(kubectl get pods | grep - )
    while [ ${#PODS[@]} -eq 0 ]
    do
        sleep 1
        for NAME in ${PODS_[@]}
        do
            echo "$NAME"
        done
        PODS=$(kubectl get pods | grep - )
    done
    
    PODS_=$(kubectl get pods | grep - | cut -d ' ' -f 1 )
    echo "Still waiting for: ${#PODS[@]}"
    for NAME in ${PODS_[@]}
    do 
        if [ "$(echo "$NAME" | cut -d '-' -f1)" != "sync" ] && [ "$(echo "$NAME" | cut -d '-' -f1)" != "publish" ]
        then
            echo "Waiting for pod/$NAME"
            kubectl wait --for=condition=ready "pod/$NAME" --timeout=60s
        fi
    done

    echo "Waiting for DB to be operational"
    while [ "$(logs gateway | grep "DB initialized")" != "DB initialized" ]
    do
        logs gateway | tail -1
        logs db | tail -1
    done

    echo "Waiting for Rabbit-MQ to be operational"
    while [ "$(logs conf | grep "Watching queue")" != "Watching queue: cache" ]
    do
        logs conf | tail -1
        logs rabbit | tail -1
    done

    all
}

function startJob(){
    local CURRENT_DIR=$(pwd)
    cd $KUBERNETES_DIR
    kubectl delete job.batch/$1 &> /dev/null
    kubectl apply -f $1-job.yaml &> /dev/null || exit 1
    eval $(echo "kubectl wait --for=condition=ready pod/$(getName $1) --timeout=120s &> /dev/null")
    logs $1 -f
    cd $CURRENT_DIR
}

function sync(){
    startJob sync
}

function publish(){
    startJob publish
}

#This function builds the production yaml configuration in the kubernetes folder.
function applyProductionYaml() {
    local CURRENT_DIR=$(pwd)
    cd $KUBERNETES_DIR
    mv kustomization.yaml ./local_patches/kustomization.yaml
    mv ./prod_patches/kustomization.yaml kustomization.yaml
    kustomize build -o test.yaml
    mv kustomization.yaml ./prod_patches/kustomization.yaml
    mv ./local_patches/kustomization.yaml kustomization.yaml
    cd $CURRENT_DIR
}

function startJob(){
    local CURRENT_DIR=$(pwd)
    cd $KUBERNETES_DIR
    kubectl delete job.batch/$1 &> /dev/null
    kubectl apply -f $1-job.yaml &> /dev/null || exit 1
    eval $(echo "kubectl wait --for=condition=ready pod/$(getName $1) --timeout=120s &> /dev/null")
    logs $1 -f
    cd $CURRENT_DIR
}

function publish(){
    startJob publish
}


function sync(){
    startJob sync
}


function setupTest(){
    local CURRENT_DIR=$(pwd)
    cd $SCRIPT_DIR
    #dotnet test
    start 
    awaitRunningState
    all
    #Forward ports to be able to communicate with the cluster
    kubectl port-forward service/gateway-svc 30080:80 &
    kubectl port-forward service/db-svc 30084:5984 &
    #wait a few second to be sure the port forwarding is in effect
    sleep 3
    IP="127.0.0.1"
    SERVER="http://${ip}"
    #test that the server and DB is accessible
    curl "${SERVER}:30084"
    front_url="${SERVER}:30080"
    curl ${front_url}/ping
    #publish transformations and configurations
    publish
    
    logs gateway | tail -1
    logs conf | tail -1
    #syncronize and wait for it to complete
    sync
    sleep 300

    cd $CURRENT_DIR
}

function test(){
    NAME=$(kubectl get pods -l app=gateway -o name) 
    newman run https://api.getpostman.com/collections/7af4d823-d527-4bc8-88b0-d732c6243959?apikey=${PM_APIKEY} -e https://api.getpostman.com/environments/b0dfd968-9fc7-406b-b5a4-11bae0ed4b47?apikey=${PM_APIKEY} --env-var "ip"=${IP} --env-var "master_key"=${MASTER_KEY} #|| exit 1
    echo "*********************GATEWAY********************************"
    echo "*********************GATEWAY********************************"
    echo "*********************GATEWAY********************************"
    logs gateway | tail -50
    echo "*********************UNIFORM********************************"
    echo "*********************UNIFORM********************************"
    echo "*********************UNIFORM********************************"
    logs uniform | tail -50
}

setupTest
test