# IotWelt.API — Multi-Stage-Build. Build-Kontext ist das Repo-Root:
#   docker build -f IotWelt.API/Dockerfile -t iotwelt-api .                       (API)
#   docker build -f IotWelt.API/Dockerfile --target migrator -t iotwelt-migrator . (Schema-Migration)

# ---- build: SDK-Image, nur zum Kompilieren -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Zuerst nur Projektdateien kopieren und restoren: Dieser Layer bleibt im Cache, solange sich keine
# csproj-Datei und keine Paketversion ändert — eine Änderung an einer .cs-Datei restored nicht neu.
COPY Directory.Build.props ./
COPY IotWelt.API/IotWelt.API.csproj IotWelt.API/
COPY IotWelt.Common/IotWelt.Common.csproj IotWelt.Common/
COPY IotWelt.ServiceDefaults/IotWelt.ServiceDefaults.csproj IotWelt.ServiceDefaults/
RUN dotnet restore IotWelt.API/IotWelt.API.csproj

# Dann den Quellcode — nur die Projekte, die die API braucht (Änderungen am Portal lassen den Cache in Ruhe)
COPY IotWelt.API/ IotWelt.API/
COPY IotWelt.Common/ IotWelt.Common/
COPY IotWelt.ServiceDefaults/ IotWelt.ServiceDefaults/
RUN dotnet publish IotWelt.API/IotWelt.API.csproj -c Release -o /app/publish --no-restore -p:UseAppHost=false

# ---- migrator-build: EF-Core-Migrations-Bundle (eine ausführbare Datei mit allen Migrationen) ----------
FROM build AS migrator-build
RUN dotnet tool install --global dotnet-ef --version 10.0.9
ENV PATH="$PATH:/root/.dotnet/tools"
# Das Bundle startet zum Erzeugen den Host der API; Aspire verlangt dabei einen Connection-String.
# Der Platzhalter wird nie benutzt (keine Verbindung beim Bauen) und ist kein Secret.
RUN ConnectionStrings__iotweltdb="Server=build-placeholder" \
    dotnet ef migrations bundle --project IotWelt.API --startup-project IotWelt.API \
        --configuration Release -r linux-x64 -o /app/efbundle

# ---- migrator: framework-dependent Bundle auf demselben Basis-Image wie die API ------------------------
# Self-contained auf runtime-deps wäre nicht kleiner (das Bundle bringt dann die ganze ASP.NET-Runtime mit),
# so teilen sich API und Migrator die Basis-Layer und belegen sie auf der Platte nur einmal.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrator
WORKDIR /app
COPY --from=migrator-build /app/efbundle .
USER $APP_UID
# Connection-String kommt zur Laufzeit per Env-Var ConnectionStrings__iotweltdb
ENTRYPOINT ["./efbundle"]

# ---- final: nur die ASP.NET-Core-Runtime und die veröffentlichte App -----------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# curl nur für den Compose-Healthcheck (das Basis-Image hat keins) — Größenvergleich ist Teil der Übung
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Verzeichnis für die Data-Protection-Schlüssel (DataProtection:KeysPath in appsettings.Staging.json).
# Gehört dem App-User, damit ein gemountetes Volume beim ersten Start dessen Rechte übernimmt.
RUN mkdir /keys && chown $APP_UID /keys

WORKDIR /app
COPY --from=build /app/publish .

# Nicht als root laufen; die .NET-Images bringen den User "app" mit (APP_UID), Kestrel lauscht auf 8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "IotWelt.API.dll"]
