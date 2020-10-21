FROM hobbes.azurecr.io/sdk
WORKDIR /source
ARG CONFIGURATION=release
ARG ARG_FEED
ENV BUILD_CONFIGURATION release
ENV BUILD_ENV docker

COPY ./build/paket.* ./
COPY build/.paket /.paket

RUN dotnet new tool-manifest
RUN dotnet tool install paket --version=5.249.0
RUN dotnet paket config add-credentials https://kmddk.pkgs.visualstudio.com/45c29cd0-03bf-4f63-ac71-3c366095dda9/_packaging/KMD_Package_Feed/nuget/v2 --username na --password ${ARG_FEED}
#RUN dotnet paket update

COPY paket.references /paket.references
COPY hobbes.properties.targets .
#ONBUILD RUN cat paket.lock | sed 's/>= netcoreapp5.0/>= netcoreapp3.1/' >> paket.lock
