
## Builds

We have three seperate stages in the build process. The build server only executes one of them. The other two seldom needs to be executed. The list below is in order of frequency

- Main build
This is the build that compiles the servers in the various containers such as hobbes-server, azuredevops-collector and git-collector. It uses a copy of the dlls from the common projects
- PushSdkImages
This build target builds the SDK images that includes a fresh copy of hobbes.core, hobbes.web and hobbes.helpers. This target nees to be executed if any of these projets change or if the file paket.dependencies changes or any of the files in ./shared folder are changed, added or deleted
- PushGenericImages
This target will very seldom have to be run. It only needs to be executed if any of the files in the ./docker folder change