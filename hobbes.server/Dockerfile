FROM kmdrd/sdk:hobbes AS build

# final stage/image
FROM kmdrd/aspnet

WORKDIR /app
COPY --from=build /app .

ENTRYPOINT [ "dotnet", "hobbes.server.dll"]