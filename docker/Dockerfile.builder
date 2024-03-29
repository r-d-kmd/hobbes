
ARG DOTNET_VERSION=5.0
FROM kmdrd/sdk:${DOTNET_VERSION} AS build-base

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

RUN dotnet 
RUN dotnet paket update

FROM build-base
COPY ./common/hobbes.messaging/src/Broker.fs /source/Broker.fs
ONBUILD COPY ./src /source
WORKDIR /source

ONBUILD RUN echo "dotnet \"$(expr $(ls *.?sproj) : '\(.*\)\..sproj').dll\"\n" >> /tmp/start.sh
ONBUILD RUN chmod +x /tmp/start.sh
ONBUILD RUN cat /tmp/start.sh