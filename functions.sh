APPS=(db hobbes collectordb azuredevops git)
VOLUMES=(db collectordb)

function getname(){
   POD_NAME=$(kubectl get all | grep pod/.*$1)
   POD_NAME="$( cut -d ' ' -f 1 <<< "$POD_NAME" )"; echo "$POD_NAME"
   echo $POD_NAME
}

function logs(){
    getname $1
    kubectl logs $2 $POD_NAME
}

function restart(){
    for var in "$@"
    do
        FILE_NAME=$(ls *$var*-deployment.yaml)
        kubectl scale --replicas=0 -f $FILE_NAME
        kubectl scale --replicas=1 -f $FILE_NAME
    done
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

function describe(){
    NAME=$(kubectl get pods -l app=$1 -o name)
    kubectl describe ${NAME}
}

function listServices(){
    minikube service list
}

function build(){
    eval $(minikube -p minikube docker-env)
    cd .. && fake build
}

function start(){
    build
    cd kubernetes
    kubectl apply -f env.JSON
    
    for i in "${APPS[@]}"; do kubectl apply -f $i-deployment.yaml,$i-svc.yaml; done
    for i in "${VOLUMES[@]}"; do kubectl apply -f $i-volume.yaml; done
}

function startkube(){
    set $PATH=$PATH:/Applications/VirtualBox.app/
    minikube start --vm-driver virtualbox
}

function update(){
    for i in "${APPS[@]}"; do kubectl apply -f $i-deployment.yaml,$i-svc.yaml; done
    for i in "${VOLUMES[@]}"; do kubectl apply -f $i-volume.yaml; done
    kubectl apply -f env.JSON
}