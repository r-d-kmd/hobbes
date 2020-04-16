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
    FILE_NAME=$(ls *$1*-deployment.yaml)
    kubectl scale --replicas=0 -f $FILE_NAME
    kubectl scale --replicas=1 -f $FILE_NAME
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

function start(){
    eval $(minikube -p minikube docker-env)
    cd .. && fake build
    cd kubernetes
    kubectl apply -f env.JSON
    kubectl apply -f db-deployment.yaml,db-svc.yaml,db-volume.yaml,hobbes-deployment.yaml,hobbes-svc.yaml,collectordb-volume.yaml,collectordb-deployment.yaml,collectordb-svc.yaml,azuredevops-deployment.yaml,azuredevops-svc.yaml
}

function startkube(){
    set $PATH=$PATH:/Applications/VirtualBox.app/
    minikube start --vm-driver virtualbox
}

function update(){
    kubectl apply -f azuredevops-deployment.yaml,azuredevops-svc.yaml,collectordb-deployment.yaml,collectordb-svc.yaml,collectordb-volume.yaml,hobbes-deployment.yaml,db-deployment.yaml,hobbes-svc.yaml,db-svc.yaml,db-volume.yaml,env.JSON
}