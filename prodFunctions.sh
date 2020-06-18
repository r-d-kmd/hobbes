function get_script_dir(){
     SOURCE="${BASH_SOURCE[0]}"
     # While $SOURCE is a symlink, resolve it
     while [ -h "$SOURCE" ]; do
          DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
          SOURCE="$( readlink "$SOURCE" )"
          # If $SOURCE was a relative symlink (so no "/" as prefix, need to resolve it relative to the symlink base directory
          [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE"
     done
     DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
     echo "$DIR"
}

SCRIPT_DIR=$(get_script_dir)
KUBERNETES_DIR="$SCRIPT_DIR/kubernetes"

function applyProductionYaml() {
    cd $KUBERNETES_DIR
    mv kustomization.yaml ./local_patches/kustomization.yaml
    mv ./prod_patches/kustomization.yaml kustomization.yaml
    kustomize build -o test.yaml
    mv kustomization.yaml ./prod_patches/kustomization.yaml
    mv ./local_patches/kustomization.yaml kustomization.yaml
    az login -u $1 -p $2
    az aks get-credentials --resource-group hobbes-rg --name hobbes-kub
    kubectl apply -f test.yaml
    cd ..
}