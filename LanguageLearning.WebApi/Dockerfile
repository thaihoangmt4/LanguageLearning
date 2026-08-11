FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

COPY LanguageLearning.Common/LanguageLearning.Common.csproj LanguageLearning.Common/
COPY LanguageLearning.WebApi/LanguageLearning.WebApi.csproj LanguageLearning.WebApi/
RUN dotnet restore LanguageLearning.WebApi/LanguageLearning.WebApi.csproj

FROM restore AS publish
COPY . .
RUN dotnet publish LanguageLearning.WebApi/LanguageLearning.WebApi.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
WORKDIR /app
EXPOSE 8080

COPY --from=publish --chown=$APP_UID:$APP_UID /app/publish .
RUN mkdir -p /app/logs && chown "$APP_UID:$APP_UID" /app/logs
USER $APP_UID

ENTRYPOINT ["dotnet", "LanguageLearning.WebApi.dll"]
