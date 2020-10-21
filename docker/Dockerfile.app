FROM hobbes.azurecr.io/sdk
WORKDIR /source
ARG CONFIGURATION=release
ARG ARG_FEED=433hp5ologxbv6itb4lir7mzde2qf6x75tnwrzvhom3udr6qomrq
ENV FEED_PAT ${ARG_FEED}
ENV BUILD_CONFIGURATION release
ENV BUILD_ENV docker
COPY ./build/paket.* ./
COPY build/.paket /.paket

RUN dotnet new tool-manifest
RUN dotnet tool install paket --version=5.249.0
#RUN dotnet paket update

COPY paket.references /paket.references
COPY hobbes.properties.targets .
#ONBUILD RUN cat paket.lock | sed 's/>= netcoreapp5.0/>= netcoreapp3.1/' >> paket.lock
