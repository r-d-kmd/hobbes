FROM kmdrd/sdk:hobbes AS build

# final stage/image
FROM kmdrd/aspnet

WORKDIR /app
COPY --from=build /app .
COPY --from=build /.paket/ /.paket

ENV DB_SERVER_URL http://db-svc:5984

ENTRYPOINT [ "dotnet", "hobbes.server.dll"]