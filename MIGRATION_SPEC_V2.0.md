# Blazor Server Projekt – Migration zu Version 2.0
## Von Azure/Entra ID zu myASP.NET/ASP.NET Identity

**Autor:** Thorsten  
**Datum:** September 2026  
**Status:** Specification für Claude Code Umsetzung  
**Zielplattform:** myASP.NET Pro Plan (Windows Server 2022, SQL Server 2025)

---

## 📋 Executive Summary

Bestehende Blazor Server Anwendung (v1.0) läuft aktuell in Azure mit Entra ID für Authentifizierung. Version 2.0 wird auf myASP.NET gehostet und nutzt ASP.NET Identity für Benutzerverwaltung. Ziel: Kostenersparnis (€24–38/Monat vs. Azure) bei gleicher Funktionalität für interne Demoanwendung.

---

## 🎯 Anforderungen & Constraints

### Funktionale Anforderungen
- [x] Blazor Server Frontend (bestehend, keine Logik-Änderungen)
- [x] ASP.NET Web API (bestehend, nur Auth-Anpassung)
- [x] Benutzerverwaltung (Entra ID → ASP.NET Identity Migration)
- [x] Entity Framework mit Dependency Injection (unverändert)
- [x] SQL Server als Datenbank (unverändert)

### Nicht-funktionale Anforderungen
- Domain: **teqpool.de** (mit myASP.NET verbunden)
- Subdomain: **things.teqpool.net** (von Strato auf myASP.NET umgeleitet)
- Hosting: myASP.NET Pro Plan
- DB-Host: myASP.NET SQL Server 2025
- .NET Version: .NET 8 LTS (mindestens)
- Datentyp: **Demodaten nur** (keine Produktionsdaten, daher Migration einfacher)

### Constraints & Limitations
- ❌ Entra ID Features aufgegeben: MFA, Conditional Access, Azure B2C
- ❌ Kein Azure Key Vault: Secrets müssen in `appsettings.json` oder Environment Variables
- ⚠️ Keine Single Sign-On (SSO) mit Microsoft Account (falls nicht gewünscht)
- ⚠️ Benutzer müssen sich neu registrieren oder werden importiert

---

## 🔄 Architektur – Vorher vs. Nachher

### v1.0 (Azure/Entra ID)
```
┌─────────────────────────────────────────┐
│   Blazor Server (Azure App Service)     │
│   ├─ Razor Components                   │
│   └─ DI Container                       │
└──────────────┬──────────────────────────┘
               │
       ┌───────┴────────┐
       ▼                ▼
┌──────────────┐  ┌──────────────────┐
│ Web API      │  │ Entra ID (Azure) │
│ Controllers  │  │ Authentication   │
└──────┬───────┘  └──────────────────┘
       │
       ▼
┌──────────────────────┐
│ SQL Server (Azure)   │
│ ├─ UserClaimsTable   │
│ ├─ RolesTable        │
│ └─ App Data          │
└──────────────────────┘
```

### v2.0 (myASP.NET/ASP.NET Identity)
```
┌─────────────────────────────────────────┐
│   Blazor Server (myASP.NET)             │
│   ├─ Razor Components                   │
│   └─ DI Container                       │
└──────────────┬──────────────────────────┘
               │
       ┌───────┴────────┐
       ▼                ▼
┌──────────────┐  ┌──────────────────────┐
│ Web API      │  │ ASP.NET Identity     │
│ Controllers  │  │ (lokale Auth)        │
└──────┬───────┘  └──────────────────────┘
       │          (in SQL Server Tabellen)
       ▼
┌──────────────────────────┐
│ SQL Server (myASP.NET)   │
│ ├─ AspNetUsers           │
│ ├─ AspNetRoles           │
│ ├─ AspNetUserRoles       │
│ └─ App Data              │
└──────────────────────────┘
```

---

## 🔐 Authentifizierung – Änderungen

### Alte Konfiguration (Entra ID)
```csharp
// Program.cs (v1.0)
builder.AddMicrosoftIdentityWebAppAuthentication(configuration);
builder.AddMicrosoftIdentityWebApi(configuration);

// appsettings.json
"AzureAd": {
  "Instance": "https://login.microsoftonline.com/",
  "ClientId": "xxx-xxx-xxx",
  "TenantId": "xxx-xxx-xxx",
  "CallbackPath": "/signin-oidc"
}
```

