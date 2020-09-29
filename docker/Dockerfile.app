FROM kmdrd/sdk
WORKDIR /source
ARG CONFIGURATION=release
ENV BUILD_CONFIGURATION release
ENV BUILD_ENV docker

COPY ./build/paket.* ./
COPY build/.paket /.paket

RUN mono /.paket/paket.exe restore

COPY paket.references /paket.references
COPY hobbes.properties.targets .

ONBUILD COPY ./src/ .

ONBUILD RUN cat /paket.references >> ./paket.references

ONBUILD RUN dotnet publish -c ${BUILD_CONFIGURATION} -o /app