
ARG DOTNET_VERSION=3.1
FROM kmdrd/sdk:${DOTNET_VERSION} AS build-base

ARG FEED_PAT_ARG
ARG FEED_USER_AEG
ARG FEED_PASSWORD_ARG

ENV FEED_PAT ${FEED_PAT_ARG}
ENV FEED_USER ${FEED_USER_ARG}
ENV FEED_PASSWORD ${FEED_PASSWORD_ARG}

COPY setEnv.sh /tmp/setEnv.sh
RUN cat /tmp/setEnv.sh >> /tmp/temp.sh
RUN chmod +x /tmp/temp.sh
RUN /tmp/temp.sh

COPY .fake/build.fsx/.paket/Paket.Restore.targets /.paket/Paket.Restore.targets
COPY paket.lock .
COPY paket.dependencies .

RUN dotnet paket restore

FROM build-base
ONBUILD COPY ./src /source
WORKDIR /source

ONBUILD RUN echo "dotnet \"$(expr $(ls *.?sproj) : '\(.*\)\..sproj').dll\"\n" >> /tmp/start.sh
ONBUILD RUN chmod +x /tmp/start.sh
ONBUILD RUN cat /tmp/start.sh
ONBUILD RUN dotnet publish -c ${BUILD_CONFIGURATION} -o /app