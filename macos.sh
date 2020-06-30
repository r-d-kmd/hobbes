#! /bin/bash

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

eval $(minikube -p minikube docker-env)

#source <(kubectl completion bash)
function test(){
    local CURRENT_DIR=$(pwd)
    cd $SCRIPT_DIR
    dotnet test
    source functions.sh
    ip=$(minikube ip)
    start
    awaitRunningState
    front_url="http://${ip}:30080"
    #test the gateway is functioning
    curl ${front_url}/ping
    #test the db is responding
    curl "http://${ip}:30084"
    #wait for queue to be ready
    sleep 10
    #publish transformations and configurations
    docker build -t workbench tools/workbench && docker run -dt workbench development --host "${front_url}" --masterkey Rno8hcqr9rXXs
    all
    sync
    sleep 60
    NAME=$(kubectl get pods -l app=gateway -o name) 
    newman run https://api.getpostman.com/collections/7af4d823-d527-4bc8-88b0-d732c6243959?apikey=${PM_APIKEY} -e https://api.getpostman.com/environments/b0dfd968-9fc7-406b-b5a4-11bae0ed4b47?apikey=${PM_APIKEY} --env-var "ip"=${ip} --env-var "master_key"=${MASTER_KEY}
    cd $CURRENT_DIR
}


declare -a APPS=(db)
function services(){
    local APP_NAME=""
    for PROJECT_FILE in $(find ${SCRIPT_DIR}/services -name *.fsproj)
    do
        local FILE_NAME=`basename $PROJECT_FILE`
        APP_NAME=$(echo $FILE_NAME | cut -d'.' -f 1 | tr '[:upper:]' '[:lower:]')
        APPS+=($APP_NAME)
    done 
    APP_NAME=""
    for PROJECT_FILE in $(find ${SCRIPT_DIR}/workers -name *.fsproj)
    do
        local FILE_NAME=`basename $PROJECT_FILE`
        if [[ "$FILE_NAME" = *.worker.* ]] 
        then
            APP_NAME=$(echo $FILE_NAME | cut -d'.' -f 1 | tr '[:upper:]' '[:lower:]')
        fi
        APPS+=($APP_NAME)
    done 
}
services


printf "Project home folder is:\n"
printf " - ${LightBlue} $SCRIPT_DIR\n"
printf "${NoColor}Apps found:\n${LightBlue}"
printf ' - %s\n' "${APPS[@]}"
printf "${NoColor}\n"


if [ $(uname -s) = "Darwin" ]
then
    printf "${Green}Mac${NoColor}\n"
    source macos.sh
else
    printf "${Red}Running on windows${NoColor}\n"
fi