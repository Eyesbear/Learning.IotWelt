# IotWelt – Migration zu Version 2.0
## Von Azure/Entra ID zu eigener Benutzerverwaltung und myASP.NET

**Autor:** Thorsten
**Ursprung:** September 2026 (Spec für die Umsetzung mit Claude Code)
**Stand:** Oktober 2026, nach Phase 1 (v0.5.0) an die tatsächliche Umsetzung angepasst
**Zielplattform Produktion:** myASP.NET (Windows Server, IIS, SQL Server) — Phase 4

> Die ursprüngliche Spec sah Cookie-Auth mit Identity-Scaffolding direkt im Blazor-Portal vor.
> Umgesetzt wurde stattdessen: **Die API ist der Token-Aussteller**, Portal und (künftig) MAUI-App
> sind reine Clients. Grund: Die MAUI-App braucht dieselbe Anmeldung, und nur die API greift auf die DB zu.
> Verbindliche Architekturregeln stehen in `CLAUDE.md`, die fachlichen Regeln in
> `docs/user-stories/benutzerverwaltung.md`.

---

## 📋 Executive Summary

v1.0 lief in Azure (App Service, Entra ID / External ID, Microsoft Graph). Letzter Stand: Git-Tag `v0.4.1-azure`.
v2.0 kommt ohne Azure aus: ASP.NET Core Identity in der API, eigene JWTs mit rotierenden Refresh-Tokens,
lokal Aspire, Staging per Docker Compose im LAN, Produktion bei myASP.NET. Ziel: keine Azure-Kosten,
gleiche Funktion, plus Mehrbenutzer-Konten (Owner/Editor/Reader).

---

## 🎯 Anforderungen & Constraints

### Funktionale Anforderungen
- [x] Blazor Server Portal (Radzen), ruft nur die API auf
- [x] ASP.NET Core Web API mit eigener Token-Ausgabe
- [x] Benutzerverwaltung mit ASP.NET Core Identity (Registrierung, E-Mail-Bestätigung, Passwort vergessen/ändern)
- [x] Konten mit mehreren Logins: Einladungen, Konto-Rollen, Kontowechsel, Eigentümerschaft übertragen
- [x] Login-Verwaltung für System-Admins (sperren, Admin-Rolle, löschen)
- [x] Entity Framework Core, SQL Server
- [ ] MAUI-App (Android/Windows) als zweiter Client — Phase 5

### Nicht-funktionale Anforderungen
- Domain: **teqpool.de**, Subdomain **things.teqpool.net** (von Strato auf myASP.NET umgeleitet)
- Hosting: myASP.NET, DB: SQL Server bei myASP.NET
- .NET 10
- Nur Demodaten: frische Datenbank, keine Übernahme der Entra-Benutzer

### Constraints & Limitations
- ❌ Aufgegeben: MFA, Conditional Access, Single Sign-On mit Microsoft-Konto
- ❌ Kein Key Vault — Secrets **nie** in `appsettings*.json`: lokal User-Secrets, sonst Env-Vars / `.env`
- ⚠️ Benutzer registrieren sich neu (kein Import)

---

## 🔄 Architektur – Vorher vs. Nachher

### v1.0 (Azure/Entra ID)
```
Portal (Azure App Service) ──OIDC──► Entra ID / External ID
   │  Access-Token (Entra)              ▲
   ▼                                    │ Microsoft Graph (Benutzerverwaltung)
Web API (Azure App Service) ────────────┘
   │
   ▼
SQL Server (Azure)
```

### v2.0 (umgesetzt)
```
┌────────────────────────────┐        ┌──────────────────────┐
│ MyOit.Portal (Blazor)      │        │ IotWelt.Maui (Ph. 5) │
│ Cookie: nur Sitzungs-ID    │        │ Tokens im SecureStore│
│ Tokens serverseitig im RAM │        └──────────┬───────────┘
└─────────────┬──────────────┘                   │
              │ Bearer-JWT (+ Refresh)           │
              ▼                                  ▼
┌──────────────────────────────────────────────────────────┐
│ IotWelt.API                                              │
│ ├─ ASP.NET Core Identity (AddIdentityCore, ohne UI)      │
│ ├─ /api/auth: Login, Refresh, Logout, Registrierung …    │
│ ├─ JWT HS256 (15 min) + Refresh-Token (14 Tage, Rotation)│
│ └─ Mandantentrennung über Claim account_id               │
└─────────────┬────────────────────────────────────────────┘
              ▼                         ▲
┌──────────────────────────┐            │ POST /api/sensor (anonym)
│ SQL Server               │       ESP32-Klimasensoren
│ AspNet*-Tabellen + App   │
└──────────────────────────┘
```

---

## 🔐 Authentifizierung – Umsetzung

### API (`IotWelt.API`)
- `AddIdentityCore<AppUser>` + Rollen + EF-Store, **keine Cookies, keine UI** (`Program.cs`).
  Passwort mind. 8 Zeichen mit Groß-/Kleinbuchstaben und Ziffer, E-Mail-Bestätigung Pflicht,
  Sperre nach 5 Fehlversuchen für 15 min.
