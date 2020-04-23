FROM kmdrd/sdk:hobbes AS build

# final stage/image
FROM kmdrd/aspnet

WORKDIR /app
COPY --from=build /app .
#RUN paket update

ENTRYPOINT [ "dotnet", "collectors.git.dll" ]