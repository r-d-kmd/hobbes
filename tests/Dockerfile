ARG DOTNET_VERSION=5.0
FROM kmdrd/sdk:${DOTNET_VERSION}

ENV target "publish"

ARG ENV_FILE
ENV ENV_FILE=${ENV_FILE}

COPY . /tests
WORKDIR /tests

RUN  dotnet new tool-manifest
RUN  dotnet tool install paket
RUN  dotnet tool install fake-cli

COPY paket.dependencies .

RUN dotnet paket restore 

RUN dotnet fake build --list

RUN echo "dotnet fake build --target \$target \$args" > /tmp/start.sh
RUN chmod +x /tmp/start.sh

CMD /tmp/start.sh