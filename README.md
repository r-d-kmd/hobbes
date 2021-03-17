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

### Deploying to the kubernetes cluster
First of all we need to make sure the minikube is running. This can be done by hand or by utilizing some helper functions we have
by hand: 'minikube start --driver=docker --cpu=<number of CPUs you can spare> --memory=XGb <X being more than 4>
Or using helper functions
- `source functions.sh`
- `startKube`

It's recommended to source the script because it also configures the shell environment. If it's the first time you start the kube you might have to source again for everyting to be set up correctly

#### Build the world
The deployment script expects the services and workers to already be built. To do this we need to trigger a build

    dotnet fake build 

takes care of that. If you haven't already restored the tools you'll get and error. run `dotnet tool restore` to fix that and then re-run the above command.
If the build goes well you are ready to deploy and run the first test

#### deploying and testing
After starting the kube and building the services and workders navigate to `./tests/` and execute the command

    dotnet fake build

This times another build file is used (`./tests/build.fsx`). It has a series of stept to deploy the services and workers to the kubernetes cluster as well as fetching data that can be used for testing. The most likely errors are that you have not set up your Azure DevOps PAT in the env.JSON file (or that you simply haven't created that file yet).

If everything is set up correctly you will now have everything deployed, populated with data and tested ready to start developing new features

