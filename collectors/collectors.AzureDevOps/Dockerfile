FROM mcr.microsoft.com/dotnet/core/aspnet:3.0
COPY /src/deploy /
WORKDIR /azureDevopsCollector
ENTRYPOINT [ "dotnet", "azureDevopsCollector.dll" ]