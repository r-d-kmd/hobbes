FROM kmdrd/sdk:hobbes AS build

# final stage/image
FROM kmdrd/aspnet

ENV USER "user"
ENV PASSWORD "2ZLdDfjniG"

WORKDIR /app
COPY --from=build /app .

ENTRYPOINT [ "dotnet", "hobbes.qtest.dll"]