- `TokenService` stellt aus: **Access-Token** JWT HS256, 15 min, Claims `sub`, `name`, `email`, `role`,
  `account_id`, `account_role`. **Refresh-Token** 14 Tage, nur als SHA-256-Hash in `RefreshTokens`,
  bei jedem Refresh rotiert; die Wiederverwendung eines verbrauchten Tokens widerruft alle Sitzungen des Logins.
- Konfiguration Section `Jwt` (`JwtOptions`): Issuer, Audience, Laufzeiten in `appsettings.json`,
  **`Jwt:SigningKey`** (Base64, ≥ 32 Bytes) nur aus Secret/Env-Var.
- Rechte: Policies `CanRead` / `CanEdit` / `IsOwner` aus der Konto-Rolle, System-Rolle `Admin` getrennt.
  `CurrentAccount` liefert `UserId` und `CustomerId` des aktiven Kontos; jeder Datenzugriff filtert darüber.
- Admin-Seed beim Start aus `SeedAdmin:Email` / `SeedAdmin:Password`.

### Portal (`MyOit.Portal`)
- Eigenes Cookie-Schema; im Cookie steht nur die Sitzungs-ID (Claim `portal_sid`) plus Anzeige-Claims.
- Access- und Refresh-Token liegen serverseitig im `ITokenStore` (heute `InMemoryTokenStore`).
- `TokenSessionManager` erneuert vor Ablauf und serialisiert Refresh, Logout, Kontowechsel und
  Passwortänderung pro Sitzung (Refresh-Token ist nur einmal verwendbar).
- `BearerTokenHandler` hängt das Token an alle API-Aufrufe (`IotWeltApiClient`).
- Seiten, die das Cookie setzen (Login, Registrierung, Einladung annehmen, Konto löschen), laufen als
  statisches SSR; interaktive Seiten ändern das Cookie per Formular-POST an `/account/*`
  (Logout, Kontowechsel, Eigentümerschaft übertragen) — mit Antiforgery-Pflicht.

### Lokal (Aspire)
- Der AppHost reicht den Parameter `jwt-signing-key` (User-Secrets des AppHost,
  `Parameters:jwt-signing-key`) als `Jwt__SigningKey` an die API weiter.
- Tests setzen einen eigenen Schlüssel (`ApiFactory`), Datenbank per Testcontainers.

---

## 💾 Datenbank

### Tabellen
- Identity: `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`,
  `AspNetUserTokens`, `AspNetRoleClaims` (`AppDbContext : IdentityDbContext<AppUser>`)
- Konten: `Accounts` (mit `CustomerId`), `AccountMemberships` (Login ↔ Konto mit Rolle, genau ein Owner
  pro Konto per gefiltertem Unique-Index), `AccountInvitations`, `RefreshTokens`
- Fachdaten: `Devices`, `RaumKlimaLogs`

### Migrationen
- Frische Basis `InitialV2` (alte Stände nur über Tag `v0.4.1-azure`), danach `AccountInvitations`,
  `OneOwnerPerAccount`.
- Lokal per `Update-Database` (Befehle in `CLAUDE.md`).
- **myASP.NET:** idempotentes Skript erzeugen und im SQL-Manager des Control Panels ausführen:
  ```bash
  dotnet ef migrations script --idempotent --project IotWelt.API --startup-project IotWelt.API -o migrate.sql
  ```

### Connection String Format (myASP.NET)
```
Server=sql.myasp.net;Database=teqpool_xxx;User Id=teqpool_user;Password=…;Encrypt=true;
```
*Verbindungsdetails aus dem myASP.NET Control Panel; Name des Connection-Strings: `iotweltdb`.*

---

## 🚀 Deployment-Optionen bei myASP.NET (Phase 4)

### Option A: GitHub Actions + Web Deploy (Ziel, Phase 3/4)
Build und Tests in GitHub Actions, danach Web Deploy (MSDeploy) mit den Publish-Daten aus dem Control Panel
als Repository-Secrets.

### Option B: Visual Studio Web Deploy
```
Rechtsklick auf Projekt → Publish → Web Deploy → myASP.NET-Daten aus dem Control Panel → Publish
```

### Option C: FTP
```bash
dotnet publish -c Release
# Ausgabe per FTP (z. B. FileZilla) nach /site/wwwroot hochladen
```

API und Portal sind zwei Anwendungen (z. B. `api.things.teqpool.net` und `things.teqpool.net`).

---

## ⚙️ Konfiguration für myASP.NET

Secrets kommen als Umgebungsvariablen über die `web.config` (`<aspNetCore><environmentVariables>`)
bzw. die Einstellungen im Control Panel — **nicht** in `appsettings.Production.json`.

