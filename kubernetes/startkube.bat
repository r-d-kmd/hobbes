minikube start --vm-driver hyperv --hyperv-virtual-switch "Primary Virtual Switch" -p test

minikube addons enable ingress -p test