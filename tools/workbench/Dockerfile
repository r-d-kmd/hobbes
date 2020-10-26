FROM base AS build
COPY ./src/ .
RUN cat /paket.references >> ./paket.references
RUN dotnet publish -c Releasae -o /app

# final stage/image
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1

WORKDIR /app
COPY --from=build /app .

ENV COLLECTION test
ENV HOST "http://gateway-svc"
ENV PAT ""

ENTRYPOINT ["dotnet","hobbes.workbench.dll"]