ARG DOTNET_VERSION=5.0
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} as sdk-image
FROM sdk-image as sdk
WORKDIR /source
 
RUN apt-get update
RUN apt-get install mono-complete -y

ARG BUILD_CONFIGURATION_ARG=release
ENV BUILD_CONFIGURATION ${BUILD_CONFIGURATION_ARG}
ENV BUILD_ENV docker

ARG FEED_PAT_ARG=""
ARG FEED_USER_ARG=""
ARG FEED_PASSWORD_ARG=""
ARG BUILD_CONFIGURATION_ARG="Release"

ENV BUILD_CONFIGURATION ${BUILD_CONFIGURATION_ARG}
ENV FEED_PAT ${FEED_PAT_ARG}
ENV FEED_USER ${FEED_USER_ARG}
ENV FEED_PASSWORD ${FEED_PASSWORD_ARG}

RUN if [ -n "$FEED_PAT" ]; then export FEED_USER="$FEED_PAT"; export FEED_PASSWORD="$FEED_PAT"; fi

COPY paket.dependencies .

RUN dotnet new tool-manifest
RUN dotnet tool install paket

RUN dotnet tool restore
RUN dotnet paket update

FROM sdk as build
COPY ./src /source
WORKDIR /source

RUN echo "dotnet \"$(expr $(ls *.?sproj) : '\(.*\)\..sproj').dll\"\n" >> /tmp/start.sh
RUN chmod +x /tmp/start.sh
RUN cat /tmp/start.sh

ARG EXECUTABLE
ENV EXECUTABLE=${EXECUTABLE}

# final stage/image
RUN dotnet publish -c ${BUILD_CONFIGURATION} -o /app

# final stage/image
FROM kmdrd/runtime:5.0
COPY --from=build /tmp/start.sh /tmp/start.sh 
WORKDIR /app
COPY --from=build /app .

ENV COLLECTION test
ENV HOST "http://gateway-svc"
ENV PAT ""
ENV port 8085

ENTRYPOINT /tmp/start.sh