### Neue Konfiguration (ASP.NET Identity)
```csharp
// Program.cs (v2.0)
// 1. DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Identity
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// 3. Authentication Scheme
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Identity/Account/Login";
        options.LogoutPath = "/Identity/Account/Logout";
        options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    });

builder.Services.AddAuthorization();

// Alte Entra ID Services ENTFERNEN
```

```json
// appsettings.json (v2.0)
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=sql2025.database.windows.net;Database=teqpool_v2;User Id=sa;Password=xxx;Encrypt=true;Connection Timeout=30;"
  }
}
```

---

## 💾 Datenbank-Migration

### Neue Tabellen (ASP.NET Identity)
Diese werden von EF automatisch erstellt:
- `AspNetUsers` – Benutzer (UserName, Email, PasswordHash, etc.)
- `AspNetRoles` – Rollen (Admin, User, etc.)
- `AspNetUserRoles` – Zuordnung User ↔ Roles
- `AspNetUserClaims` – Custom Claims pro Benutzer
- `AspNetUserLogins` – OAuth/Social Logins (optional)

### Migration Path
```bash
# 1. Bestehende Entra ID Users extrahieren (manuell oder via Azure CLI)
# Export: CSV mit Email, UserId, etc.

# 2. EF Migration erstellen
dotnet ef migrations add InitialIdentity

# 3. Datenbank aktualisieren (bei myASP.NET)
dotnet ef database update

# 4. Benutzer-Import (optional, einmalig)
# Script: ImportEntraUsersToIdentity.cs
# ├─ Liest CSV der Entra ID Users
# ├─ Erstellt IdentityUser + hasht Passwort
# └─ Schreibt zu AspNetUsers
```

### Connection String Format (myASP.NET)
```
Server=sql.myasp.net;Database=teqpool_xxx;User Id=teqpool_user;Password=YourPassword;Encrypt=true;
```
*Verbindungsdetails aus myASP.NET Control Panel kopieren*

---

## 🔑 Entity Framework – Keine Änderungen nötig

### Bestehende DbContext-Struktur bleibt erhalten
```csharp
public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Bestehende DbSets (unverändert)
    public DbSet<YourEntity> YourEntities { get; set; }
    public DbSet<AnotherEntity> AnotherEntities { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Bestehende Konfiguration bleibt
    }
}
```

**WICHTIG:** `IdentityDbContext<IdentityUser>` erbt von `DbContext` und enthält Identity-Tabellen

---

## 🔌 Dependency Injection – Keine Änderungen

```csharp
// Program.cs (DI-Container identisch)
builder.Services.AddScoped<IYourService, YourService>();
builder.Services.AddScoped<IAnotherService, AnotherService>();
// ... bestehende Services bleiben unverändert
```

**EF Injection funktioniert weiter:**
```csharp
public class YourService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<YourService> _logger;

    public YourService(ApplicationDbContext context, ILogger<YourService> logger)
    {
        _context = context;
        _logger = logger;
    }
}
```

---

## 🌐 Web API – Auth-Anpassung erforderlich

### Alte API (Entra ID Token)
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MyApiController : ControllerBase
{
    [HttpGet]
    public IActionResult GetData()
    {
        var userId = User.GetObjectId(); // Entra ID spezifisch
        return Ok();
    }
}
```

### Neue API (JWT oder Session)
**Option A: Über Blazor Session (einfacher)**
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MyApiController : ControllerBase
{
    [HttpGet]
    public IActionResult GetData()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userName = User.FindFirst(ClaimTypes.Name)?.Value;
        return Ok(new { userId, userName });
    }
}
```

**Option B: JWT-Token (wenn externe API-Clients nötig)**
```csharp
// Program.cs (zusätzlich)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("YourSecretKey")),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

// Authentifizierungs-Endpoint (neuer Login-Endpoint)
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginModel model)
{
    var user = await _userManager.FindByEmailAsync(model.Email);
    if (user != null && await _userManager.CheckPasswordAsync(user, model.Password))
    {
        var token = GenerateJwtToken(user);
        return Ok(new { token });
    }
    return Unauthorized();
}
```

---

## 🖥️ Blazor Server – Minimal Changes

