# Changelog

Alle wesentlichen Änderungen an diesem Projekt werden hier dokumentiert.
Format orientiert sich an [Keep a Changelog](https://keepachangelog.com/de/1.0.0/).
Versionierung nach [Semantic Versioning](https://semver.org/lang/de/).

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
