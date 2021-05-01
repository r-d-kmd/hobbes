function startJob(){
    local CURRENT_DIR=$(pwd)
    cd $KUBERNETES_DIR
    kubectl delete job.batch/$1 &> /dev/null
    
    kubectl apply -f sync-job.yaml
    
    printf "${Cyan}$1 started\n${NoColor}"
    
    cd $CURRENT_DIR
}

function sync(){
    startJob sync
}