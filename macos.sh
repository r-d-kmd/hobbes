#! /bin/bash
function setupTest(){
    local CURRENT_DIR=$(pwd)
    cd $SCRIPT_DIR
    #dotnet test
    set -e
    #start 
    echo "Await running state"
    all
    #Forward ports to be able to communicate with the cluster
    kubectl port-forward service/gateway-svc 30080:80 &
    kubectl port-forward service/db-svc 30084:5984 &
    
    #wait a few second to be sure the port forwarding is in effect
    sleep 3
    IP="$(minikube ip)"
    SERVER="http://${ip}:30080"

    printf "${Purple}test that the server and DB is accessible${NoColor}"
    curl "${SERVER}:30084"
    printf "${Cyan}DB is running${NoColor}"

    front_url="${SERVER}:30080"
    curl ${front_url}/ping
    printf "${Cyan}gateway is running${NoColor}"

    printf "${Purple}Publishing transformations and configurations\n" 
    
    set -e
    publish 
    echo "transformations and configurations were published successfully"
    echo "syncronize and wait for it to complete"

    sync
    set +e

    WAIT=300
    printf "${Yellow}Waiting ${WAIT}s for sync to complete\n${NoColor}"
    sleep $WAIT
    
    cd $CURRENT_DIR
}

function test(){
    set -e
    NAME=$(kubectl get pods -l app=gateway -o name) 
    newman run https://api.getpostman.com/collections/7af4d823-d527-4bc8-88b0-d732c6243959?apikey=${PM_APIKEY} -e https://api.getpostman.com/environments/b0dfd968-9fc7-406b-b5a4-11bae0ed4b47?apikey=${PM_APIKEY} --env-var "ip"=${IP} --env-var "master_key"=${MASTER_KEY}
    echo "*********************GATEWAY********************************"
    echo "*********************GATEWAY********************************"
    echo "*********************GATEWAY********************************"
    logs gateway | printf "${Green}%s\n" &
    logs uniform | printf "${Purple}%s\n" &
    echo "*********************UNIFORM********************************"
    echo "*********************UNIFORM********************************"
    echo "*********************UNIFORM********************************"
    set +e
} 