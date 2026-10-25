# KanaanFlow sync service. The deploy workflow publishes the app into ./app before building this image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY app/ .
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "KanaanFlow.Sync.dll"]
