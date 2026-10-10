# Changelog

Alle wesentlichen Änderungen an diesem Projekt werden hier dokumentiert.
Format orientiert sich an [Keep a Changelog](https://keepachangelog.com/de/1.0.0/).
Versionierung nach [Semantic Versioning](https://semver.org/lang/de/).

---

## [Unreleased] — v2.0 im Umbau

### Added
- Portal-Seite **Einladung annehmen** (`/account/accept-invitation`, Ziel des Einladungslinks): neue Person legt direkt einen Login an und ist angemeldet (B2); bestehender Login meldet sich an und nimmt an, danach wechselt das Portal gleich in das neue Konto (B3). Abgelaufene, schon angenommene und fremde Einladungen (anderer Login angemeldet) werden erklärt
- Portal-Seite **Mitglieder** (`/account/members`, nur für den Owner, Menüeintrag nur für Owner): Personen per E-Mail als Leser/Bearbeiter einladen, offene Einladungen zurückziehen oder abgelaufene erneut senden, Rolle ändern, Mitglied entfernen, Eigentümerschaft übertragen. Die Übertragung läuft als Formular-POST (`/account/transfer-ownership`, mit Antiforgery-Schutz) und erneuert danach sofort Tokens und Cookie
- **Passwort ändern** im Portal-Profil (Story A3): Die aktuelle Sitzung bleibt angemeldet und bekommt neue Tokens, alle anderen Sitzungen des Logins werden beendet; der Tausch läuft unter der Sitzungssperre, damit kein paralleler Refresh das widerrufene Token vorlegt
- Portal-Seite **Konto und Login löschen** (`/account/delete`): eigenes aktives Konto mit Bestätigung löschen (C4), danach den Login mit Passwortbestätigung (A4); weitere eigene Konten werden aufgelistet. Nach dem Löschen eines Kontos wird die Sitzung sofort erneuert, damit Token und Cookie nicht mehr auf das gelöschte Konto zeigen
- **Portal meldet sich an der eigenen API an** (Cookie statt Entra ID): Login, Logout, „Angemeldet bleiben“; im Cookie steht nur eine Sitzungs-ID, Access- und Refresh-Token bleiben serverseitig im Portal und werden vor Ablauf automatisch erneuert (pro Sitzung serialisiert, damit kein Refresh-Token doppelt verwendet wird)
- Portal-Seiten für **Registrierung mit E-Mail-Bestätigung** und **Passwort vergessen/zurücksetzen** (`/account/...`); Identity-Fehler der API erscheinen auf Deutsch
- **Kontowechsler** im Kopfbereich des Portals, sichtbar bei Logins mit mehreren Konten
- Abgelaufene oder widerrufene Anmeldung führt in Dashboard, Geräten, Gerätedetail, Klimaverlauf, Profil und den Admin-Seiten zurück zum Login (mit Rücksprung auf die Seite)
- **Einladungen** (Stories B1–B3): Owner lädt per E-Mail als Editor/Reader ein (`POST /api/members/invitations`), Link 7 Tage gültig und nur einmal nutzbar, Token nur als SHA-256-Hash gespeichert. Annahme mit vorhandenem Login (`POST /api/invitations/{token}/accept`, E-Mail muss passen) oder mit neuem Login ohne eigenes Konto (`POST /api/invitations/{token}/register`, liefert direkt ein Token-Paar)
- **Mitgliederverwaltung** für Owner (`/api/members`, Stories C1–C5): Mitglieder und offene Einladungen anzeigen, Rolle ändern, Mitglied entfernen, Ownership übertragen, Konto löschen (mit Geräten, Messwerten, Mitgliedschaften, Einladungen)
- **Eigenen Login löschen** (`DELETE /api/auth/me`, Story A4) mit Passwortbestätigung; abgelehnt, solange der Login Owner eines Kontos ist
- **Login-Verwaltung für System-Admins** (`/api/admin/logins`, Stories E1–E3): Logins mit Konten und Rollen auflisten, sperren/entsperren (widerruft alle Refresh-Tokens), Admin-Rolle vergeben/entziehen, Login löschen; nie am eigenen Login
- Tabelle `AccountInvitations` und gefilterter eindeutiger Index `IX_AccountMemberships_OneOwnerPerAccount` (genau ein Owner pro Konto, auch auf DB-Ebene) — Migrationen `AccountInvitations`, `OneOwnerPerAccount`
- 55 neue Tests für Einladungen, Mitglieder, Login-Löschung und Admin-Login-Verwaltung (gesamt 91); Test-Helfer `InviteAndAcceptAsync` und `RefreshAsync`
- **Eigene Benutzerverwaltung in der API** (ASP.NET Core Identity): Registrierung mit E-Mail-Bestätigung, Login mit Sperre nach 5 Fehlversuchen, Passwort vergessen/ändern — `/api/auth/...`
- **Eigene Token-Ausgabe**: JWT (HS256, 15 min) + Refresh-Token (14 Tage, nur als SHA-256-Hash gespeichert), Rotation bei jedem Refresh; Wiederverwendung eines verbrauchten Tokens widerruft alle Sitzungen des Logins
- **Konten und Mitgliedschaften**: Registrierung legt ein Konto (`CustomerId`) mit dem Login als Owner an; Konto-Rollen Owner/Editor/Reader, Kontowechsel über `/api/auth/switch-account`
- Policies `CanRead` / `CanEdit` / `IsOwner`; Geräte löschen nur noch als Owner
- Admin-Seed beim Start aus `SeedAdmin:Email` / `SeedAdmin:Password` (User-Secrets bzw. Env-Vars)
- 17 neue Tests für Auth-Flows und Rechte-Matrix (gesamt 35); Tests melden sich mit echten Tokens an statt per Test-Header
- **`IotWelt.API.Tests`**: 18 Integrationstests (xUnit, `WebApplicationFactory`, SQL Server per Testcontainers) als Sicherheitsnetz vor dem Auth-Umbau — Sensor-Empfang (alle drei Zuordnungsmodi), Mandantentrennung bei `/api/devices`, Dashboard, Verlauf inkl. Bucket-Aggregation, Kundenprofil, Admin-Endpoints
- `docs/user-stories/benutzerverwaltung.md`: User Stories für Konto/Login/Mitgliedschaft (Owner/Editor/Reader, Einladungen, Kontowechsel)

### Changed
- **JWT-Signaturschlüssel kommt lokal aus dem AppHost**: Aspire-Parameter `jwt-signing-key` (geheim, User-Secrets des AppHost unter `Parameters:jwt-signing-key`), an die API als `Jwt__SigningKey` weitergereicht — derselbe Weg wie später per Env-Var in Compose; das Portal bekommt keine Datenbank-Referenz mehr
- **Admin-Benutzerverwaltung** im Portal (`/admin/users`) arbeitet auf den Logins statt auf Microsoft Graph (Stories E1–E3): Logins mit Konten, Rollen und Status, sperren/entsperren, Admin-Rolle vergeben/entziehen, Konten eines Eigentümers löschen, Login löschen (erst wenn er keine Konten mehr besitzt); am eigenen Login sind diese Aktionen ausgeblendet. Abgelaufene Anmeldung führt zum Login
- Token-Erzeugung und -Hash aus `TokenService` nach `SecureToken` ausgelagert (gemeinsam für Refresh-Tokens und Einladungen); Löschlogik für Konten nach `AccountService.DeleteAsync` (genutzt von C4 und dem Admin-Löschen der Konten eines Logins, Verhalten unverändert)
- **Migrationen neu aufgesetzt** (`InitialV2`) — alte Stände nur noch über Tag `v0.4.1-azure`; lokale DB neu erstellen
- `CustomerProfiles` ersetzt durch `Accounts` + `AccountMemberships`; `CustomerService` ersetzt durch `CurrentAccount` (liest das aktive Konto aus dem Token)
- `CLAUDE.md` beschreibt die umgesetzte Auth-Architektur statt des Übergangszustands; `MIGRATION_SPEC_V2.0.md` an die tatsächliche Umsetzung angepasst (API als Token-Aussteller, Env-Vars statt Secrets in `appsettings.Production.json`, offene Punkte für myASP.NET)
- `CLAUDE.md` für die v2.0-Zielarchitektur neu geschrieben (kein Azure, API als Token-Aussteller, Definition of Done, Konventionen)
- `.claude/settings.local.json` nicht mehr versioniert

### Removed
- `GET /api/admin/customers` und `CustomerProfileDto` (ungenutzt seit der Login-Verwaltung); das Löschen der Konten eines Logins heißt jetzt `DELETE /api/admin/logins/{userId}/accounts` statt `DELETE /api/admin/customers/{ownerId}`
- Entra-ID-Anmeldung und Microsoft Graph im Portal (`Microsoft.Identity.Web(.UI)`, `Microsoft.Graph`, `GraphUserService`, Section `AzureAd`, `IotWeltApi:Scopes`, Testseite `/auth`) sowie ungenutzte EF-Core-Pakete im Portal
- Demoseiten Counter und Weather aus der Projektvorlage
- Entra-ID-Validierung in der API (`Microsoft.Identity.Web`, Section `AzureAd`)

### Fixed
- Portal: Ein Fehler beim Laden des Klimaverlaufs (API nicht erreichbar, Sitzung abgelaufen) beendete den ganzen Circuit; jetzt erscheint ein Hinweis bzw. der Login
- Admin-Geräteliste: Besitzerabfrage war von EF Core nicht übersetzbar (Filter nach der Projektion in einen Record)

### Security
- Portal: Formular-Endpoints unter `/account` (Logout, Kontowechsel) lehnen Anfragen ohne gültiges Antiforgery-Token ab (400) — `UseAntiforgery()` prüft nur und blockiert selbst nicht; eine fremde Seite hätte sonst per Auto-Submit abmelden oder das Konto wechseln können
- `POST /api/sensor` hängt ein Gerät mit vorhandenem Besitzer nicht mehr um — bisher konnte jeder, der die `hardwareId` kannte, das Gerät per `customer_id` in ein fremdes Konto holen. Abweichende `customer_id` wird ignoriert und als Warnung geloggt; herrenlose Geräte werden weiterhin beim ersten Melden zugeordnet
- Bekannt, offen bis Phase 6 (Geräte-Schlüssel): Wer eine `hardwareId` kennt, kann weiterhin Messwerte einschleusen; wer eine `customer_id` kennt, kann neue Geräte in dieses Konto melden

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
