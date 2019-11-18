FROM mcr.microsoft.com/dotnet/core/aspnet:3.0
COPY /src/deploy /
WORKDIR /Collector
ENTRYPOINT [ "dotnet", "collector.dll" ]