# IotWelt — Lernprojekt

## Zweck
Übungsprojekt zum Erlernen des Zusammenspiels von .NET Aspire, Blazor Server, Web API, EF Core, SQL Server, JWT-Auth und IoT (ESP32).

## Solution-Struktur

| Projekt | Typ | Zweck |
|---|---|---|
| `IotWelt.AppHost` | Aspire AppHost | Orchestrierung aller Services |
| `IotWelt.ServiceDefaults` | Class Library | Gemeinsame Aspire-Konfiguration (Health, Telemetry) |
| `MyOit.Portal` | Blazor Server | Frontend-Portal mit ASP.NET Core Identity |
| `IotWelt.API` | ASP.NET Core Web API | REST-API für Gerätedaten |
| `IotWelt.DbClasses` | Class Library | Geplant: Gemeinsame DB-Modelle/Entities |

## Laufen lassen
- Startprojekt: `IotWelt.AppHost`
- Voraussetzung: SQL Server LocalDB (`MSSQLLocalDB`)
- Aspire Dashboard öffnet sich automatisch beim Start

## Datenbank
- Engine: SQL Server LocalDB (kein Docker)
- Connection-String in `IotWelt.API/appsettings.Development.json`
- DbContext: `IotWelt.API/Data/AppDbContext.cs`
- Migrations-Befehle immer mit expliziten Flags:
  ```
  Add-Migration <Name> -Project IotWelt.API -StartupProject IotWelt.API
  Update-Database -Project IotWelt.API -StartupProject IotWelt.API
  ```

## API-Endpoints
- Scalar UI: `https://localhost:<port>/scalar/v1`
- `GET/POST/PUT/DELETE /api/devices` — CRUD für IoT-Geräte
- `POST /api/sensor` — Sensordaten von ESP32 empfangen

## ESP32-Integration
- ESP32 sendet per HTTP POST an `/api/sensor`
- Payload: `{ "deviceId": <uint 4-bit>, "temperatur": <double>, "relativeFeuchte": <double>, "wasserAlarm": <bool> }`
- Device muss vorher über `/api/devices` angelegt werden (mit passender `DeviceId`)

## Offen / Nächste Schritte
- [ ] Portal an die API anbinden (HttpClient via Aspire Service Discovery)
- [ ] JWT-Auth zwischen Portal und API verdrahten
- [ ] `IotWelt.DbClasses` mit gemeinsamen Modellen befüllen
- [ ] Liquid UI im Portal einrichten
