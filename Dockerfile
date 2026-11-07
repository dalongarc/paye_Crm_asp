# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["Paye.sln", "./"]
COPY ["src/Paye.Core/Paye.Core.csproj", "src/Paye.Core/"]
COPY ["src/Paye.Infrastructure/Paye.Infrastructure.csproj", "src/Paye.Infrastructure/"]
COPY ["src/Paye.Web/Paye.Web.csproj", "src/Paye.Web/"]

RUN dotnet restore "src/Paye.Web/Paye.Web.csproj"

COPY ["src/", "src/"]
WORKDIR "/src/src/Paye.Web"
RUN dotnet publish "Paye.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Create persistent directories for Data Protection keys and private file uploads owned by non-root user
RUN mkdir -p /var/lib/paye/keys /var/lib/paye/private-uploads \
    && chown -R $APP_UID:$APP_UID /var/lib/paye /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    Security__DataProtectionKeysPath=/var/lib/paye/keys \
    Security__PrivateUploadsPath=/var/lib/paye/private-uploads

USER $APP_UID
EXPOSE 8080

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD ["dotnet", "Paye.Web.dll", "--healthcheck"]

ENTRYPOINT ["dotnet", "Paye.Web.dll"]
