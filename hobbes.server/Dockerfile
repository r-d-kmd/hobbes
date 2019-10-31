FROM mcr.microsoft.com/dotnet/core/aspnet:3.0
ARG version="debug"
ENV VERSION=$version
COPY /src/deploy /
WORKDIR /Server
ENTRYPOINT [ "dotnet", "hobbes.server.dll" ]