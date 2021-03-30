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

COPY ./.config/dotnet-tools.json /.config/dotnet-tools.json
RUN dotnet tool restore

FROM sdk as build
COPY ./src /source
WORKDIR /source

#install packages
RUN dotnet paket install
RUN dotnet paket restore

#create a script that just starts the app. THe reg ex is just to make the file generic
RUN echo "dotnet \"$(expr $(ls *.?sproj) : '\(.*\)\..sproj').dll\"\n" >> /tmp/start.sh
RUN chmod +x /tmp/start.sh
RUN cat /tmp/start.sh

#Build the app
RUN dotnet publish -c ${BUILD_CONFIGURATION} -o /app

# final stage/image
FROM kmdrd/runtime:DOTNET_VERSION
COPY --from=build /tmp/start.sh /tmp/start.sh 
WORKDIR /app
COPY --from=build /app .

ENV COLLECTION test
ENV HOST "http://gateway-svc"
ENV PAT ""
ENV port 8085

ENTRYPOINT /tmp/start.sh