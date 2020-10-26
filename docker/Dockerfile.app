FROM hobbes.azurecr.io/sdk
WORKDIR /source
ARG CONFIGURATION=release
ARG ARG_FEED

COPY docker/build/.paket/Paket.Restore.targets /.paket/Paket.Restore.targets
COPY docker/paket.references /paket.references
COPY docker/hobbes.properties.targets .
COPY docker/.lib/yaml-parser/*.* /source/.lib/

ENV FEED_PAT ${ARG_FEED}
ENV BUILD_CONFIGURATION release
ENV BUILD_ENV docker

RUN dotnet new tool-manifest
RUN dotnet tool install paket
COPY paket.dependencies .
RUN dotnet paket update