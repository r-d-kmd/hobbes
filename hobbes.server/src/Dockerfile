FROM microsoft/dotnet:2.2-aspnetcore-runtime
COPY /deploy /
WORKDIR /Server
ENV SERVER_PORT 8085
ENTRYPOINT [ "dotnet", "hobbes.server.dll" ]