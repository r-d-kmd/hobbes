#! /bin/bash
function setupTest(){
    local CURRENT_DIR=$(pwd)
    cd $SCRIPT_DIR
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