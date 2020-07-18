#! /bin/bash
function setupTest(){
    local CURRENT_DIR=$(pwd)
    cd $SCRIPT_DIR
    #dotnet test
    start 
    echo "Await running state"
    awaitRunningState
    all
    
    #Forward ports to be able to communicate with the cluster
    kubectl port-forward service/gateway-svc 30080:80 &
    kubectl port-forward service/db-svc 30084:5984 &
    
    #wait a few second to be sure the port forwarding is in effect
    sleep 3
    IP="127.0.0.1"
    SERVER="http://${ip}"

    printf "${LightBlue}test that the server and DB is accessible${NoColor}"
    curl "${SERVER}:30084"
    printf "${LightBlue}DB is running${NoColor}"

    front_url="${SERVER}:30080"
    curl ${front_url}/ping
    printf "${LightBlue}gateway is running${NoColor}"

    printf "${LightBlue}publish transformations and configurations${NoColor}"
    publish || exit 1

    LOGS=$(logs gateway) && echo ${LOGS##*$'\n'}
    LOGS=$(logs conf) && echo ${LOGS##*$'\n'}
    
    printf "${LightBlue}Syncronize${NoColor}"

    sync
    WAIT=300
    printf "${LightBlue}Waiting ${WAIT}s for sync to complete${NoColor}"
    sleep $WAIT
    
    cd $CURRENT_DIR
}

function test(){
    NAME=$(kubectl get pods -l app=gateway -o name) 
    newman run https://api.getpostman.com/collections/7af4d823-d527-4bc8-88b0-d732c6243959?apikey=${PM_APIKEY} -e https://api.getpostman.com/environments/b0dfd968-9fc7-406b-b5a4-11bae0ed4b47?apikey=${PM_APIKEY} --env-var "ip"=${IP} --env-var "master_key"=${MASTER_KEY} #|| exit 1
    echo "*********************GATEWAY********************************"
    echo "*********************GATEWAY********************************"
    echo "*********************GATEWAY********************************"
    logs gateway | tail -50
    echo "*********************UNIFORM********************************"
    echo "*********************UNIFORM********************************"
    echo "*********************UNIFORM********************************"
    logs uniform | tail -50
} 