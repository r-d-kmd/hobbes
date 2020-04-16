eval $(minikube -p minikube docker-env)
cd .. && fake build
cd kubernetes
kubectl apply -f env.JSON
kubectl apply -f db-deployment.yaml,db-svc.yaml,db-volume.yaml,hobbes-deployment.yaml,hobbes-svc.yaml,collectordb-volume.yaml,collectordb-deployment.yaml,collectordb-svc.yaml,azuredevops-deployment.yaml,azuredevops-svc.yaml
