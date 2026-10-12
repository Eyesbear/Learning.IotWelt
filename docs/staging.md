# Staging mit Docker

Staging läuft im LAN in Linux-Containern (Docker Desktop auf Gandalf), ohne Visual Studio, mit echtem SQL Server.
Ports: **API 5080**, **Portal 5081**. Aspire (F5, API auf 5013) läuft parallel weiter.

Stand PR 2b: Images und Handbetrieb ohne Compose. Compose und Runbook folgen in PR 2c.

## Images

| Image | Dockerfile | Inhalt |
|---|---|---|
| `iotwelt-api` | `IotWelt.API/Dockerfile` (Stufe `final`) | ASP.NET-Core-Runtime + API, Port 8080 |
| `iotwelt-migrator` | `IotWelt.API/Dockerfile` (Stufe `migrator`) | EF-Core-Migrations-Bundle auf `aspnet:10.0`, läuft einmal und endet |
| `iotwelt-portal` | `MyOit.Portal/Dockerfile` | ASP.NET-Core-Runtime + Portal, Port 8080 |

Gebaut wird immer aus dem **Repo-Root** (Build-Kontext `.`), weil die Projekte `IotWelt.Common` und
`IotWelt.ServiceDefaults` mitgebraucht werden:

```powershell
docker build -f IotWelt.API/Dockerfile -t iotwelt-api .
docker build -f IotWelt.API/Dockerfile --target migrator -t iotwelt-migrator .
docker build -f MyOit.Portal/Dockerfile -t iotwelt-portal .
```

Die Images kennen keine Umgebung und keine Secrets. Beides kommt beim Start per `-e` (später aus `.env`):

| Variable | Wofür |
|---|---|
| `ASPNETCORE_ENVIRONMENT=Staging` | lädt `appsettings.Staging.json` (Schlüsselpfad `/keys`), mappt `/health` und `/alive` |
| `ConnectionStrings__iotweltdb` | SQL Server (API und Migrator) |
| `Jwt__SigningKey` | Base64, mind. 32 Bytes (API) |
| `App__PortalBaseUrl` | Portal-Adresse für Links in Mails (API) |
| `SeedAdmin__Email` / `SeedAdmin__Password` | optional, erster Admin (API) |
| `Smtp__Host` / `Smtp__Port` / `Smtp__Security` | optional, sonst landen Mails nur im Log (API) |

Schlüssel erzeugen (Windows PowerShell 5.1):

```powershell
$b = New-Object byte[] 32; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b)
```

## Review-Checkliste für Dockerfiles

- [ ] **Basis-Images mit Tag** (`sdk:10.0`, `aspnet:10.0`), nie ohne Tag (= `latest`)
- [ ] **Restore-Layer vor dem Quellcode**: erst `*.csproj` + `Directory.Build.props`, `dotnet restore`, dann der Rest
- [ ] **Nur nötige Projekte** im Build-Kontext der Stufe kopiert (API kopiert kein Portal und umgekehrt)
- [ ] **Keine Secrets im Image**: weder `COPY` von `.env`/`secrets.json` noch `ENV`/`ARG` mit Passwörtern. Prüfen mit
      `docker history --no-trunc iotwelt-api` und `docker run --rm --entrypoint ls iotwelt-api -la /app`
- [ ] **Nicht-Root**: `USER $APP_UID` in jedem Laufzeit-Image. Prüfen: `docker run --rm --entrypoint id iotwelt-api`
- [ ] **Nur Laufzeit im Final-Image**: kein SDK, kein Quellcode, nur `publish`-Ausgabe
- [ ] **`.dockerignore` greift**: `bin/`, `obj/`, `.vs/`, `.git/`, `secrets.json`, `.env` landen nicht im Kontext
      (sichtbar an „transferring context“ in der Build-Ausgabe)
- [ ] **Schreibbare Verzeichnisse** (`/keys`) gehören dem App-User, sonst scheitert das Volume an Rechten

## Übungen

### 1 · Bauen und Größen vergleichen

```powershell
docker build -f IotWelt.API/Dockerfile -t iotwelt-api .
docker build -f IotWelt.API/Dockerfile --target migrator -t iotwelt-migrator .
docker build -f MyOit.Portal/Dockerfile -t iotwelt-portal .
docker pull mcr.microsoft.com/dotnet/sdk:10.0
docker pull mcr.microsoft.com/dotnet/aspnet:10.0
docker image ls
```

`docker image ls` zeigt die Images im lokalen Speicher von Docker (kein Ordner im Repo). **DISK USAGE** ist die
entpackte Größe auf der Platte, **CONTENT SIZE** die komprimierte, die bei `pull`/`push` übertragen wird.
Die Basis-Images erscheinen erst nach `docker pull` mit Namen, der Build legt sie nur im Build-Cache ab.

Vergleichen: SDK-Image ↔ `aspnet:10.0` ↔ `iotwelt-api` ↔ `iotwelt-migrator`. Wie viel bringt curl?
Dazu die curl-Zeilen im Dockerfile kurz auskommentieren, als `iotwelt-api:nocurl` bauen, Größen vergleichen,
Änderung wieder zurücknehmen. `docker history iotwelt-api` zeigt, welcher Layer wie groß ist.

`docker image ls` rechnet jedes Image so, als läge es allein auf der Platte. Was wirklich belegt ist, zeigt:

```powershell
docker system df -v
```

