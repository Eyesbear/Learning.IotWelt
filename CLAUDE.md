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
- **Mandantentrennung:** jeder Datenzugriff in der API filtert über `CurrentAccount` auf die
  `CustomerId` des aktiven Kontos (Claim `account_id`). Admin-Endpoints nur mit Rolle `Admin`.
- **Konto vs. Login:** Ein Konto (Mandant, `CustomerId`) gehört einem Eigentümer, kann aber
  **mehrere Logins** haben (z. B. lesender Zugriff für Dritte, ohne Credentials weiterzugeben).
  Rechte hängen daher an der Mitgliedschaft *Login ↔ Konto* (Konto-Rolle), nicht am Login selbst.
  Die System-Rolle `Admin` ist davon getrennt. User Stories: `docs/user-stories/benutzerverwaltung.md`.
- **Umgebungen:** lokal = Aspire (F5) · Staging = Docker Compose im LAN · Produktion = myASP.NET (IIS, kein Docker).
- **Konfiguration:** keine Secrets in `appsettings*.json` — lokal User-Secrets, sonst Env-Vars / `.env`.

## Solution-Struktur
| Projekt | Typ | Zweck |
|---|---|---|
| `IotWelt.AppHost` | Aspire AppHost | Lokale Orchestrierung (API + Portal) |
| `IotWelt.ServiceDefaults` | Class Library | Health-Checks, OpenTelemetry, Service Discovery |
| `IotWelt.API` | ASP.NET Core Web API | REST-API, EF Core, Identity + Token-Ausgabe |
| `MyOit.Portal` | Blazor Server (Radzen) | Web-Frontend, ruft nur die API auf |
| `IotWelt.Common` | Class Library | DTOs, die API und Clients teilen |
| `IotWelt.API.Tests` | xUnit | Integrationstests: `WebApplicationFactory` + SQL Server per Testcontainers, angemeldet wird mit echten Tokens (`factory.CreateUserAsync()` in `Infrastructure/TestUsers.cs`) |

Geplant: `IotWelt.Maui` (Android/Windows).

## Auth-Architektur
- **API** (`IotWelt.API`): `AddIdentityCore<AppUser>` ohne Cookies/UI. `TokenService` stellt JWT (HS256, 15 min,
  Claims `sub`, `name`, `email`, `role`, `account_id`, `account_role`) und Refresh-Token (14 Tage, nur als Hash
  gespeichert, bei jedem Refresh rotiert) aus. Ein zweimal vorgelegtes Refresh-Token widerruft **alle**
  Sitzungen des Logins. Rollen/Konto im Token wirken erst nach dem nächsten Refresh.
- **Rechte:** Policies `CanRead`/`CanEdit`/`IsOwner` (Konto-Rolle) und Rolle `Admin` (`Services/Policies.cs`);
  `CurrentAccount` liefert `UserId`/`CustomerId` des aktiven Kontos.
- **Portal** (`MyOit.Portal/Services/Auth`): Cookie enthält nur die Sitzungs-ID (`portal_sid`), die Tokens liegen
  serverseitig im `ITokenStore` (derzeit im Speicher). `TokenSessionManager` erneuert und serialisiert alles,
  was das Refresh-Token verbraucht (Refresh, Logout, Kontowechsel, Passwortänderung) pro Sitzung — neue
  Token-Verwendungen nur über ihn. `BearerTokenHandler` hängt das Token an `IotWeltApiClient`.
- **Cookie ändern** geht nur außerhalb des Circuits: Seiten mit Anmeldung/Cookie-Erneuerung sind statisches SSR
  (`[ExcludeFromInteractiveRouting]`); interaktive Seiten nutzen Formular-POSTs an `/account/*`
  (`AccountEndpoints.cs`, Antiforgery-Pflicht per Endpoint-Filter — `UseAntiforgery()` allein blockiert nicht).
- **401 von der API** → Seite ruft `Navigation.NavigateToLogin()` (Rücksprung auf die Seite).
- **Secrets:** `Jwt:SigningKey` (Base64, ≥ 32 Bytes) lokal als AppHost-Parameter, `SeedAdmin:Email`/`Password`
  in den User-Secrets der API. Mails (Bestätigung, Reset, Einladung) werden derzeit nur geloggt (`LoggingEmailSender`).

