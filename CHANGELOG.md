# Changelog

Alle wesentlichen Änderungen an diesem Projekt werden hier dokumentiert.
Format orientiert sich an [Keep a Changelog](https://keepachangelog.com/de/1.0.0/).
Versionierung nach [Semantic Versioning](https://semver.org/lang/de/).

---

## [Unreleased] — v2.0 im Umbau

### Added
- **Eigene Benutzerverwaltung in der API** (ASP.NET Core Identity): Registrierung mit E-Mail-Bestätigung, Login mit Sperre nach 5 Fehlversuchen, Passwort vergessen/ändern — `/api/auth/...`
- **Eigene Token-Ausgabe**: JWT (HS256, 15 min) + Refresh-Token (14 Tage, nur als SHA-256-Hash gespeichert), Rotation bei jedem Refresh; Wiederverwendung eines verbrauchten Tokens widerruft alle Sitzungen des Logins
- **Konten und Mitgliedschaften**: Registrierung legt ein Konto (`CustomerId`) mit dem Login als Owner an; Konto-Rollen Owner/Editor/Reader, Kontowechsel über `/api/auth/switch-account`
- Policies `CanRead` / `CanEdit` / `IsOwner`; Geräte löschen nur noch als Owner
- Admin-Seed beim Start aus `SeedAdmin:Email` / `SeedAdmin:Password` (User-Secrets bzw. Env-Vars)
- 17 neue Tests für Auth-Flows und Rechte-Matrix (gesamt 35); Tests melden sich mit echten Tokens an statt per Test-Header
- **`IotWelt.API.Tests`**: 18 Integrationstests (xUnit, `WebApplicationFactory`, SQL Server per Testcontainers) als Sicherheitsnetz vor dem Auth-Umbau — Sensor-Empfang (alle drei Zuordnungsmodi), Mandantentrennung bei `/api/devices`, Dashboard, Verlauf inkl. Bucket-Aggregation, Kundenprofil, Admin-Endpoints
- `docs/user-stories/benutzerverwaltung.md`: User Stories für Konto/Login/Mitgliedschaft (Owner/Editor/Reader, Einladungen, Kontowechsel)

### Changed
- **Migrationen neu aufgesetzt** (`InitialV2`) — alte Stände nur noch über Tag `v0.4.1-azure`; lokale DB neu erstellen
- `CustomerProfiles` ersetzt durch `Accounts` + `AccountMemberships`; `CustomerService` ersetzt durch `CurrentAccount` (liest das aktive Konto aus dem Token)
- `CLAUDE.md` für die v2.0-Zielarchitektur neu geschrieben (kein Azure, API als Token-Aussteller, Definition of Done, Konventionen)
- `.claude/settings.local.json` nicht mehr versioniert

### Removed
- Entra-ID-Validierung in der API (`Microsoft.Identity.Web`, Section `AzureAd`)

### Fixed
- Admin-Geräteliste: Besitzerabfrage war von EF Core nicht übersetzbar (Filter nach der Projektion in einen Record)

### Security
- Bekannt, durch Test dokumentiert: `POST /api/sensor` ist anonym — wer eine `hardwareId` kennt, kann das Gerät per `customer_id` einem anderen Kunden zuordnen. Wird in Phase 1 behoben.

---

## [0.4.1] — 2026-07-16

### Added
- `DashboardOptions` (Section `Dashboard` in `appsettings.json`): `OfflineAfterMinutes` steuert, ab wann eine Sensorkachel im Live-Dashboard als **Offline** gilt — Default **5 Minuten**

### Changed
- Live-Dashboard: Online-/Offline-Schwelle war fest auf 2 Minuten kodiert und kommt jetzt aus der Konfiguration (Bindung via `IOptions<DashboardOptions>`, Auslesen beim Start — Änderung erfordert Portal-Neustart)

### Fixed
- Versionsnummern in `IotWelt.API.csproj` und `MyOit.Portal.csproj` standen noch auf `0.3.0` — der Bump auf 0.4.0 war unterblieben; beide jetzt auf `0.4.1`

---

## [0.4.0] — 2026-07-16

### Added
- **Hardware-ID als stabile Gerätekennung**: `Device.HardwareId` (max. 14 Zeichen); ESP32 sendet `hardwareId` im Sensor-Payload
- `SensorController`: primäre Geräte-Identifikation per **HardwareId** (Auto-Create bei unbekannter ID); `CustomerId` + `DeviceName` sowie reiner `DeviceName` als Fallback-/Legacy-Modi
- **Hardware-ID-Spalte** in `/my-devices` und `/admin/devices`
- **Live-Dashboard → Verlauf**: zusätzliche Zeiträume **12 h / 24 h** sowie Kalendertag-Presets **Heute / Gestern / Letzte 7 Tage**
- **Serverseitige Aggregation** der Verlaufsdaten: Mittelung in Zeit-Buckets abhängig von der Fensterdauer (≤ 4 h roh, ≤ 12 h → 5 min, ≤ 24 h → 15 min, ≤ 48 h → 30 min, > 48 h → 60 min) — hält Payload und Chart lesbar/performant

### Changed
- `SensorController`: bei bekannter HardwareId werden `Name` und `CustomerId` ggf. an die gemeldeten Werte angeglichen
- `SensorDataDto.CustomerId` explizit als JSON-Feld `customer_id`
- `DeviceDashboardDto` + `AdminDeviceDto`: um `HardwareId` erweitert; `DevicesController.GetDashboard` liefert HardwareId mit
- `RaumKlimaLogController.GetVerlauf`: neuer `range`-Parameter (Kalendertage) + Bucket-Aggregation
- `IotWeltApiClient.GetKlimaVerlaufAsync`: optionaler `range`-Parameter; Verlauf-Achse zeigt bei tagesübergreifenden Zeiträumen zusätzlich das Datum

### Fixed
- `/my-devices`: Spalte „Geräte-Nr." zeigte den DTO-Typnamen statt eines Werts — ersetzt durch „Hardware ID" (Feld fehlte im `DeviceDashboardDto`)

### Migrations
- `20260711133147_AddHardwareIdToDevice` — `Devices.HardwareId nvarchar(14) NULL`

---

## [0.3.0] — 2026-07-11

### Added
- **Geräteverwaltung** (`/my-devices`): Nutzer können eigene Geräte bearbeiten (Name, Standort, Caption) und löschen — löscht Logs kaskadierend
- **Admin: Geräteverwaltung** (`/admin/devices`): alle Geräte mit Server-Paging (10/Seite), Filter nach Kunden-ID, Inline-Edit inkl. Kunden-ID-Zuweisung
- **Admin: Benutzerverwaltung** (`/admin/users`): alle registrierten Nutzer mit Kunden-ID, Admin-Rolle per Toggle zuweisen/entziehen, Nutzer löschen (CIAM + DB)
- **App-Rolle „Admin"** in Entra External ID: steuert Sichtbarkeit der Admin-Seiten und API-Zugriff
- `Device.Caption` — optionales Beschreibungsfeld (max. 200 Zeichen) für Geräte
- `CustomerProfile.DisplayName` + `CustomerProfile.Email` — werden automatisch beim Login aus JWT-Claims befüllt
- `AdminController` (`/api/admin/*`): paginierte Geräteliste, Admin-CRUD ohne Ownership-Filter, Kunden-Übersicht, Kunden-Löschung mit Kaskade
- Neue gemeinsame DTOs: `AdminDeviceDto`, `CustomerProfileDto`, `PagedResult<T>`, `DeviceUpdateDto`, `AdminDeviceUpdateDto`

### Changed
- `GraphUserService`: nutzt jetzt App-Token (Client Credentials, `GetAccessTokenForAppAsync`) statt delegiertem Token — delegierte Graph-Berechtigungen funktionieren für CIAM Directory-Ops nicht
- `GraphUserService` in DI registriert (`AddScoped`)
- `DevicesController.PUT`: nimmt jetzt `DeviceUpdateDto` statt Entity-Objekt
- `DevicesController.GetDashboard`: liefert jetzt Caption + CustomerId im DTO
- `CustomerService.EnsureCustomerIdAsync`: speichert DisplayName + Email bei jedem Login
- NavMenu: „Meine Geräte" für alle Nutzer; Admin-Links nur für `Roles="Admin"`

### Migrations
- `20260711103837_AddCaptionAndOwnerInfo` — `Devices.Caption nvarchar(200)`, `CustomerProfiles.DisplayName nvarchar(200)`, `CustomerProfiles.Email nvarchar(200)`

---

## [0.2.0] — 2026-07-10

### Added
- **Entra External ID (CIAM)** als Identity Provider — Selbstregistrierung mit E-Mail/Passwort, kein Microsoft-Konto nötig
- **CustomerProfile-Tabelle** in der API-Datenbank: bildet den JWT-Claim `oid` auf eine 16-stellige alphanumerische `CustomerId` ab
- `CustomerService`: legt CustomerProfile automatisch beim ersten API-Aufruf an
- `CustomersController` (`GET /api/customers/me`): gibt die CustomerId des angemeldeten Users zurück
- **Profilseite** (`/account/profile`) im Portal: zeigt Name, E-Mail und CustomerId mit Kopier-Button
- `CustomerId`-Feld in der `Devices`-Tabelle: jedes Gerät gehört jetzt einem Benutzer
- `customer_id` im Sensor-Payload: ESP32-Geräte identifizieren sich über die CustomerId statt OAuth2
- Legacy-Modus im SensorController: Geräte ohne CustomerId laufen weiter per DeviceName

### Changed
- Portal-Auth von Workforce-Tenant auf Entra External ID umgestellt (`Authority: ciamlogin.com`)
- API-JWT-Validierung: `AddJwtBearer` mit explizitem Metadata-Endpoint (GUID-Subdomain) + `UseSecurityTokenValidators = true`
- `DevicesController`, `RaumKlimaLogController`: alle Endpunkte filtern nach CustomerId des angemeldeten Users
- `SensorController`: `[AllowAnonymous]` statt OAuth2 Client-Credentials-Policy
- NavMenu: Login/Logout-Links und Dashboard nur für angemeldete Nutzer sichtbar
- Profilseite: `prerender: false` verhindert MSAL-Fehler beim ersten Aufruf

### Migrations
- `20260709120516_AddCustomerIdToDevice` — `Devices.CustomerId nvarchar(16) NULL`
- `20260709212632_AddCustomerProfile` — neue Tabelle `CustomerProfiles (Id, OwnerId, CustomerId)`

---

## [0.1.0] — 2026-06-29

### Added
- .NET Aspire Orchestrierung (`IotWelt.AppHost`, `IotWelt.ServiceDefaults`)
- Blazor Server Portal (`MyOit.Portal`) mit Entra Workforce-Tenant Auth (MS Entra B2B, Einladungs-Flow)
- ASP.NET Core Web API (`IotWelt.API`) mit JWT-Bearer-Auth gegen Workforce-Tenant
- EF Core + SQL Server LocalDB für Gerätedaten
- `Devices`-Tabelle: CRUD-Endpunkte (`GET/POST/PUT/DELETE /api/devices`)
- `RaumKlimaLog`-Tabelle: Verlaufsdaten pro Gerät
- ESP32-Integration: `POST /api/sensor` empfängt Temperatur, Feuchte, WasserAlarm
- OAuth2 Client-Credentials-Flow für ESP32-Geräte
- Scalar API-Dokumentation (`/scalar/v1`)
- `IotWelt.Common`: gemeinsame DTOs (`DeviceDashboardDto`, `KlimaVerlaufPunkt`)
- Live-Dashboard im Portal mit automatischer Aktualisierung
- Benutzerverwaltung über Microsoft Graph (B2B-Einladungen, Rollen)
- Azure Deployment: App Service (Portal + API), Azure SQL

### Migrations
- `20260627123535_InitialCreate`
- `20260627130112_AddDevice`
- `20260627141619_UpdateDevice`
- `20260629112143_AddRaumKlimaLog`
