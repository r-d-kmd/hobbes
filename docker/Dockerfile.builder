FROM mcr.microsoft.com/dotnet/core/sdk:3.1
WORKDIR /source

RUN apt-get update
RUN apt-get install dirmngr gnupg apt-transport-https ca-certificates -y
RUN apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
RUN sh -c 'echo "deb https://download.mono-project.com/repo/ubuntu stable-bionic main" > /etc/apt/sources.list.d/mono-official-stable.list'
RUN apt-get update
RUN apt-get install mono-complete -y

ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet tool install --global Paket

COPY paket.dependencies .
COPY .paket/ /source/.paket/
COPY .lib/ /source/.lib/

# copy csproj and restore as distinct layers
ONBUILD COPY ./src/*.fsproj .

# copy and publish app and libraries
ONBUILD COPY ./src/ .
ONBUILD RUN dotnet publish -c release -o /app