## Bauen, Testen, Starten
```powershell
dotnet build IotWelt.slnx
dotnet test IotWelt.slnx          # braucht laufendes Docker (Testcontainers startet SQL Server)
dotnet run --project IotWelt.AppHost
```
- Startprojekt in VS: `IotWelt.AppHost`; Aspire-Dashboard öffnet sich automatisch.
- JWT-Schlüssel einmalig setzen (sonst fragt das Dashboard danach):
  `dotnet user-secrets set "Parameters:jwt-signing-key" <base64> --project IotWelt.AppHost`
- API lokal fest auf `http://localhost:5013` (wegen ESP32 im LAN auch `0.0.0.0:5013`).
- Scalar-UI (nur Development): `/scalar/v1`. Manuelle Requests: `IotWelt.API/IotWelt.API.http`.

**Build-Hook:** `.claude/settings.json` → `.claude/hooks/build-check.sh` baut die Solution, bevor Claude eine
Antwort beendet (nur wenn sich `.cs/.razor/.csproj/.slnx/.props` geändert haben). Build-Fehler zwingen Claude
zur Korrektur; gesperrte DLLs (App läuft in VS) werden nur als Hinweis gemeldet.

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
- `GET /api/customers/me` — Kundenkennung des aktiven Kontos
- `/api/auth/...` — Registrierung, Login, Refresh, Logout, Kontowechsel, Passwort, eigenen Login löschen
- `/api/members/...` — Mitglieder und Einladungen des aktiven Kontos (Owner), `/api/invitations/{token}/...` — annehmen
- `/api/admin/devices`, `/api/admin/logins` — Geräte und Logins aller Mandanten (Rolle `Admin`)
- `POST /api/sensor` — anonym, Sensordaten vom ESP32

## ESP32-Integration
- Firmware: `D:\WORK\VS_Development\ComplexProjects\ESP32Configuration_Complex\ESP32_KlimaSensor`
- Payload an `POST /api/sensor`:
  `{ "hardwareId": "...", "customer_id": "...", "deviceName": "...", "temperatur": 21.5, "relativeFeuchte": 45.0, "wasserAlarm": false }`
  **Achtung:** `customer_id` mit Unterstrich (`[JsonPropertyName]` in `SensorDataDto`), alle anderen Felder camelCase.
- Gerät wird primär über `hardwareId` erkannt (Auto-Create), `customer_id`/`deviceName` sind Fallback.
- Ein Gerät mit Besitzer wird über den Sensor-Endpoint **nie umgehängt** (nur Erstzuordnung herrenloser Geräte).
- Der Sensor-Endpoint muss abwärtskompatibel bleiben, solange die Firmware nicht umgestellt ist.

## Konventionen
- Sprache: Code/Bezeichner Englisch oder bestehende deutsche Fachbegriffe (`Temperatur`, `RaumKlimaLog`)
  beibehalten; Kommentare, Commits, CHANGELOG auf Deutsch.
- Git: `master` nur über Pull Requests; Branches `feature/…`, `fix/…`, `chore/…`.
  Commit-Nachrichten im Stil `feat: …`, `fix: …`, `chore: …`, `docs: …`, `test: …`.
- Versionierung: SemVer, Version zentral in `Directory.Build.props` (`VersionPrefix`, nicht in den csproj-Dateien), Einträge in `CHANGELOG.md` (Keep a Changelog).
- Kleine, einzeln prüfbare Commits; keine kommentarlosen Großumbauten.

## Aktueller Stand / Roadmap
Plan: Phase 0 Fundament (Git, CLAUDE.md, Tests) → 1 eigene Benutzerverwaltung (Azure-Ausbau) →
2 Docker/Compose-Staging → 3 GitHub Actions → 4 myASP.NET-Produktion → 5 MAUI → 6 SQL-Tuning, ESP32, Zeitzonen.

**Stand:** Phase 0 und 1 abgeschlossen (v0.5.0, eigene Benutzerverwaltung, kein Azure mehr im Code).
Als Nächstes Phase 2 (Docker/Compose-Staging). Letzter Azure-Stand: Git-Tag `v0.4.1-azure`.
`MIGRATION_SPEC_V2.0.md`: umgesetzte Auth-Architektur und offene Punkte für myASP.NET (Phase 4).

Lernjournal: `ZZZ_Learning/Lernjournal.md` — nach jeder Phase ergänzen.
Bekannte Lücken und Folgepunkte: `docs/backlog.md` — neue Funde dort eintragen statt sie nur zu erwähnen.
