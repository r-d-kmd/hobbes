
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

COPY .paket/Paket.Restore.targets /.paket/Paket.Restore.targets
COPY paket.lock .
COPY paket.dependencies .

RUN if [ ! -f ".config/dotnet-tools.json" ]; then \
    dotnet new tool-manifest; \
    dotnet tool install paket; \
    fi
RUN dotnet tool restore
RUN dotnet paket restore

FROM build-base as builder
COPY ./src /source
WORKDIR /source

RUN echo "dotnet \"$(expr $(ls *.?sproj) : '\(.*\)\..sproj').dll\"\n" >> /tmp/start.sh
RUN chmod +x /tmp/start.sh
RUN cat /tmp/start.sh
RUN dotnet publish -c ${BUILD_CONFIGURATION} -o /app

FROM builder AS build
ARG EXECUTABLE
ARG MAJOR_ARG=0
ARG MINOR_ARG=0
ARG BUILD_VERSION_ARG=1

ENV EXECUTABLE=${EXECUTABLE}
ENV MAJOR=${MAJOR_ARG}
ENV MINOR=${MINOR_ARG}
ENV BUILD_VERSION=${BUILD_VERSION_ARG}

# final stage/image
FROM kmdrd/runtime:5.0
COPY --from=build /tmp/start.sh /tmp/start.sh 
WORKDIR /app
COPY --from=build /app .

ENV port 8085
ENTRYPOINT /tmp/start.sh