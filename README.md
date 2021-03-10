## Coding convetions

We try to verify input and out put of functions. We do this using assert. We'd rather have asserts in the code than unit tests. Since asserts can test the same but will do so every time we debug. Resulting in a wider array of conditions being tested.

json being read and transmitted should be based on a concrete type. The de-/serialization should be done using Json.(de)serialize. If we only read the data or simply store whatever we receive in the db (as is the case when workbench is publishing) we can use FSharp.Data type providers

## Builds

start by running `source functions.sh` from the root of the project. This file includes a number of utility functions. One of them being `build`. It takes one or two arguments. The first being the name of the build and the second (optinal) argument is the maximum number of concurrent builds. Usually it's a good idea to set a number larger than the number of apps (if the machine building supports that many concurrent threads). If however there's a build error omit the number, since the build log is pretty much unsuable when building in parallel

### Builder
When building for the first time or after removing all docker images or similar start by building the target `builder` i.e `build builder`.
This creates a docker image used for caching packages and other stuff that rarely changes. If you make changes to the paket.dependencies file, You'd need to run this target again.

### Build
IF running `build` with no arguments the entire application will be build providing initial feedback such as compile warnings and errors on whether the application is good to go ie be tested. The build might fail with an error stating that the packages couldn't be downloaded. with a 401. The most likely cause is that you haven't set the environment variable FEED_PAT or that the PAT stored in that variable is no longer valid.

### setupTest
When the application has been build (and minikube started `minikube start`) execute the function `start`followed by `setupTest`this will start the application and populate with test data

## Getting started
To be able to run the application you are going to ned a few tools such as docker and minikube.
### Mac

#### docker
[Installing docker desktop on Mac](https://docs.docker.com/docker-for-mac/install/)

#### Installing minikube on mac
- `curl -Lo minikube https://storage.googleapis.com/minikube/releases/latest/minikube-darwin-amd64 && chmod +x minikube`
- `sudo mv minikube /usr/local/bin`

### Linux (for windows install under WSL2)

#### docker
[Installing docker engine on ubuntu](https://docs.docker.com/engine/install/ubuntu/#install-using-the-repository)

#### Installing minikube
- `curl -Lo minikube https://storage.googleapis.com/minikube/releases/latest/minikube-linux-amd64 && chmod +x minikube`
- `sudo mv minikube /usr/local/bin`
