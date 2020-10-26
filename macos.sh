#! /bin/bash
function setupTest(){
    local CURRENT_DIR=$(pwd)
    cd $SCRIPT_DIR
    #dotnet test
    
    kubectl port-forward service/db-svc 30084:5984 &
    start
    kubectl port-forward service/db-svc 5984:5984 &
    kubectl port-forward service/gateway-svc 30080:80 &
    sleep 3
    printf "${Purple}Publishing transformations and configurations\n"
    publish
    PRESULT=$?
    if [ "$PRESULT" -eq "0" ]
    then
        printf "${Purple}Publishing\n"
        sleep 10
        kubectl wait --for=condition=complete job/publish --timeout=60s
        printf "${Purple}Published${NoColor}\n" 
        printf "${Cyan}Sync\n"
        CONFIG_COUNT=$(curl --silent http://admin:password@127.0.0.1:5984/configurations/_all_docs | grep "key" | wc -l)
        printf "${Cyan}Syncing${NoColor}\n"
        sync
        SREULT=$?
        if [ "$SREULT" -eq 0 ]
        then
            sleep 10
            kubectl wait --for=condition=complete job/sync --timeout=60s
            printf "${Green}Syncronization and tranformations completed.\n${NoColor}"
        else
            printf "${Red}Syncronization failed.\n${NoColor}"
            cd $CURRENT_DIR
            exit $SREULT
        fi
    else
       printf "${Red}Publishing failed.\n${NoColor}"
       cd $CURRENT_DIR
       exit $PRESULT
    fi
    cd $CURRENT_DIR
}

function test(){
    printf "${NoColor}"
    logs job/publish
    logs job/sync
    kubectl port-forward service/gateway-svc 30080:80 >> /dev/null &
    sleep 30
    newman run https://api.getpostman.com/collections/7af4d823-d527-4bc8-88b0-d732c6243959?apikey=$(PM_APIKEY) -e https://api.getpostman.com/environments/b0dfd968-9fc7-406b-b5a4-11bae0ed4b47?apikey=$(PM_APIKEY) --env-var "ip"=$(minikube ip) --env-var "master_key"=$(MASTER_KEY)
} 