# Backlog — bekannte Lücken und Folgepunkte

Sammelstelle für Dinge, die beim Umsetzen aufgefallen, aber bewusst nicht im selben PR behoben sind.
Erledigte Punkte werden gestrichen und im CHANGELOG erwähnt.

## API

- **Sensor legt Geräte für gelöschte Konten an** *(gefunden in 1e, `Delete.razor`)*
  Nach dem Löschen eines Kontos (C4) meldet ein weiter sendender ESP32 sich mit der alten `customer_id`;
  `POST /api/sensor` legt das Gerät dann neu an, ohne zu prüfen, ob das Konto existiert → verwaistes Gerät.
  Idee: unbekannte `customer_id` wie „herrenlos“ behandeln (Gerät ohne Besitzer anlegen, Warnung loggen).
  Kandidat für einen kleinen `fix/`-PR mit Test.
- **Kein Endpoint „eigenes Konto anlegen“** *(aus 1c)* — ein Login ohne Konto (nach C4, nach Entzug aller
  Mitgliedschaften oder per Einladung registriert und dann entfernt) kommt aus diesem Zustand nicht heraus.
- **Alter Owner kann nach Übertragung (C5) noch einladen** *(aus 1c)* — bis zu 15 min mit dem alten Token.
  Löschen und Übertragen prüfen schon gegen die DB, Einladen noch nicht.
- **Bestätigungsmail erneut senden** fehlt *(aus 1d)*.
- **E-Mail > 100 Zeichen ohne Anzeigenamen → 500** *(aus 1d)* — `Account.Name` ist `nvarchar(100)`.
- **Reset-Code prüfen ohne Verbrauch** fehlt *(aus 1d)* — die Reset-Seite merkt erst beim Absenden,
  dass der Link abgelaufen ist.

## Portal

- **Logins ohne Konto** *(gefunden in 1e)* — Dashboard, Geräte und Profil setzen ein aktives Konto voraus.
  Braucht eine Hinweisseite („Sie gehören keinem Konto an“) und den API-Endpoint oben.
- **Kein Portal-Testprojekt** *(aus 1d)* — z. B. bUnit für Komponenten, `WebApplicationFactory` für die
  Formular-Endpoints (CSRF-Schutz wurde bisher nur manuell geprüft).

## Produktion (Phase 4)

- **Kein echter E-Mail-Versand** — `LoggingEmailSender` schreibt Bestätigungs-, Reset- und Einladungslinks nur ins Log.
  Für Staging/Produktion einen SMTP-Sender (myASP.NET) anbinden.
- **Portal-Sitzungen nur im Speicher** — Neustart oder App-Pool-Recycle meldet alle ab. Persistenten `ITokenStore`
  und dauerhaft abgelegte Data-Protection-Schlüssel vorsehen (auch für Docker-Staging relevant).
