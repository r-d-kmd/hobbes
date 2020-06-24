FROM kmdrd/sdk
WORKDIR /source
ARG CONFIGURATION=release
ENV BUILD_CONFIGURATION ${CONFIGURATION}
ENV BUILD_ENV docker

COPY ./build/paket.* ./
COPY build/.paket /.paket

RUN mono /.paket/paket.exe restore

COPY .lib/ ../.lib/
COPY hobbes.properties.targets ./

ONBUILD COPY ./src/ .
ONBUILD RUN dotnet publish -c ${BUILD_CONFIGURATION} -o /app