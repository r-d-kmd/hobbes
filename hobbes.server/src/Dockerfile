FROM microsoft/dotnet:3.0-aspnetcore-runtime
COPY /deploy /
WORKDIR /Server
ENTRYPOINT [ "dotnet", "hobbes.server.dll" ]