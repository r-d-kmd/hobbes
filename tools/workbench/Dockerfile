FROM kmdrd/app AS build

# final stage/image
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1

ARG WORKER_NAME

WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet","hobbes.workbench.dll","--publish", "development"]