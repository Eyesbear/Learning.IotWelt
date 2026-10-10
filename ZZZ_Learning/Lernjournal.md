# Lernjournal — IotWelt v2.0

Ziel des Journals: konkrete, belegbare Beispiele sammeln — was hat der Agent gut gemacht,
wo lag er falsch, wie wurde es gefunden, was habe ich daraus gelernt.
Grundregel für Lebenslauf und Gespräche: **nur aufschreiben, was ich selbst praktisch gemacht habe.**

Aufbau pro Phase: *Fakten* (was passiert ist) · *Agent-Bilanz* · *Meine Erkenntnisse* · *Belege* (Commits/PRs).

---

## Phase 0 — Fundament (Git, CLAUDE.md, Tests, Hook) · 2026-10-06

### Fakten
- Neuausrichtung des Projekts im **Plan Mode** erarbeitet: Ziele, Phasen, Architekturentscheidungen
  (API als Token-Aussteller, Aspire bleibt für Dev, Staging in Docker, Produktion bei myASP.NET).
- Letzten Azure-Stand als annotierten Tag `v0.4.1-azure` gesichert und gepusht.
- `.claude/settings.local.json` aus dem Repo genommen (`git rm --cached`) und in `.gitignore` aufgenommen.
- Secret-Scan der gesamten Historie mit **gitleaks im Docker-Container** + manuellem `git log -p`-Grep → sauber.
  Danach Repo auf **öffentlich** gestellt und `master` per **Ruleset** geschützt (PR-Pflicht, kein Force-Push).
- `CLAUDE.md` neu geschrieben (Zielarchitektur, Definition of Done, Konventionen).
- User Stories zur Benutzerverwaltung erarbeitet: Konto ↔ Login ↔ Mitgliedschaft, Owner/Editor/Reader,
  Einladungen, Kontowechsel (`docs/user-stories/benutzerverwaltung.md`).
- Testprojekt `IotWelt.API.Tests`: 18 Integrationstests mit `WebApplicationFactory` und
  **SQL Server per Testcontainers**; Test-Login über Header statt Entra-JWT.
