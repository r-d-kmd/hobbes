# Tools needed to be installed to run the hobbes server

The following actions must be performed

## windows

- install [https://golang.org/doc/install?download=go1.13.windows-amd64.msi](go)
- install [https://github.com/kubernetes-sigs/kind](kind)
- install [https://chocolatey.org/docs/installation](chocolatey)
- `choco install kubernetes-cli --version=1.4.6`

## Mac

- install [https://www.virtualbox.org/wiki/Downloads](VirtualBox)
- `curl -Lo minikube https://storage.googleapis.com/minikube/releases/latest/minikube-darwin-amd64 && chmod +x minikube`
- `sudo mv minikube /usr/local/bin`