#! /bin/bash
Black='\033[0;30m'
DarkGray='\033[1;30m'
Red='\033[0;31m'
LightRed='\033[1;31m'
Green='\033[0;32m'
LightGreen='\033[1;32m'
Orange='\033[0;33m'
Yellow='\033[1;33m'
Blue='\033[0;34m'
LightBlue='\033[1;34m'
Purple='\033[0;35m'
LightPurple='\033[1;35m'
Cyan='\033[0;36m'
LightCyan='\033[1;36m'
LightGray='\033[0;37m'
White='\033[1;37m'
NoColor='\033[0m'

eval $(SHELL=/bin/bash minikube -p minikube docker-env)
#source <(kubectl completion bash)

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

declare -a APPS=(db)
function services(){
    local APP_NAME=""
    for PROJECT_FILE in $(find ${SCRIPT_DIR}/services -name *.fsproj)
    do
        local FILE_NAME=`basename $PROJECT_FILE`
        APP_NAME=$(echo $FILE_NAME | cut -d'.' -f 1 | tr '[:upper:]' '[:lower:]')
        APPS+=($APP_NAME)
    done 
    APP_NAME=""
    for PROJECT_FILE in $(find ${SCRIPT_DIR}/workers -name *.fsproj)
    do
        local FILE_NAME=`basename $PROJECT_FILE`
        if [[ "$FILE_NAME" = *.worker.* ]] 
        then
            APP_NAME=$(echo $FILE_NAME | cut -d'.' -f 1 | tr '[:upper:]' '[:lower:]')
        fi
        APPS+=($APP_NAME)
    done 
}
services

if [[ $(uname -s) == MINGW64_NT* ]]
then
    printf "${Red}Running on windows${NoColor}\n"
else
    printf "${Green}Mac${NoColor}\n"
    source macos.sh
fi

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

function getJobWorker(){
    local JOB_NAME=$(kubectl get all | grep job.batch/syncronization-scheduler-.*$1 | cut -d ' ' -f 1)
    if [[ "$JOB_NAME" = job.batch/* ]]
    then
        echo $JOB_NAME
    fi
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

function getAppName(){
   local SERVICE_NAME=$(kubectl get services \
                        | grep $1 \
                        | cut -d ' ' -f 1)
   local APP_NAME=${SERVICE_NAME::${#SERVICE_NAME}-4}
   echo $APP_NAME
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
    re='^[0-9]+$'
    if [ -z "$1" ]
    then 
        dotnet fake build
    elif [[ $1 =~ $re ]]
    then
        build "build" $1 
    elif [ -z "$2" ]
    then
        dotnet fake build --target "$1"
    else
        dotnet fake build --target "$1" --parallel $2
    fi
    cd $CURRENT_DIR
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

    kubectl apply -f env.JSON
    kubectl apply -k ./
    sleep 5
    cd $CURRENT_DIR
}

function startkube(){
    set $PATH=$PATH:/Applications/VirtualBox.app/
    minikube start --vm-driver docker
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
        if [ "$(echo "$APP_NAME" | cut -d '-' -f1)" = "sync" ][ "$(echo "$APP_NAME" | cut -d '-' -f1)" = "publish" ]
        then
            echo "True"
        else
            echo $(kubectl get pod/$1 -o 'jsonpath={..status.conditions[?(@.type=="Ready")].status}')
        fi
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

function run(){
    kubectl run -i --tty temp-$1 --image kmdrd/$1 
}

function startJob(){
    local CURRENT_DIR=$(pwd)
    cd $KUBERNETES_DIR
    kubectl delete job.batch/$1 &> /dev/null
    kubectl apply -f $1-job.yaml &> /dev/null || exit 1
    sleep 5
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
    ~/go/bin/kustomize build -o test.yaml
    mv kustomization.yaml ./prod_patches/kustomization.yaml
    mv ./local_patches/kustomization.yaml kustomization.yaml
    cd $CURRENT_DIR
}

printf "Project home folder is:\n"
printf " - ${LightBlue}$SCRIPT_DIR\n"
printf "${NoColor}Apps found:\n${LightBlue}"
printf ' - %s\n' "${APPS[@]}"
printf "${NoColor}"