# User Stories — Benutzerverwaltung (v2.0)

Status: **Entwurf** · Grundlage für Phase 1 · Stand 2026-10-06

## Begriffe
| Begriff | Bedeutung |
|---|---|
| **Konto** | Mandant mit eindeutiger `CustomerId`. Besitzt Geräte und Messwerte. |
| **Login** | Zugang einer Person (ASP.NET Identity User: E-Mail + Passwort). Besitzt selbst keine Daten. |
| **Mitgliedschaft** | Verbindung Login ↔ Konto mit einer **Konto-Rolle**. Ein Login kann Mitglied mehrerer Konten sein. |
| **Konto-Rolle** | `Owner` (alles), `Editor` (Geräte bearbeiten), `Reader` (nur lesen). |
| **Aktives Konto** | Das Konto, in dessen Kontext ein Login gerade arbeitet (Kontowechsel). |
| **System-Admin** | Betreiber der Plattform (Identity-Rolle `Admin`), unabhängig von Konten. |

## Grundsatzentscheidungen
- Dritte erhalten Zugang **ausschließlich per Einladungslink**; niemand kennt fremde Passwörter.
- Ein Login kann **mehreren Konten** angehören; Portal und App bieten einen **Kontowechsel**.
- Rollen: **Owner / Editor / Reader**.
- Ein Zugriff gilt für das **ganze Konto**, bis er entzogen wird (keine Befristung, keine Geräte-Auswahl — spätere Erweiterung möglich).

## Rechte-Matrix
| Aktion | Reader | Editor | Owner | System-Admin |
|---|:-:|:-:|:-:|:-:|
| Dashboard, Geräte, Verlauf ansehen | ✓ | ✓ | ✓ | ✓ (alle Konten) |
| Gerät umbenennen / Standort / Caption | – | ✓ | ✓ | ✓ |
| Gerät löschen | – | – | ✓ | ✓ |
| Mitglieder einladen / Rolle ändern / entfernen | – | – | ✓ | ✓ |
| Konto löschen | – | – | ✓ | ✓ |
| Logins sperren / System-Admins ernennen | – | – | – | ✓ |

---

## Epic A — Registrierung & Anmeldung

**A1 Registrieren**
Als neue Person möchte ich mich mit E-Mail und Passwort registrieren, damit ich ein eigenes Konto für meine Sensoren bekomme.
- Nach Registrierung existiert ein Login **und** ein neues Konto mit mir als `Owner`.
- E-Mail muss bestätigt werden, bevor ein Login möglich ist (lokal: Fake-Mailer schreibt Link ins Log).
- Passwortregeln: mind. 8 Zeichen, Ziffer, Groß-/Kleinbuchstabe.

**A2 Anmelden / Abmelden**
Als Login möchte ich mich in Portal und App anmelden und abmelden können.
- Anmeldung liefert Access-Token (kurzlebig, ~15 min) + Refresh-Token (rotierend, ~14 Tage).
- Abmelden widerruft den Refresh-Token serverseitig.
- Nach 5 Fehlversuchen wird der Login für 15 min gesperrt (Identity Lockout).

**A3 Passwort vergessen / ändern**
Als Login möchte ich mein Passwort per E-Mail-Link zurücksetzen bzw. angemeldet ändern können.
- Nach Passwortänderung werden alle Refresh-Tokens dieses Logins widerrufen.

**A4 Eigenen Login löschen**
Als Login möchte ich meinen Zugang löschen können.
- Bin ich **einziger Owner** eines Kontos, muss ich das Konto vorher löschen oder die Ownership übertragen (siehe C5) — sonst wird die Löschung abgelehnt.
- Mitgliedschaften in fremden Konten verschwinden mit dem Login.

## Epic B — Einladungen

**B1 Einladen**
Als Owner möchte ich eine Person per E-Mail mit einer Rolle (Editor/Reader) einladen, damit sie meine Daten sehen kann, ohne meine Zugangsdaten zu kennen.
- Einladung enthält einmal verwendbaren Link, gültig 7 Tage.
- Einladungen sind in der Mitgliederliste als „ausstehend“ sichtbar und können zurückgezogen werden.

