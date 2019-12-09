kubectl scale --replicas=0 -f %1-deployment.yaml
kubectl scale --replicas=1 -f %1-deployment.yaml 