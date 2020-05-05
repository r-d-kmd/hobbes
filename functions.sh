APPS=(db hobbes azuredevops git qtest uniformdata calculator configurations)
VOLUMES=(db)

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
KUBERNETES_DIR="$SCRIPT_DIR/kubernetes"

function getName(){
   local POD_NAME=$(kubectl get all \
                        | grep pod/$1\
                        | cut -d ' ' -f 1 \
                        | cut -d '/' -f 2)
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
    echo $POD_NAME
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

function build(){    
    eval $(minikube -p minikube docker-env)
    ECHO "Starting Build"
    local CURRENT_DIR=$(pwd)
    cd $SCRIPT_DIR
    if [ -z "$1" ]
    then
        fake build
    else
        for var in "$@"
        do
            fake build --target "hobbes.$var"
        done
        restart $1
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
    build
    kubectl apply -f env.JSON;

    for i in "${APPS[@]}"; do kubectl apply -f $i-deployment.yaml,$i-svc.yaml; done
    for i in "${VOLUMES[@]}"; do kubectl apply -f $i-volume.yaml; done
    cd $CURRENT_DIR
}

function startkube(){
    set $PATH=$PATH:/Applications/VirtualBox.app/
    minikube start --vm-driver virtualbox --disk-size=75GB
}

function update(){
    for i in "${APPS[@]}"; do kubectl apply -f $i-deployment.yaml,$i-svc.yaml; done
    for i in "${VOLUMES[@]}"; do kubectl apply -f $i-volume.yaml; done
    kubectl apply -f env.JSON
}