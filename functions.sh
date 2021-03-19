
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

echo "Evaluating"
if [[ $(uname -s) == CYGWIN_NT* ]] || [[ $(uname -s) == "Darwin" ]] || [[ $(uname -s) == MINGW64_NT* ]]
then
    eval $(minikube docker-env)
else
    eval $(SHELL=/bin/bash; minikube -p minikube docker-env)
fi
#source <(kubectl completion bash)

if [[ $(uname -s) == CYGWIN_NT* ]]
then
   SCRIPT_DIR=$(pwd)
else
    SOURCE="${BASH_SOURCE[0]}"
    # While $SOURCE is a symlink, resolve it
    while [ -h "$SOURCE" ]; do
        printf "${LightBlue}$SOURCE${NoColor}\n"
        DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
        SOURCE="$( readlink "$SOURCE" )"
        # If $SOURCE was a relative symlink (so no "/" as prefix, need to resolve it relative to the symlink base directory
        [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE"
    done
    SCRIPT_DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
fi
KUBERNETES_DIR="$SCRIPT_DIR/kubernetes"

printf "${LightBlue}sourcing${NoColor}\n"
if [[ $(uname -s) == MINGW64_NT* ]]
then
    printf "${Red}Running on windows${NoColor}\n"
elif [[ $(uname -s) == CYGWIN_NT* ]]
then
    source <(cat macos.sh | dos2unix)
    printf "${Yellow}Running CygWin${NoColor}\n"
else
    printf "${Green}Mac${NoColor}\n"
    source macos.sh
fi

if [[ $(uname -s) = MINGW64_NT* ]]
then
    declare -a APPS=("db" "azuredevops" "calculator" "configurations" "gateway" "git" "uniformdata")
else
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
fi

function getName(){
    echo "$(kubectl get pods | grep $1 | cut -d ' ' -f 1 )"
}

function logs(){
    local NAME=$(getName $1)
    kubectl wait --for=condition=ready pod/"$NAME" --timeout=60s
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
    kubectl delete --all replicationcontroller
    kubectl delete --all statefulset
    kubectl delete --all pods
    kubectl delete --all pvc
    kubectl delete --all secrets
    kubectl delete --all job
    kubectl delete --all hpa
}
function build(){    
    local CURRENT_DIR=$(pwd)
    cd $SCRIPT_DIR
    if [ "$1" != "builder" ]; then        
        if [[ "$(docker images -q builder 2> /dev/null)" == "" ]]; then
            build builder
        fi
    fi 
    re='^[0-9]+$'
    if [ -z "$1" ]
    then 
        dotnet fake build
    elif [[ $1 =~ $re ]]
    then
        build "build" $1 
    else
        for LAST in $@; do :; done
        if [[ $LAST =~ $re ]]
        then
            P=$LAST
            echo "Running with $P parallel builds"
        else
            P=1
        fi
        for target in "$@"
        do
            if [[ $target =~ $re ]]
            then
               echo "Done building"
            else
                dotnet fake build --target "$target" --parallel $P
            fi
        done
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
    kubectl apply -f $SCRIPT_DIR/env.JSON
    for kube_dir in $(find $SCRIPT_DIR -type d -name kubernetes)
    do
        echo $kube_dir
        if [ -f "$kube_dir/kustomization.yaml" ]
        then
            kubectl apply -k $kube_dir
            if (( $? > 0 )); then exit $?; fi
        fi
    done
    
    #awaitRunningState
    
    cd $CURRENT_DIR
}

function startKube(){
    minikube start --driver=docker --memory=4GB --cpus=4
    
    if [[ $(uname -s) == CYGWIN_NT* ]] || [[ $(uname -s) == "Darwin" ]] || [[ $(uname -s) == MINGW64_NT* ]]
    then
        eval $(minikube docker-env)
    else
        eval $(SHELL=/bin/bash; minikube -p minikube docker-env)
    fi
}

function update(){
    local CURRENT_DIR=$(pwd)
    cd $KUBERNETES_DIR
    start
    kubectl apply -f env.JSON
    cd $CURRENT_DIR
}

function pingService(){
    curl -I -L -X GET "http://$(minikube ip):$1/ping"
}

function testServiceIsFunctioning(){
    pingService $1 2>/dev/null | grep HTTP | tail -1 | cut -d$' ' -f2
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
        if [[ $NAME != sync* ]] && [[ $NAME != publish* ]]
        then
            echo "Waiting for pod/$NAME"
            kubectl wait --for=condition=ready "pod/$NAME" --timeout=60s

            #empty if there are no pods running for the given name. Ie the wait timed out
            if [ -z "$(kubectl get pods --field-selector=status.phase=Running | grep $NAME)" ]
            then
                #print out the log so that we can get som einfo on what went wrong
                kubectl logs "pod/$NAME"
                exit 1
            fi
        fi
    done

    echo "Waiting for DB to be operational"
    while [ "$(logs gateway | grep "DB initialized")" != "DB initialized" ]
    do
        sleep 1
    done

    echo "Waiting for Rabbit-MQ to be operational"
    while [ "$(logs configurations | grep "Watching queue")" != "Watching queue: cache" ]
    do
        echo $(logs configurations | grep "Queue not yet ready" | tail -1)
        sleep 10
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
    
    kubectl apply -f $1-job.yaml
    
    printf "${Cyan}$1 started\n${NoColor}"
    
    cd $CURRENT_DIR
}


function publish(){
    local CURRENT_DIR=$(pwd)
    cd $SCRIPT_DIR
    cd tools/workbench
    
    docker build -t kmdrd/workbench .
    RESULT=$?
    if (( RESULT == 0)); then
        printf "${Green}Publisher built${NoColor}\n"
        startJob publish
        logs publish -f &
        RESULT=$?
    fi
    cd $CURRENT_DIR
}

function forward(){
    local NAME=$(getAppName $1)
    kubectl port-forward service/$NAME-svc $2:$3 &>/dev/null &
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

function addSource(){
    dotnet nuget add source --name KMD_FEED \
        https://kmddk.pkgs.visualstudio.com/45c29cd0-03bf-4f63-ac71-3c366095dda9/_packaging/KMD_Package_Feed/nuget/v2
}

function pushPackage(){
    nuget push -Source KMD_FEED -ApiKey az $1
}

function test(){
    #kubectl port-forward service/db-svc 5984:5984 &
    #kubectl port-forward service/gateway-svc 30080:80 &

    #PM_APIKEY="$(cat "postman api-key.md")"
    #newman run https://api.getpostman.com/collections/7af4d823-d527-4bc8-88b0-d732c6243959?apikey=${PM_APIKEY} -e https://api.getpostman.com/environments/b0dfd968-9fc7-406b-b5a4-11bae0ed4b47?apikey=${PM_APIKEY} --env-var "ip"=$(minikube ip) --env-var "master_key"=${MASTER_KEY} >> testresults.txt
    TESTRESULT=$?
    if [ "$TESTRESULT" -eq "0" ]
    then
        printf "${Green}********************* Test passed ***********************\n"
        cat testresults.txt
        printf "${Green}********************* Test passed ***********************\n"
        printf "${NoColor}"
    else
        printf "${Red}********************* Test failed ***********************\n"
        cat testresults.txt
        printf "${NoColor}"
        logs az
        printf "${Yellow}********************************************\n${NoColor}"
        logs gateway
        printf "${Yellow}********************************************\n${NoColor}"
        logs calc -f &
        printf "${Red}********************* Test failed ***********************\n"
    fi
    exit $TESTRESULT
}

printf "Project home folder is:\n"
printf " - ${LightBlue}$SCRIPT_DIR\n"
printf "${NoColor}Apps found:\n${LightBlue}"
printf ' - %s\n' "${APPS[@]}"
printf "${NoColor}"

alias fake="dotnet fake"
alias paket="dotnet paket"