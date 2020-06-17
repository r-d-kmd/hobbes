eval $(minikube -p minikube docker-env)
VOLUMES=(db)
function get_script_dir(){
     SOURCE="${BASH_SOURCE[0]}"
     # While $SOURCE is a symlink, resolve it
     while [ -h "$SOURCE" ]; do
          DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
          SOURCE="$( readlink "$SOURCE" )"
          # If $SOURCE was a relative symlink (so no "/" as prefix, need to resolve it relative to the symlink base directory
          [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE"
     done
     DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
     echo "$DIR"
}

SCRIPT_DIR=$(get_script_dir)
KUBERNETES_DIR="$SCRIPT_DIR/kubernetes"

if [ $(uname -s) = "Darwin" ]
then
    declare -a APPS=(db)
    function services(){
         local APP_NAME=""
         for APP in $(find ${SCRIPT_DIR}/services -name *.fsproj | rev | cut -d'/' -f1 | rev)
         do
             APP_NAME=$(echo $APP | cut -d'.' -f 1 | tr '[:upper:]' '[:lower:]')
             APPS+=($APP_NAME)
         done 
         APP_NAME=""
         for APP in $(find ${SCRIPT_DIR}/workers -name *.fsproj | rev | cut -d'/' -f1 | rev)
         do
             if [[ "$APP" = *.worker.* ]] 
             then
                APP_NAME=$(echo $APP | cut -d'.' -f 1 | tr '[:upper:]' '[:lower:]')
             fi
             APPS+=($APP_NAME)
         done 
    }
    services
else
    declare -a APPS=("db" "azuredevops" "calculator" "configurations" "gateway" "git" "sync" "uniformdata")
fi

function getPodName(){
    local POD_NAME=$(kubectl get all | grep -e pod/$1 -e pod/collectors-$1 | cut -d ' ' -f 1 )
    if [[ "$POD_NAME" = pod/* ]]
    then
       echo $POD_NAME
    fi
}

function getJobWorker(){
    local JOB_NAME=$(kubectl get all | grep job.batch/syncronization-scheduler-.*$1 | cut -d ' ' -f 1)
    if [[ "$JOB_NAME" = job.batch/* ]]
    then
        echo $JOB_NAME
    fi
}

function getName(){
    local NAME=$(kubectl get all | grep -e pod/$1 | cut -d ' ' -f 1 )
    if [ -z "$NAME" ]
    then
       NAME=$(getJobWorker $1)
    fi
    echo $NAME
}

function getAppName(){
   local SERVICE_NAME=$(kubectl get all \
                        | grep service/$1 \
                        | cut -d ' ' -f 1 \
                        | cut -d '/' -f 2)
   local APP_NAME=${SERVICE_NAME::${#SERVICE_NAME}-4}
   echo $APP_NAME
}

function logs(){
    local NAME=$(getName $1)
    kubectl logs $2 $NAME
}

function delete(){
    local POD_NAME=$(getName $1)
    kubectl delete "$POD_NAME"
}

function restart(){
    local CURRENT_DIR=$(pwd)
    cd $KUBERNETES_DIR
    for var in "$@"
    do
        local FILE_NAME=$(ls *$var*-deployment.yaml)
        kubectl scale --replicas=0 -f $FILE_NAME
        kubectl scale --replicas=1 -f $FILE_NAME
    done
    cd $CURRENT_DIR
}

function all(){
    kubectl get all
}

function clean(){
    kubectl delete --all deployment
    kubectl delete --all service
    kubectl delete --all pods
    kubectl delete --all pvc
    kubectl delete --all secrets
    kubectl delete --all statefulset
    kubectl delete --all job
    kubectl delete --all replicationcontroller
    kubectl delete --all hpa
}

function build(){    
    local CURRENT_DIR=$(pwd)
    cd $SCRIPT_DIR
    if [ -z "$1" ]
    then 
        fake build
    else 
        if [ -z "$2" ]
        then
            fake build --target "$1"
        else
            fake build --target "$1" --parallel $2
        fi
    fi

    cd $CURRENT_DIR
    echo "Done building"
}

function describe(){
    local NAME=$(getAppName $1)
    local NAME=$(kubectl get pods -l app=$NAME -o name)
    kubectl describe ${NAME}
}

function listServices(){
    minikube service list 
}

function start() {
    local CURRENT_DIR=$(pwd)
    cd $KUBERNETES_DIR
    local FILE=""

    kubectl apply -f env.JSON;
    
    kubectl apply -k ./
    
    cd $CURRENT_DIR
}

function startkube(){
    set $PATH=$PATH:/Applications/VirtualBox.app/
    minikube start --vm-driver virtualbox --disk-size=75GB
}

function update(){
    local CURRENT_DIR=$(pwd)
    cd $KUBERNETES_DIR
    start
    kubectl apply -f env.JSON
    cd $CURRENT_DIR
}

function isRunning(){
    local APP_NAME=$(echo "$1")
    if [ "$(echo "$APP_NAME" | cut -d '-' -f1)" = "sync" ]
    then 
        echo "True"
    else
        echo $(kubectl get pod/$1 -o 'jsonpath={..status.conditions[?(@.type=="Ready")].status}')
    fi
}

function pingService(){
    curl -I -L -X GET "http://$(minikube ip):$1/ping"
}

function testServiceIsFunctioning(){
    pingService $1 2>/dev/null | grep HTTP | tail -1 | cut -d$' ' -f2
}
declare -a PODS=()
function pods(){
    PODS=()
    PODS_=$(kubectl get pods | grep - | cut -d ' ' -f 1)
    for NAME in ${PODS_[@]}
    do 
        if [[ $(isRunning $NAME) != "True" ]]
        then
            PODS+="$NAME"
        fi
    done
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
    pods    
    while (( ${#PODS[@]} ))
    do
        PODS_=$(kubectl get pods | grep - | cut -d ' ' -f 1 )
        echo "Still waiting for: ${#PODS[@]}"
        for NAME in ${PODS_[@]}
        do 
            if [[ $(isRunning $NAME) != "True" ]]
            then
                echo "$(echo "$NAME" | cut -d '-' -f1)"
            fi
        done
        sleep 1
        pods
    done
    all
}

function run(){
    kubectl run -i --tty temp-$1 --image kmdrd/$1 
}

function restartApp(){
    delete "$1" && logs "$1" -f
}

function sync(){
    local CURRENT_DIR=$(pwd)
    cd $KUBERNETES_DIR
    echo $(kubectl delete job.batch/sync)
    kubectl apply -f sync-job.yaml
    cd $CURRENT_DIR
}

function test(){
    local CURRENT_DIR=$(pwd)
    cd $SCRIPT_DIR
    dotnet test
    source functions.sh
    ip=$(minikube ip)
    start
    awaitRunningState
    front_url="http://${ip}:30080"
    #test the gateway is functioning
    curl ${front_url}/ping
    #test the db is responding
    curl "http://${ip}:30084"
    #wait for queue to be ready
    sleep 10
    #publish transformations and configurations
    docker build -t workbench tools/workbench && docker run -dt workbench development --host "${front_url}" --masterkey Rno8hcqr9rXXs
    all
    sync
    sleep 60
    NAME=$(kubectl get pods -l app=gateway -o name) 
    newman run https://api.getpostman.com/collections/7af4d823-d527-4bc8-88b0-d732c6243959?apikey=${PM_APIKEY} -e https://api.getpostman.com/environments/b0dfd968-9fc7-406b-b5a4-11bae0ed4b47?apikey=${PM_APIKEY} --env-var "ip"=${ip} --env-var "master_key"=${MASTER_KEY}
    cd $CURRENT_DIR
}

echo "Project home folder is: $SCRIPT_DIR"
echo "Apps found:"
printf '%s\n' "${APPS[@]}"