APPS=(db hobbes azuredevops git qtest uniformdata)
VOLUMES=(db)

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
    for var in "$@"
    do
        local FILE_NAME=$(ls *$var*-deployment.yaml)
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

function build(){    
    eval $(minikube -p minikube docker-env)
    ECHO "Starting Build"
    cd ..
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
    cd kubernetes
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
    build
    kubectl apply -f env.JSON;

    for i in "${APPS[@]}"; do kubectl apply -f $i-deployment.yaml,$i-svc.yaml; done
    for i in "${VOLUMES[@]}"; do kubectl apply -f $i-volume.yaml; done

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