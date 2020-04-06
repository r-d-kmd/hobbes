FROM kmdrd/sdk:hobbes-stretch AS build

# final stage/image
FROM kmdrd/aspnet:stretch

ENV LD_LIBRARY_PATH=/root/.nuget/packages/gitversion.tool/5.1.2/tools/netcoreapp3.0/any/runtimes/debian.9-x64/native/
WORKDIR /app
COPY --from=build /app .
#RUN paket update

ENTRYPOINT [ "dotnet", "collectors.git.dll" ]