# MyOit.Portal — Multi-Stage-Build nach demselben Muster wie die API (ohne Migrator).
# Build-Kontext ist das Repo-Root:
#   docker build -f MyOit.Portal/Dockerfile -t iotwelt-portal .

# ---- build ------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore-Layer vor dem Quellcode (siehe IotWelt.API/Dockerfile)
COPY Directory.Build.props ./
COPY MyOit.Portal/MyOit.Portal.csproj MyOit.Portal/
COPY IotWelt.Common/IotWelt.Common.csproj IotWelt.Common/
COPY IotWelt.ServiceDefaults/IotWelt.ServiceDefaults.csproj IotWelt.ServiceDefaults/
RUN dotnet restore MyOit.Portal/MyOit.Portal.csproj

COPY MyOit.Portal/ MyOit.Portal/
COPY IotWelt.Common/ IotWelt.Common/
COPY IotWelt.ServiceDefaults/ IotWelt.ServiceDefaults/
RUN dotnet publish MyOit.Portal/MyOit.Portal.csproj -c Release -o /app/publish --no-restore -p:UseAppHost=false

# ---- final ------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir /keys && chown $APP_UID /keys

WORKDIR /app
COPY --from=build /app/publish .

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "MyOit.Portal.dll"]
