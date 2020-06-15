
## Builds

### Complete
When building for the first time or after removing all docker images or similar use the **Complete** target. THis will create a debug version of the sdk image. Failing to do so it's still possible to compile the solution but it will run in release configuration

### All
If chaning the dependencies or the commonlibraries you should you the target **all** this will build the base docker image as well as all the apps

## Apps
If you have change any of the apps, you can either run a target named the same as the app i.e. **gateway**, **calculator** etc or you can simply run __fake build__ using the default target that builds all apps but __not__ the sdk image