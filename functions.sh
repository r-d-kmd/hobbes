declare -a APPS=(db)
VOLUMES=(db)
function get_script_dir () {
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
function services(){
    local APP_NAME=""
    for APP in $(find ${SCRIPT_DIR}/services -name *.fsproj | rev | cut -d'/' -f1 | rev)
    do
        if [[ "$APP" = hobbes.* ]] 
        then
            APP_NAME=$(echo $APP | cut -d'.' -f 2)
            APPS+=($APP_NAME)
        fi
        
    done 
    APP_NAME=""
    for APP in $(find ${SCRIPT_DIR}/workers -name *.fsproj | rev | cut -d'/' -f1 | rev)
    do
        if [[ "$APP" = *.worker.* ]] 
        then
           APP_NAME=$(echo $APP | cut -d'.' -f 1)
        else
           APP_NAME=$(echo $APP | rev | cut -d'.' -f2- | rev | tr . -)
        fi
        APPS+=($APP_NAME)
    done 
}

services

echo "Project home folder is: $SCRIPT_DIR"
echo "Apps found:"
printf '%s\n' "${APPS[@]}"

KUBERNETES_DIR="$SCRIPT_DIR/kubernetes"


function getName(){
   local POD_NAME=$(kubectl get all | grep -e pod/$1 -e pod/collectors-$1 | cut -d ' ' -f 1 | cut -d '/' -f 2)
   echo $POD_NAME
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
    local POD_NAME=$(getName $1)
    kubectl logs $2 $POD_NAME
}

function delete(){
    local POD_NAME=$(getName $1)
    echo $POD_NAME
    kubectl delete "pod/$POD_NAME"
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
}

function setenv(){
    eval $(minikube -p minikube docker-env)
}

function build(){    
    setenv
    ECHO "Starting Build"
    local CURRENT_DIR=$(pwd)
    cd $SCRIPT_DIR
    if [ -z "$1" ]
    then
        fake build
    else
        if ["$1" = "target"]
        then
            fake build --target $1
        else
            for var in "$@"
            do
                fake build --target "hobbes.$var"
            done
            restart $1
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
    local FILES=""

    cd $KUBERNETES_DIR
    kubectl apply -f env.JSON;
    
    for i in "${APPS[@]}"; do 
        if test -f "$i-svc.yaml"
        then
            FILES="$i-deployment.yaml,$i-svc.yaml"
        else
            if test -f "$i-deployment.yaml"
            then
                FILES="$i-deployment.yaml"
            else
                FILES="$i-job.yaml"
            fi
        fi
        kubectl apply -f $(echo $FILES)
    done
    for i in "${VOLUMES[@]}"; do kubectl apply -f $i-volume.yaml; done
    kubectl apply -f rabbitmq-svc.yaml
    cd $CURRENT_DIR
}

function buildAndStart() {
    build
    start
}

function startkube(){
    set $PATH=$PATH:/Applications/VirtualBox.app/
    minikube start --vm-driver virtualbox --disk-size=75GB
}

function update(){
    local CURRENT_DIR=$(pwd)
    cd $KUBERNETES_DIR
    for i in "${APPS[@]}"; do kubectl apply -f $i-deployment.yaml,$i-svc.yaml; done
    for i in "${VOLUMES[@]}"; do kubectl apply -f $i-volume.yaml; done
    kubectl apply -f env.JSON
    cd $CURRENT_DIR
}

function isRunning(){
    echo $(kubectl get pods -l app=$1 -o 'jsonpath={..status.conditions[?(@.type=="Ready")].status}')
}

function pingService(){
    curl -I -L -X GET "http://$(minikube ip):$1/ping"
}

function testServiceIsFunctioning(){
    pingService $1 2>/dev/null | grep HTTP | tail -1 | cut -d$' ' -f2
}

function awaitRunningState(){
    for NAME in ${APPS[@]}
    do 
        while [[ $(isRunning $NAME)  != "True" ]]
        do 
            echo "waiting for $NAME" && sleep 1
        done
        echo "$NAME is ready"
    done
    all
}

function run(){
    setenv
    kubectl run -i --tty temp-$1 --image kmdrd/$1 
}