### AuthorizeRouteView bleibt unverändert
```razor
<!-- _Host.cshtml oder Layouts/MainLayout.razor -->
<CascadingAuthenticationState>
    <Router AppAssembly="@typeof(Program).Assembly">
        <Found Context="routeData">
            <AuthorizeRouteView RouteData="@routeData" 
                                DefaultLayout="@typeof(MainLayout)">
                <NotAuthorized>
                    <p>Bitte <a href="/Identity/Account/Login">anmelden</a></p>
                </NotAuthorized>
            </AuthorizeRouteView>
        </Found>
    </Router>
</CascadingAuthenticationState>
```

### User Info abrufen
```razor
@page "/profile"
@using System.Security.Claims
@inject AuthenticationStateProvider AuthenticationStateProvider

<h3>Benutzerprofil</h3>

@if (user != null)
{
    <p>Name: @user.Identity?.Name</p>
    <p>Email: @user.FindFirst(ClaimTypes.Email)?.Value</p>
}

@code {
    private ClaimsPrincipal? user;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        user = authState.User;
    }
}
```

---

## 📝 Migrations-Checkliste

### Phase 1: Vorbereitung (vor Code-Änderungen)
- [ ] myASP.NET Pro Plan bestellt
- [ ] teqpool.de mit myASP.NET verbunden
- [ ] things.teqpool.net von Strato umgeleitet
- [ ] SQL Server Connection String aus myASP.NET Control Panel kopiert
- [ ] Entra ID Users exportieren (falls Import nötig)

### Phase 2: Code-Änderungen
- [ ] Program.cs: Entra ID → ASP.NET Identity umstellen
- [ ] appsettings.json: Connection String aktualisieren
- [ ] ApplicationDbContext: Auf IdentityDbContext<IdentityUser> erben
- [ ] EF Migration erstellen: `InitialIdentity`
- [ ] Web API Auth-Endpoints anpassen
- [ ] Login/Register Pages (aus ASP.NET Identity Scaffolding) integrieren
- [ ] Tests: Authentifizierung lokal testen

### Phase 3: Datenbank
- [ ] Lokal: `dotnet ef database update`
- [ ] Bei myASP.NET: EF-Migration via Deployment anwenden
- [ ] (Optional) Benutzer-Import skripten

### Phase 4: Deployment & Testing
- [ ] Lokal: Volle Funktionalität prüfen (Login, API, Blazor)
- [ ] Visual Studio Publish Profil erstellen (Web Deploy zu myASP.NET)
- [ ] Zu myASP.NET deployen
- [ ] Test bei things.teqpool.net: Login, API-Calls, Blazor Components

### Phase 5: Cleanup
- [ ] Alte Azure App Service (optional) stillegen
- [ ] Alte Entra ID App Registration (optional) deaktivieren

---

## 🛠️ Wichtige Dateien zum Anpassen

### Priorität 1 (Critical)
- `Program.cs` – Auth-Konfiguration komplett neu
- `appsettings.json` – Connection Strings
- `ApplicationDbContext.cs` – Erbschaft anpassen

### Priorität 2 (Required)
- Web API Controller mit `[Authorize]` – Anspruchsadressen prüfen
- Login/Register Seiten (aus Scaffolding oder bestehend)

### Priorität 3 (Nice-to-have)
- Entra ID Claims → ASP.NET Identity Claims Migration
- JWT-Endpoint (falls externe API-Clients)

### Nicht anfassen
- Blazor Components (`.razor` Dateien)
- Bestehende Services (IYourService, etc.)
- Bestehende DbSets (YourEntity, etc.)

---

## 🚀 Deployment-Optionen bei myASP.NET

### Option A: GitHub Auto-Deploy (empfohlen)
```bash
# 1. Repository zu GitHub pushen
# 2. myASP.NET Control Panel → Deployments → GitHub verbinden
# 3. Automatischer Build & Deploy bei jedem Push
# 4. Produktiv in ~2 Minuten
```

### Option B: Visual Studio Web Deploy
```
Rechtsklick auf Projekt → Publish
→ Hosting Typ: Web Deploy
→ myASP.NET-Daten eingeben (aus Control Panel)
→ Publish Button
```

### Option C: FTP
```bash
# Lokal publishen
dotnet publish -c Release

# FTP-Client (z.B. FileZilla)
# Verbinden mit myASP.NET FTP-Daten
# /site/wwwroot hochladen
```

**Empfehlung:** GitHub Auto-Deploy für Entwicklung, dann Web Deploy für Produktion

---

## ⚙️ Konfiguration für myASP.NET

