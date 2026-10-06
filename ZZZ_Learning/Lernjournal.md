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
