
ARG DOTNET_VERSION=5.0
FROM kmdrd/sdk:${DOTNET_VERSION}

ARG target="all"
ENV target ${target}

ARG envfile
ENV ENV_FILE=${envdata}

COPY . /tests
WORKDIR /tests

RUN  dotnet new tool-manifest
RUN  dotnet tool install paket
RUN  dotnet tool install fake-cli

COPY paket.dependencies .

RUN dotnet paket restore 

RUN echo "echo \"${ENV_FILE}\" >> env.JSON && dotnet fake build --target ${target}" >> /tmp/start.sh
RUN chmod +x /tmp/start.sh
ENTRYPOINT /tmp/start.sh