| Anwendung | Variable | Inhalt |
|---|---|---|
| API | `ConnectionStrings__iotweltdb` | Connection-String (s. o.) |
| API | `Jwt__SigningKey` | Base64, ≥ 32 Bytes, eigener Schlüssel je Umgebung |
| API | `SeedAdmin__Email`, `SeedAdmin__Password` | erster Admin (nach dem ersten Start entfernen) |
| API | `App__PortalBaseUrl` | `https://things.teqpool.net` (Links in Mails) |
| Portal | `IotWeltApi__BaseUrl` | URL der API |
| beide | `ASPNETCORE_ENVIRONMENT` | `Production` |

### Offene Punkte für Produktion
- **E-Mail-Versand:** `LoggingEmailSender` schreibt Bestätigungs-, Reset- und Einladungslinks nur ins Log.
  Für Produktion braucht es einen echten Sender (SMTP von myASP.NET) — siehe `docs/backlog.md`.
- **Portal-Sitzungen im Speicher:** Ein Neustart oder App-Pool-Recycle des Portals meldet alle ab.
  Für Produktion: persistenter `ITokenStore` und Data-Protection-Schlüssel dauerhaft ablegen
  (sonst sind auch die Cookies nach dem Recycle ungültig).
- IIS: eigener Application Pool je Anwendung, .NET 10 Hosting Bundle muss beim Hoster verfügbar sein.

---

## ✅ Test-Szenarien

1. **Registrierung:** `/account/register` → Bestätigungslink (aus dem Log) öffnen → Anmelden.
   Prüfen: neuer Eintrag in `AspNetUsers`, Konto mit Login als Owner.
2. **Login und Refresh:** Anmelden, länger als 15 min arbeiten — keine Abmeldung (Refresh im Hintergrund).
3. **Mandantentrennung:** Zweiter Login sieht die Geräte des ersten nicht (automatisiert in `IotWelt.API.Tests`).
4. **Einladung:** Owner lädt als Leser ein → Link annehmen → Kontowechsler zeigt beide Konten, Leser kann nichts ändern.
5. **Admin:** Login ohne Admin-Rolle ruft `/admin/users` auf → kein Zugriff; nach „Zum Admin machen“ und
   erneuter Anmeldung (bzw. spätestens nach 15 min) → Zugriff.
6. **Sensor:** `POST /api/sensor` (Beispiel in `IotWelt.API.http`) → Gerät erscheint im Dashboard.

---

## ⚠️ Bekannte Fallstricke & Lösungen

### API startet nicht: „Jwt:SigningKey fehlt“
Lokal den AppHost-Parameter `jwt-signing-key` setzen, in Produktion die Env-Var `Jwt__SigningKey`.

### Alle Sitzungen plötzlich beendet
Ein Refresh-Token wurde zweimal vorgelegt (z. B. parallele Refreshs) → die API widerruft alle Sitzungen.
Im Portal verhindert das die Sitzungssperre im `TokenSessionManager`; neue Clients (MAUI) brauchen dasselbe.

### Rechte-Änderung wirkt nicht sofort
Rollen stehen im Access-Token und werden erst beim nächsten Refresh (spätestens nach 15 min) neu gelesen.

### Migration schlägt fehl
Lokal vor dem Deployment `Update-Database`; bei myASP.NET nur das idempotente Skript verwenden.

### Connection String ungültig
`Encrypt=true;` und ggf. `Connection Timeout=30;` setzen, Werte aus dem Control Panel übernehmen.

---

## 📝 Checkliste

### Phase 1 – Benutzerverwaltung (erledigt, v0.5.0)
- [x] Identity in der API, eigene Token-Ausgabe, Migrationsbasis `InitialV2`
- [x] Konten, Mitgliedschaften, Einladungen, Admin-Login-Verwaltung
- [x] Portal ohne Entra ID, Anmeldung gegen die API
- [x] Entra-ID-, Graph- und `AzureAd`-Reste entfernt
- [x] Integrationstests mit echten Tokens (Testcontainers)

### Phase 4 – myASP.NET
- [ ] myASP.NET-Plan bestellt, teqpool.de verbunden, things.teqpool.net umgeleitet
- [ ] Datenbank angelegt, idempotentes Migrationsskript eingespielt
- [ ] Env-Vars gesetzt (Tabelle oben), Admin-Seed nach dem ersten Start entfernt
- [ ] Echter E-Mail-Versand, persistente Portal-Sitzungen und Data-Protection-Schlüssel
- [ ] API und Portal deployt, Test-Szenarien bei things.teqpool.net durchgespielt

---

## 📚 Ressourcen & Referenzen
- [Identity für APIs und SPAs](https://learn.microsoft.com/aspnet/core/security/authentication/identity-api-authorization)
- [Blazor Security](https://learn.microsoft.com/aspnet/core/blazor/security/)
- [ASP.NET Core auf IIS hosten](https://learn.microsoft.com/aspnet/core/host-and-deploy/iis/)
- [Data Protection konfigurieren](https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview)
- myASP.NET Knowledge Base: https://www.myasp.net/support/kb/root.aspx
