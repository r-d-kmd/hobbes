
## Builds

start by running `source functions.sh` from the root of the project. This file includes a number of utility functions. One of them being build. It takes one or two arguments. The first being the name of the build and the second (optinal) argument os the maximum number of concurrent builds. Usually it's a good idea to set a number larger than 8 (if the machine building supports that many concurrent threads). If however there's a build error omit the number, since the build log is pretty much unsuable when build in parallel

### Complete
When building for the first time or after removing all docker images or similar use the **Complete** target. THis will create a debug version of the sdk image. Failing to do so it's still possible to compile the solution but it will run in release configuration

### All
If chaning the dependencies or the commonlibraries you should you the target **all** this will build the base docker image as well as all the apps

## Apps
If you have change any of the apps, you can either run a target named the same as the app i.e. **gateway**, **calculator** etc or you can simply run __fake build__ using the default target that builds all apps but __not__ the sdk image