**SHARED SIZE** sind Layer, die sich mehrere Images teilen (hier `aspnet:10.0`, ca. 340 MB), **UNIQUE SIZE** nur
das, was ein Image allein mitbringt. API, Migrator und Portal teilen sich die Basis-Layer, deshalb belegen alle drei
zusammen nur gut 500 MB statt der Summe der Einzelgrößen. Darum läuft auch der Migrator auf `aspnet:10.0`
statt self-contained auf `runtime-deps:10.0`: Einzeln wäre er kaum kleiner, aber er teilte nichts mit der API.

### 2 · Layer-Cache beobachten

1. Ohne Änderung erneut bauen: alle Schritte `CACHED`.
2. Eine `.cs`-Datei der API ändern (z. B. Kommentar), neu bauen: `restore` bleibt `CACHED`, ab `COPY IotWelt.API/` läuft alles neu.
3. `IotWelt.API.csproj` ändern (z. B. Leerzeile), neu bauen: jetzt läuft auch `restore` neu.
4. Eine Datei im **Portal** ändern und die **API** bauen: alles `CACHED` (Portal ist nicht im API-Kontext der Stufe).

Änderungen danach zurücknehmen (`git restore …`).

### 3 · Von Hand ohne Compose

So wird sichtbar, was Compose in PR 2c übernimmt: Netzwerk, Reihenfolge, Variablen, Volumes.

```powershell
$sa  = 'Staging!Sql2026'          # nur für diese Übung
$jwt = '<Base64-Schlüssel von oben>'
$cs  = "Server=sql,1433;Database=IotWeltDb;User Id=sa;Password=$sa;TrustServerCertificate=True"

docker network create iotwelt

# SQL Server mit benanntem Volume (Daten überleben den Container)
docker run -d --name sql --network iotwelt `
  -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=$sa `
  -v mssql-data:/var/opt/mssql `
  mcr.microsoft.com/mssql/server:2022-latest

# ~20 s warten, bis SQL Server bereit ist (docker logs sql → "Recovery is complete"), dann Schema anlegen
docker run --rm --network iotwelt -e ConnectionStrings__iotweltdb=$cs iotwelt-migrator

# API
docker run -d --name api --network iotwelt -p 5080:8080 `
  -e ASPNETCORE_ENVIRONMENT=Staging `
  -e ConnectionStrings__iotweltdb=$cs `
  -e Jwt__SigningKey=$jwt `
  -e App__PortalBaseUrl=http://localhost:5081 `
  -v api-keys:/keys `
  iotwelt-api

# Portal (findet die API über den Container-Namen "api", siehe appsettings.Staging.json)
docker run -d --name portal --network iotwelt -p 5081:8080 `
  -e ASPNETCORE_ENVIRONMENT=Staging `
  -v portal-keys:/keys `
  iotwelt-portal
```

Prüfen:
- `curl.exe http://localhost:5080/health` → `Healthy`
- Sensordaten senden wie ein ESP32, ohne Hardware: `IotWelt.API/IotWelt.API.http` in Visual Studio öffnen,
  oben rechts im Editor die Umgebung **staging** wählen (definiert in `IotWelt.API/http-client.env.json`),
  beim Request „Sensordaten wie vom ESP32“ auf **Anforderung senden** klicken → 200.
  Mit leerem `customer_id` entsteht ein Gerät ohne Besitzer. Soll es im eigenen Dashboard erscheinen, die
  Kundenkennung aus dem Portal-Profil eintragen. Ohne VS geht es auch per PowerShell:
  ```powershell
  Invoke-RestMethod -Method Post -Uri http://localhost:5080/api/sensor -ContentType 'application/json' -Body '{"hardwareId":"TEST-HTTP-0001","customer_id":"","deviceName":"Testsensor","temperatur":21.5,"relativeFeuchte":45.0,"wasserAlarm":false}'
  ```
- `http://localhost:5081` im Browser: registrieren, der Bestätigungslink steht im Log (`docker logs api`)

Hinweis: Fehlt `--network iotwelt`, findet die API den Host `sql` nicht. Genau dieses Detail nimmt Compose ab.

Erwartete Warnungen in `docker logs` (kein Fehler):
- `XmlKeyManager[35]` (API und Portal): Die Data-Protection-Schlüssel liegen unverschlüsselt in `/keys`.
  Für Staging akzeptiert, siehe `docs/backlog.md`.
- `HttpsRedirectionMiddleware[3]` „Failed to determine the https port for redirect“ beim ersten Request:
  Staging hat nur HTTP, die Middleware leitet deshalb nicht um und lässt den Request durch.

Tipp: Mehrzeilige Befehle brechen in PowerShell, wenn nach einem Backtick noch ein Leerzeichen steht.
`docker ps -a` zeigt dann, dass der Container nie angelegt wurde. Im Zweifel den Befehl als eine Zeile eingeben.

### 4 · Container untersuchen

```powershell
docker logs -f api                          # Logs live (Strg+C beendet nur die Anzeige)
docker exec -it api bash                    # Shell im Container: id, ls /keys, env | sort
docker inspect api --format '{{json .Mounts}}'
docker stop api portal; docker start api portal
docker rm -f sql; docker run …              # SQL-Container neu (gleicher Befehl wie oben): Daten noch da?
```

Aufräumen: `docker rm -f api portal sql`, `docker network rm iotwelt`,
Volumes erst mit `docker volume rm mssql-data api-keys portal-keys` (löscht die Daten).