**B2 Einladung annehmen — neue Person**
Als eingeladene Person ohne Login möchte ich über den Link einen Login anlegen und bin danach Mitglied des Kontos.
- Die E-Mail-Adresse ist durch die Einladung vorgegeben und gilt damit als bestätigt.
- *Offen:* Bekommt die Person zusätzlich ein eigenes leeres Konto? **Vorschlag: nein** — erst auf Wunsch („Eigenes Konto anlegen“).

**B3 Einladung annehmen — vorhandener Login**
Als Person mit bestehendem Login möchte ich die Einladung nach Anmeldung annehmen; das neue Konto erscheint in meinem Kontowechsler.

## Epic C — Mitglieder verwalten (Owner)

**C1 Mitglieder ansehen** — Liste mit E-Mail, Rolle, Status (aktiv/ausstehend), Beitrittsdatum.
**C2 Rolle ändern** — Editor ↔ Reader; wirkt spätestens beim nächsten Token-Refresh.
**C3 Zugriff entziehen** — Mitgliedschaft wird entfernt; betroffene Person sieht das Konto spätestens nach dem nächsten Token-Refresh nicht mehr.
**C4 Konto löschen** — löscht Geräte, Messwerte, Mitgliedschaften und offene Einladungen (mit Bestätigungsdialog).
**C5 Ownership übertragen** — *Vorschlag:* Ein Konto hat genau einen Owner; Übertragung an ein bestehendes Mitglied, bisheriger Owner wird Editor.

## Epic D — Kontowechsel

**D1 Konten anzeigen** — Als Login sehe ich alle Konten, denen ich angehöre, mit meiner Rolle.
**D2 Konto wechseln** — Ich wähle ein Konto; Dashboard, Geräteliste und Verlauf zeigen danach nur dessen Daten.
- Zuletzt gewähltes Konto wird beim nächsten Login wieder aktiv.
- Hat ein Login nur ein Konto, wird kein Wechsler angezeigt.

## Epic E — System-Administration

**E1 Alle Konten und Logins ansehen** (ersetzt heutiges `UserManagement` mit Graph).
**E2 Login sperren / entsperren** — gesperrter Login kann sich nicht anmelden, Refresh-Tokens werden widerrufen.
**E3 System-Admin ernennen / entziehen**.
**E4 Erster Admin** wird beim Start aus Konfiguration geseedet (E-Mail per User-Secret/Env-Var).

## Epic F — Geräte & ESP32 (Abgrenzung)
- Geräte gehören immer einem **Konto**, nie einem Login.
- `POST /api/sensor` ordnet weiterhin über `hardwareId` bzw. `customerId` zu — unverändert in Phase 1.

---

## Technische Skizze (für Phase 1, nicht verbindlich)
- Tabellen: Identity-Tabellen · `Accounts` (heute `CustomerProfile`, mit `CustomerId`) · `AccountMemberships (AccountId, UserId, Role, JoinedAt)` · `AccountInvitations (AccountId, Email, Role, TokenHash, ExpiresAt, AcceptedAt)` · `RefreshTokens (UserId, TokenHash, ExpiresAt, RevokedAt, ReplacedBy)`.
- Access-Token enthält `sub` (Login), `account_id` + `account_role` (aktives Konto), ggf. `role=Admin`.
- Kontowechsel = `POST /api/auth/switch-account` → neues Token-Paar für das gewählte Konto.
- `CustomerService` liefert künftig *aktives Konto + Rolle* aus dem Token und prüft die Mitgliedschaft.
- Autorisierung über Policies (`CanRead`, `CanEdit`, `IsOwner`) statt Rollen-Strings in Controllern.

## Offene Fragen
1. B2: Eigenes Konto für eingeladene Personen automatisch anlegen? (Vorschlag: nein)
2. C5: Genau ein Owner oder mehrere Owner pro Konto? (Vorschlag: genau einer)
3. Soll ein Reader Alarme (Wasseralarm) per E-Mail/Push bekommen können? (später, Phase 5/6)
