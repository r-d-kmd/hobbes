FROM mcr.microsoft.com/dotnet/core/aspnet:3.0
COPY /src/deploy /
WORKDIR /Server
ENTRYPOINT [ "dotnet", "collectors.azuredevops.dll" ]