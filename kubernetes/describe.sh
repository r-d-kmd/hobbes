NAME=$(kubectl get pods -l app=$1 -o name)
kubectl describe ${NAME}