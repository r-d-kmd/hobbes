FROM kmdrd/sdk:hobbes AS build

# final stage/image
FROM kmdrd/aspnet

ENV USER "user"
ENV PASSWORD "2ZLdDfjniG"
ENV DB_SERVER_URL http://db-svc:5984

WORKDIR /app
COPY --from=build /app .

ENTRYPOINT [ "dotnet", "hobbes.qtest.dll"]