### Environment Settings
```json
// appsettings.Production.json (neu)
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=sql.myasp.net;Database=teqpool_prod;User Id=xxx;Password=xxx;Encrypt=true;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  }
}
```

### IIS Einstellungen (automatisch bei myASP.NET)
- Application Pool: Dedicated
- .NET Version: .NET 8
- Authentication: Forms (ASP.NET Identity)

---

## ✅ Test-Szenarien

### Szenario 1: Benutzer-Registrierung
```
1. things.teqpool.net/Identity/Account/Register aufrufen
2. Email + Passwort eingeben
3. Registrierung bestätigen
4. Überprüfung: AspNetUsers Tabelle in SQL Studio
```

### Szenario 2: Login
```
1. things.teqpool.net/Identity/Account/Login
2. Credentials eingeben
3. Weiterleitung zu Dashboard
4. Überprüfung: User.Identity.IsAuthenticated == true
```

### Szenario 3: Web API Call
```
1. Angemeldet bleiben
2. JavaScript: fetch('/api/myapi/getdata')
3. Überprüfung: Response mit Benutzerdaten
```

### Szenario 4: Autorisierung (Admin-only)
```
1. User ohne Admin-Rolle: /admin aufrufen
2. Erwartet: Access Denied
3. Admin hinzufügen: `_userManager.AddToRoleAsync(user, "Admin")`
4. Nochmal versuchen: Should work
```

---

## 📚 Ressourcen & Referenzen

### Offizielle Dokumentation
- [ASP.NET Identity in Blazor](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization)
- [Entity Framework with Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model)
- [Authorize in Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/)

### myASP.NET
- Knowledge Base: https://www.myasp.net/support/kb/root.aspx
- SQL Studio: Im Control Panel unter Database Manager
- Connection String Format: Im Control Panel → Databases

### Hilfreich
- EF Migrations: `dotnet ef migrations add <Name>`
- User Secrets lokal: `dotnet user-secrets init`
- Logging: `ILogger<T>` in Services

---

## ⚠️ Bekannte Fallstricke & Lösungen

### Problem 1: Migration schlägt fehl
```
Error: Pending migrations detected
Lösung: `dotnet ef database update` lokal vor Deployment
```

### Problem 2: Connection String zu lang oder ungültig
```
Fehler: `String not recognized as a valid Boolean value`
Lösung: Encrypt=true; Connection Timeout=30; korrekt setzen
```

### Problem 3: Benutzer können sich nicht anmelden
```
Überprüfung:
1. AspNetUsers Tabelle hat die User-Einträge
2. Password Hash mit CheckPasswordAsync korrekt
3. Rollen korrekt in AspNetUserRoles
```

### Problem 4: Web API gibt 401 Unauthorized
```
Überprüfung:
1. [Authorize] auf Controller/Action
2. User.Identity.IsAuthenticated == true (in Blazor)
3. Cookie gesetzt im HTTP-Request
```

---

## 📊 Erfolgs-Kriterien für v2.0

- [x] Localhost läuft fehlerfrei
- [x] Blazor Server + Web API authentifizieren sich gegenseitig
- [x] SQL Server auf myASP.NET mit gültigen Benutzer-Tabellen
- [x] Deployment zu things.teqpool.net erfolgreich
- [x] Login, API-Calls, Autorisierung bei things.teqpool.net funktionieren
- [x] EF Queries funktionieren gegen neue Datenbank

---

## 🎓 Nächste Schritte für Claude Code

1. **Projekt-Struktur analysieren:** `dotnet list package` + Ordner-Layout
2. **Program.cs refaktorieren** mit Auth-Umstieg
3. **appsettings.json** vorbereiten
4. **ApplicationDbContext.cs** auf IdentityDbContext anpassen
5. **Web API Endpoints** auf neue Authentifizierung prüfen
6. **Login/Register Seiten** vorbereiten (Scaffolding oder bestehend?)
7. **Lokales Testen** mit `dotnet run`
8. **Migrations erstellen & anwenden**

**Wichtig:** Dieses Dokument in deinen Projekt-Ordner ablegen, damit Claude Code es als Kontext hat!

---

## 📧 Kontakt & Fragen

Bei Fragen zur Migration: Claude Code kann dieses Dokument als Referenz nutzen.

**Version:** 2.0 Migration Spec  
**Letztes Update:** September 2026  
**Status:** Ready for Implementation