- Claude-Code-**Stop-Hook**, der vor Abschluss jeder Antwort `dotnet build` ausführt und bei Fehlern blockiert.
- Erster Pull Request (#1) mit 5 Commits, gemergt per **Merge-Commit**.

### Agent-Bilanz

**Gut gelaufen**
- Hat beim Lesen des Codes eine echte **Sicherheitslücke** gefunden: Der anonyme Sensor-Endpoint erlaubt,
  ein fremdes Gerät per bekannter `hardwareId` auf eine andere `customer_id` umzuhängen.
  Bewusst als Test dokumentiert statt still „repariert“ → Fix ist für Phase 1 eingeplant.
- Hat vor dem Öffentlich-Schalten auf die GitHub-Einschränkung hingewiesen
  (Rulesets greifen bei privaten Repos im Free-Plan nicht) und den Secret-Scan selbst vorgeschlagen.
- Hook-Skript in vier Szenarien getestet, bevor es übergeben wurde (inkl. Endlosschleifen-Schutz).

**Fehler des Agenten**
| # | Fehler | Wie gefunden | Lehre |
|---|---|---|---|
| 1 | JSON-Feldname des Sensor-Payloads geraten: `customerId` statt `customer_id` (`[JsonPropertyName]` übersehen) — in Tests **und** in `CLAUDE.md` | 4 von 18 Tests rot beim ersten Lauf | Agent liest Dateien teils nur auszugsweise und ergänzt „plausibel“. Tests decken das auf — Doku nicht. |
| 2 | Anleitung zum Ruleset-Test war unbrauchbar: `git push origin master` ohne neuen Commit kann gar nicht abgelehnt werden (und es fehlte der Hinweis auf das Arbeitsverzeichnis) | Mein Testlauf lieferte eine unerwartete Fehlermeldung, Rückfrage | Prüfanweisungen des Agenten selbst kritisch lesen: *Kann dieser Test überhaupt fehlschlagen?* |
| 3 | In der ersten `CLAUDE.md`-Fassung eine unbelegte Behauptung („Tenant nicht mehr nutzbar“) | Agent hat es beim Gegenlesen selbst entschärft | Aussagen in Kontextdateien müssen überprüfbar sein — der Agent liest sie in jeder Sitzung als Wahrheit. |

### Meine Erkenntnisse

Für mich sind Git-Tags und Rulesets neu. Ebenso die komplette Bedienung von GIT über Konsole. Hier werde ich wohl eher bei Visual Studio und der Webseite bleiben.
Agentic Coding setzt gut Planung und exakte Beschreibung des gewünschten Ergebnisses voraus. Ich hoffe der Weg vom Test-Mode zum laufenden Testcontainer wird sich mir im weiteren noch besser erschließen.
Die Fehler 1 und2 (customer_id und ruleset test ohne neuen Commit) wären mir wohl nach langer Suche aufgefallen. Die Überprüfung der Kontextdatei auf Wahrheitsgehalt wird mich wohl noch einige Übung kosten.
Die verschiedenen Optionen beim Merge (Merge-Commit vs. Squash vs. Rebase) könnte ich zur Zeit noch nicht begründen.
In Phase 1 werde ich definitiv mehr Zwischenfragen stellen.

### Belege
- PR #1: https://github.com/Eyesbear/Learning.IotWelt/pull/1 (Merge-Commit `28aee5d`)
- Tag `v0.4.1-azure`
- Tests: `IotWelt.API.Tests/` · Hook: `.claude/hooks/build-check.sh` · Stories: `docs/user-stories/benutzerverwaltung.md`

### Lebenslauf-tauglich (nur wenn selbst nachvollzogen)
- [x] Integrationstests für ASP.NET Core Web API mit WebApplicationFactory und Testcontainers (SQL Server)
- [x] Git-Workflow mit geschütztem Hauptbranch, Pull Requests und Release-Tags
- [x] Agentic Coding mit Claude Code: Projektkontext (CLAUDE.md), Plan Mode, Hooks als Qualitätsschranke

---

## Phase 1 — Eigene Benutzerverwaltung (Azure-Ausbau) · 2026-10-08 bis 2026-10-10

### Fakten
- Plan für Phase 1 im **Plan Mode** freigegeben und in sechs PRs geschnitten (1a–1f), jeder einzeln prüf- und mergebar.
- **1a (#3):** ASP.NET Core Identity in der API, eigene JWTs (HS256, 15 min) und rotierende Refresh-Tokens
  (14 Tage, nur als Hash gespeichert, Wiederverwendung widerruft alle Sitzungen). Konten und Mitgliedschaften
  statt `CustomerProfiles`, frische Migrationsbasis `InitialV2`, Admin-Seed. Tests melden sich mit echten Tokens an.
  JWT-Schlüssel per **User-Secrets** gesetzt, Endpoints in **Scalar** ausprobiert.
- **1b (#4):** Die Sicherheitslücke aus Phase 0 geschlossen — der anonyme Sensor-Endpoint hängt Geräte mit
  Besitzer nicht mehr um. Erst der rote Test, dann der Fix.
- **1c (#5) und 1d (#6) parallel** in zwei **Git-Worktrees** (`IotWelt-members`, `IotWelt-portal`) mit je einer
  eigenen Claude-Sitzung und schriftlichem Arbeitsauftrag samt Schnittstellen-Vertrag.
  1c: Einladungen, Mitgliederverwaltung, Login löschen, Admin-Login-Verwaltung (91 Tests).
  1d: Portal meldet sich an der eigenen API an (Cookie nur mit Sitzungs-ID, Tokens serverseitig), Registrierung,
  Passwort vergessen, Kontowechsler, Entra und Graph entfernt.
- **Gewollter Merge-Konflikt:** Beide Branches ergänzten denselben CHANGELOG-Abschnitt. Nach dem Merge von 1c
  den Konflikt im Portal-Branch selbst aufgelöst (`master` in den Branch gemergt, `793e5c6`).
- **1e (#7):** Portal-Seiten Konto/Login löschen, Passwort ändern, Mitglieder, Einladung annehmen,
  Admin-Benutzerverwaltung auf Logins. Jede Seite im Browser getestet, bevor committet wurde.
- **1f:** JWT-Schlüssel als geheimer **Aspire-Parameter** im AppHost, ungenutzte Endpoints entfernt,
  Bearer-Schema für Scalar, Doku an die Umsetzung angepasst, Version zentral in `Directory.Build.props`, Release 0.5.0.
- Neue Funde landen seit 1e in **`docs/backlog.md`** statt nur im Chat oder in den Notizen des Agenten.
- Noch offen: Browsertest Klimaverlauf-Fehlerhinweis und 401 → Login (fehlende Sensordaten in der Dev-Umgebung).

### Agent-Bilanz

**Gut gelaufen**
- **Refresh-Token-Race erkannt, bevor sie auftrat:** Weil ein Refresh-Token nur einmal gilt, hätten zwei parallele
  Refreshs derselben Portal-Sitzung alle Sitzungen des Logins widerrufen. Lösung: Sperre pro Sitzung im
  `TokenSessionManager`, auch für Kontowechsel und Passwortänderung.
- **Veraltete Tokens nach Konto löschen / Eigentümer übertragen** selbst bedacht: Token und Cookie werden sofort
  erneuert, statt bis zu 15 min auf ein gelöschtes Konto bzw. eine alte Rolle zu zeigen.
- **Worktree-Verwechslung bemerkt:** Eine Sitzung im Portal-Worktree bekam versehentlich den API-Auftrag (1c)
  und hat darauf hingewiesen, bevor Code auf dem falschen Branch entstand.
- Eigene Annahmen per **Negativtest** geprüft (Logout ohne Antiforgery-Token, fremde `ReturnUrl`, altes Cookie nach Logout)
  und so zwei eigene Fehler gefunden (siehe Tabelle, Nr. 2 und 3).
- Fachliche Lücken (Sensor legt Geräte für gelöschte Konten an, Logins ohne Konto, E-Mail-Versand) gemeldet
  und ins Backlog geschrieben, statt sie still mitzubauen.

**Fehler des Agenten**
| # | Fehler | Wie gefunden | Lehre |
|---|---|---|---|
| 1 | PowerShell-Befehl für den JWT-Schlüssel nutzte `RandomNumberGenerator.GetBytes(64)` — gibt es erst ab .NET 6, mein Terminal ist Windows PowerShell 5.1 (.NET Framework) | Fehlermeldung beim Ausführen | Der Agent setzt eine Laufzeitumgebung voraus, ohne sie zu kennen. Gehört in den Kontext (Shell-Version). |
| 2 | Angenommen, `UseAntiforgery()` weise ungültige Anfragen ab — tatsächlich notiert es nur das Ergebnis. Logout war damit per CSRF auslösbar | Eigener Negativtest des Agenten (Logout ohne Token → kam durch) | Sicherheitsannahmen nur gelten lassen, wenn ein Test zeigt, dass der Angriff **scheitert**. |
| 3 | Die abgewiesene Anfrage lieferte 500 statt 400 (`UseStatusCodePagesWithReExecute` leitete sie an `/not-found` weiter) | Derselbe Negativtest | Fehlerpfade durch die ganze Pipeline testen, nicht nur den Endpoint. |
| 4 | „Das Scalar-Login-Feld habe ich für PR 1f notiert“ — stand aber nirgends | Beim Sammeln fürs Lernjournal in den Sitzungsprotokollen gefunden, in 1f nachgeholt | Ein „ist notiert“ des Agenten ist nur etwas wert, wenn es in einer Datei im Repo steht (heute: `docs/backlog.md`). |
| 5 | Verrutschte Bedingung in der Passwortänderung: wann ein API-Fehler die Portal-Sitzung beendet und wann nur Fehlermeldungen zeigt | Agent beim Gegenlesen, noch vor Build und Test | Bedingungen mit mehreren Fällen als Tabelle aufschreiben lassen, bevor Code entsteht. |
| 6 | Hinweistext auf der Löschseite behauptete, Geräte gelöschter Konten würden wieder herrenlos — der Sensor-Endpoint legt sie aber mit der alten `customer_id` neu an | Agent beim Prüfen des Sensor-Codes | UI-Texte sind Behauptungen über das System und müssen wie Code geprüft werden. Die Lücke steht jetzt im Backlog. |
| 7 | Erledigten Backlog-Punkt (AppHost-Referenz) im passenden Commit nicht gestrichen | Agent selbst einen Commit später | Kleinkram, aber typisch: Doku-Nebenwirkungen eines Commits vergisst der Agent leichter als Code. |

### Meine Erkenntnisse

- Worktrees zur parallelen Verarbeitung verschiedener Entwicklungsaufgaben sind sehr praktisch, aber erfordern klare Absprachen (wer macht was, in welchem Worktree).
- Merge-Konflikte lassen sich selbst auflösen, wenn man die Zusammenhänge versteht. Ich habe mich für einen Merge-Commit entschieden, weil ich die Historie der beiden parallelen Arbeiten erhalten wollte.
- FÜr Aufgaben welche die Benutzung von Tools erfordern die nur in einer Instanz laufen (z.B. Aspire) ist es sinnvoll, die Arbeit in einem Worktree zu bündeln und die andere Aufgabe in einem zweiten Worktree zu erledigen. So kann man die Tools parallel nutzen.
- Die Fehler 2 und 3 (CSRF und StatusCodePages) hätte ich selbst bemerkt, wenn ich die Negativtests durchgeführt hätte. Die anderen Fehler waren eher subtil und erforderten ein tieferes Verständnis der Funktionsweise der Frameworks.
- Ich habe in dieser Session wenige Zwischenfragen gestellt, weil die Erläuterungen gut waren und ich schon einiges an Vorwissen hatte. In Zukunft werde ich jedoch versuchen, noch mehr Zwischenfragen zu stellen, um mein Verständnis zu vertiefen.
- JWT, Refresh-Rotation und CSRF-Schutz sind komplexe Themen. Ich habe jetzt ein besseres Verständnis dafür, wie sie zusammenarbeiten, und könnte die Konzepte erklären, insbesondere wie Refresh-Tokens sicher gehandhabt werden und wie CSRF-Angriffe verhindert werden können.

### Belege
- PRs: #3 (1a), #4 (1b), #5 (1c), #6 (1d), #7 (1e), 1f: PR folgt
- Merge-Konflikt aufgelöst: `793e5c6` · CSRF-Schutz: `AccountEndpoints.cs` (Endpoint-Filter auf `/account`)
- Sitzungssperre: `MyOit.Portal/Services/Auth/TokenSessionManager.cs` · Tokens: `IotWelt.API/Services/TokenService.cs`
- Arbeitsaufträge der parallelen Sitzungen: `phase1-1c-api-members.md`, `phase1-1d-portal-auth.md` (Claude-Plans)
- Tag `v0.5.0` (nach dem Merge)

### Lebenslauf-tauglich (nur wenn selbst nachvollzogen)
- [x] Benutzerverwaltung mit ASP.NET Core Identity und eigener JWT-/Refresh-Token-Ausgabe (Token-Rotation, Widerruf)
- [x] Mandantenfähige Web API mit Rollen pro Konto (Owner/Editor/Reader) und Policy-basierter Autorisierung
- [x] Blazor Server mit Cookie-Sitzung und serverseitiger Token-Verwaltung, CSRF-Schutz
- [x] Parallele Entwicklung mit Git-Worktrees, Merge-Konflikte selbst aufgelöst
- [x] Agentic Coding: parallele Agent-Sitzungen mit schriftlichem Schnittstellen-Vertrag
