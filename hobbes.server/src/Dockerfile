FROM microsoft/dotnet:2.2-aspnetcore-runtime
COPY /deploy /
WORKDIR /Server
ENTRYPOINT [ "dotnet", "hobbes.server.dll" ]