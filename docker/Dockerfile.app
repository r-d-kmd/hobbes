FROM hobbes.azurecr.io/sdk
WORKDIR /source
ARG CONFIGURATION=release
ARG ARG_FEED
ENV BUILD_CONFIGURATION release
ENV BUILD_ENV docker

COPY ./build/paket.* ./
COPY build/.paket /.paket

RUN mono /.paket/paket.exe config add-credentials https://kmddk.pkgs.visualstudio.com/45c29cd0-03bf-4f63-ac71-3c366095dda9/_packaging/KMD_Package_Feed/nuget/v2 --username na --password ${ARG_FEED}


COPY paket.references /paket.references
COPY hobbes.properties.targets .

ONBUILD COPY ./src/ .

ONBUILD RUN cat /paket.references >> ./paket.references
ONBUILD RUN mono /.paket/paket.exe update
ONBUILD RUN cat paket.lock | sed 's/>= netcoreapp5.0/>= netcoreapp3.1/' >> paket.lock
ONBUILD RUN mono --runtime=v4.0.30319 "/home/vsts/work/1/s/.paket/paket.exe
ONBUILD RUN dotnet publish -c ${BUILD_CONFIGURATION} -o /app