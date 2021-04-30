
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

source <(kubectl completion bash)

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

function getName(){
    echo "$(kubectl get pods | grep $1 | cut -d ' ' -f 1 )"
}

function logs(){
    local NAME=$(getName $1)
    kubectl wait --for=condition=ready pod/"$NAME" --timeout=60s
    kubectl logs $2 $NAME
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

function setDockerEnv(){
    eval $(minikube -p minikube docker-env)
}

function startKube(){
    minikube start --driver=docker --memory=4GB --cpus=4
    setDockerEnv
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


#This function builds the production yaml configuration in the kubernetes folder.
function applyProductionYaml() {
    local CURRENT_DIR=$(pwd)
    kubectl apply -f $SCRIPT_DIR/env.JSON
    for kube_dir in $(find $SCRIPT_DIR -type d -name kubernetes)
    do
        echo "Directory: " $kube_dir
        if [ -f "$kube_dir/prod_patches/kustomization.yaml" ]
        then
            echo "moving" $kube_dir/kustomization.yaml "into" $kube_dir/local_patches/kustomization.yaml 
            mv $kube_dir/kustomization.yaml $kube_dir/local_patches/kustomization.yaml
            echo "moving" $kube_dir/prod_patches/kustomization.yaml "into" $kube_dir/kustomization.yaml
            mv $kube_dir/prod_patches/kustomization.yaml $kube_dir/kustomization.yaml
            echo "Producing file"
            cd $kube_dir
            kustomize build -o test.yaml
            echo "moving" $kube_dir/kustomization.yaml "back into" $kube_dir/prod_patches/kustomization.yaml
            mv $kube_dir/kustomization.yaml $kube_dir/prod_patches/kustomization.yaml
            echo "moving" $kube_dir/local_patches/kustomization.yaml "back into" $kube_dir/kustomization.yaml
            mv $kube_dir/local_patches/kustomization.yaml $kube_dir/kustomization.yaml
#           kubectl apply -k $kube_dir
        fi
    done
    
    #awaitRunningState
    
    cd $CURRENT_DIR
}

function skipRestore(){
    export PAKET_SKIP_RESTORE_TARGETS=true
}

function setDefaultVersion(){
    export MAJOR=0
    export MINOR=0
    export BUILD_VERSION=1
}

function setEnvVars(){
    cd $SCRIPT_DIR
    if [ -z ${AZURE_DEVOPS_PAT+x} ]; then 
        export AZURE_DEVOPS_PAT="$(echo "$(cat env.JSON | jq -r .data.AZURE_DEVOPS_PAT)" | base64 -d)"
    fi
    export FEED_PAT=$AZURE_DEVOPS_PAT
    if [ -z ${COUCHDB_USER+x} ]; then 
        export COUCHDB_USER="$(echo "$(cat env.JSON | jq -r .data.COUCHDB_USER)" | base64 -d)"
    fi
    if [ -z ${COUCHDB_PASSWORD+x} ]; then 
        export COUCHDB_PASSWORD="$(echo "$(cat env.JSON | jq -r .data.COUCHDB_PASSWORD)" | base64 -d)"
    fi
    if [ -z ${MASTER_USER+x} ]; then 
        export MASTER_USER="$(echo "$(cat env.JSON | jq -r .data.MASTER_USER)" | base64 -d)"
    fi
    cd -
}

function setupLocalEnv(){
    skipRestore
    setDefaultVersion
    setEnvVars
    setDockerEnv
}

function vscode(){
    setupLocalEnv    
    code $1
}

function wrap() {
    setEnvVars
    podName=$1er
    cat <<EOF > $1.yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: $podName
spec:
  template:
    spec:
      containers:
      - name: $podName
        image: tester
        envFrom:
        - secretRef:
            name: env
        imagePullPolicy: Never
        env:
        - name: target
          value: "$1"
        - name: AZURE_DEVOPS_PAT
          value: "$AZURE_DEVOPS_PAT"
        - name: COUCHDB_PASSWORD
          value: "$COUCHDB_PASSWORD"
        - name: COUCHDB_USER
          value: "$COUCHDB_USER"
        - name: MASTER_USER
          value: "$MASTER_USER"
      restartPolicy: Never
  backoffLimit: 0
EOF
    kubectl apply -f $1.yaml
    sleep 30
    kubectl describe job/$podName
    kubectl logs job/$podName -f
    #kubectl wait --for=condition=complete job/$podName --timeout=120s
    
    if [ "$(kubectl logs job/$podName | grep "Status:" | awk '{print $NF}')" != "Ok" ]; then
        kubectl logs job/$podName
        #make the script fail if it's on the build server
        if [ -z ${ENV_FILE+x} ]; then
            echo "Running in local mode"
        else
            exit 1;
        fi
    fi
}

function test() {
    if [ -z ${ENV_FILE+x} ]; then
        echo "Running in local mode"
    else
       set -e
    fi
    cd $SCRIPT_DIR/tests
    
    echo "Publish"
    wrap "publish"
    
    echo "sync"
    dotnet fake build --target sync
    wrap "complete-sync"

    echo "test"
    wrap "test"

    cd -
}

setupLocalEnv

alias fake="dotnet fake"
alias paket="dotnet paket"