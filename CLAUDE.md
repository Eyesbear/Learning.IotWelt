# IotWelt — Lern- und Demoprojekt (v2.0 im Umbau)

## Zweck
IoT-Monitoring-Lösung (ESP32-Klimasensoren → Web API → Blazor-Portal, später MAUI-App).
Dient als Übungsfeld für Git, Docker, CI/CD, Deployment, SQL-Tuning und Agentic Coding
(siehe `ZZZ_Learning/`) und soll zugleich praktisch nutzbar und vorzeigbar sein.

**Rolle von Claude:** Trainer (erklären, Aufgaben stellen, reviewen) und Produktentwickler
(Architektur vorschlagen, Code in kleinen prüfbaren Commits). Thorsten führt Infrastruktur-,
Git- und VS-Schritte meist selbst aus — erst erklären, dann auf Rückmeldung warten.

## Zielarchitektur v2.0 (verbindlich)
- **Kein Azure mehr.** Keine Entra ID, kein Microsoft Graph, keine Azure-SDKs neu einführen.
- **API ist der Token-Aussteller:** ASP.NET Core Identity in `IotWelt.API`, eigene JWTs +
  rotierende Refresh-Tokens. Portal und MAUI-Client sind reine Clients der API.
- **Mandantentrennung:** jeder Datenzugriff in der API filtert über `CustomerService` auf die
  `CustomerId` des angemeldeten Users. Admin-Endpoints nur mit Rolle `Admin`.
- **Konto vs. Login:** Ein Konto (Mandant, `CustomerId`) gehört einem Eigentümer, kann aber
  **mehrere Logins** haben (z. B. lesender Zugriff für Dritte, ohne Credentials weiterzugeben).
  Rechte hängen daher an der Mitgliedschaft *Login ↔ Konto* (Konto-Rolle), nicht am Login selbst.
  Die System-Rolle `Admin` ist davon getrennt. Details/User Stories: offen, werden vor Phase 1
  in `docs/user-stories/benutzerverwaltung.md` festgelegt.
- **Umgebungen:** lokal = Aspire (F5) · Staging = Docker Compose im LAN · Produktion = myASP.NET (IIS, kein Docker).
- **Konfiguration:** keine Secrets in `appsettings*.json` — lokal User-Secrets, sonst Env-Vars / `.env`.

## Solution-Struktur
| Projekt | Typ | Zweck |
|---|---|---|
| `IotWelt.AppHost` | Aspire AppHost | Lokale Orchestrierung (API + Portal) |
| `IotWelt.ServiceDefaults` | Class Library | Health-Checks, OpenTelemetry, Service Discovery |
| `IotWelt.API` | ASP.NET Core Web API | REST-API, EF Core, (künftig) Identity + Token-Ausgabe |
| `MyOit.Portal` | Blazor Server (Radzen) | Web-Frontend, ruft nur die API auf |
| `IotWelt.Common` | Class Library | DTOs, die API und Clients teilen |

Geplant: `IotWelt.API.Tests` (xUnit + WebApplicationFactory), `IotWelt.Maui` (Android/Windows).

## Bauen, Testen, Starten
```powershell
dotnet build IotWelt.slnx
dotnet test IotWelt.slnx          # sobald das Testprojekt existiert
dotnet run --project IotWelt.AppHost
```
- Startprojekt in VS: `IotWelt.AppHost`; Aspire-Dashboard öffnet sich automatisch.
- API lokal fest auf `http://localhost:5013` (wegen ESP32 im LAN auch `0.0.0.0:5013`).
- Scalar-UI (nur Development): `/scalar/v1`. Manuelle Requests: `IotWelt.API/IotWelt.API.http`.

**Definition of Done für Code-Änderungen:** `dotnet build` ohne neue Warnungen, `dotnet test` grün,
betroffene Seite/Endpoint einmal real ausprobiert, `CHANGELOG.md` ergänzt.

## Datenbank
- Lokal: SQL Server LocalDB (`MSSQLLocalDB`), Connection-String `iotweltdb` in
  `IotWelt.API/appsettings.Development.json`.
- DbContext: `IotWelt.API/Data/AppDbContext.cs`. Nur die API greift auf die DB zu.
- Migrations immer mit expliziten Flags (Package Manager Console):
  ```
  Add-Migration <Name> -Project IotWelt.API -StartupProject IotWelt.API
  Update-Database -Project IotWelt.API -StartupProject IotWelt.API
  ```
  CLI-Variante: `dotnet ef migrations add <Name> --project IotWelt.API --startup-project IotWelt.API`
- Produktion: Migrationen per idempotentem Skript (`dotnet ef migrations script --idempotent`).

## API-Überblick
- `GET/POST/PUT/DELETE /api/devices`, `GET /api/devices/dashboard` — Geräte des eigenen Mandanten
- `GET /api/raumklimalog/{deviceId}` — Verlaufsdaten (serverseitig aggregiert)
- `GET /api/customers/me` — eigenes Kundenprofil
- `/api/admin/...` — Geräte und Kunden aller Mandanten (Rolle `Admin`)
- `POST /api/sensor` — anonym, Sensordaten vom ESP32

## ESP32-Integration
- Firmware: `D:\WORK\VS_Development\ComplexProjects\ESP32Configuration_Complex\ESP32_KlimaSensor`
- Payload an `POST /api/sensor`:
  `{ "hardwareId": "...", "customerId": "...", "deviceName": "...", "temperatur": 21.5, "relativeFeuchte": 45.0, "wasserAlarm": false }`
- Gerät wird primär über `hardwareId` erkannt (Auto-Create), `customerId`/`deviceName` sind Fallback.
- Der Sensor-Endpoint muss abwärtskompatibel bleiben, solange die Firmware nicht umgestellt ist.

## Konventionen
- Sprache: Code/Bezeichner Englisch oder bestehende deutsche Fachbegriffe (`Temperatur`, `RaumKlimaLog`)
  beibehalten; Kommentare, Commits, CHANGELOG auf Deutsch.
- Git: `master` nur über Pull Requests; Branches `feature/…`, `fix/…`, `chore/…`.
  Commit-Nachrichten im Stil `feat: …`, `fix: …`, `chore: …`, `docs: …`, `test: …`.
- Versionierung: SemVer, Version in den csproj-Dateien, Einträge in `CHANGELOG.md` (Keep a Changelog).
- Kleine, einzeln prüfbare Commits; keine kommentarlosen Großumbauten.

## Aktueller Stand / Roadmap
Plan: Phase 0 Fundament (Git, CLAUDE.md, Tests) → 1 eigene Benutzerverwaltung (Azure-Ausbau) →
2 Docker/Compose-Staging → 3 GitHub Actions → 4 myASP.NET-Produktion → 5 MAUI → 6 SQL-Tuning, ESP32, Zeitzonen.

**Übergangszustand:** Der Code enthält noch Entra-ID-Auth (`Microsoft.Identity.Web`, `AzureAd`-Sections,
`GraphUserService`). Diese Teile werden in Phase 1 vollständig ersetzt — nicht weiter ausbauen.
Letzter Azure-Stand: Git-Tag `v0.4.1-azure`. `MIGRATION_SPEC_V2.0.md` ist nur noch für myASP.NET-Details relevant.

Lernjournal: `ZZZ_Learning/Lernjournal.md` — nach jeder Phase ergänzen.
