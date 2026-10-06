#!/usr/bin/env bash
# Stop-Hook: Bevor Claude seine Antwort beendet, wird die Solution gebaut —
# aber nur, wenn sich C#-/Razor-/Projektdateien gegenüber HEAD geändert haben.
#   exit 0 → Claude darf aufhören
#   exit 2 → Build-Fehler gehen (stderr) an Claude zurück, Claude muss sie beheben
set -u

input=$(cat)

# Endlosschleife vermeiden: Wurde Claude bereits durch diesen Hook zurückgeschickt,
# nicht noch einmal blockieren (Claude meldet den Fehler dann dem Nutzer).
if echo "$input" | grep -q '"stop_hook_active"[[:space:]]*:[[:space:]]*true'; then
  exit 0
fi

cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0

# Geänderte oder neue Code-Dateien? (Doku-/Config-Änderungen lösen keinen Build aus)
if ! git status --porcelain --untracked-files=all | grep -qE '\.(cs|razor|csproj|slnx|props)$'; then
  exit 0
fi

output=$(dotnet build IotWelt.slnx -nologo -v q -clp:NoSummary 2>&1)
status=$?

if [ $status -eq 0 ]; then
  exit 0
fi

# Gesperrte DLLs (App läuft gerade in Visual Studio) sind kein Codefehler → nur Hinweis
if echo "$output" | grep -qE 'MSB3027|MSB3021|MSB3026'; then
  echo '{"systemMessage": "Build-Hook: Ausgabedateien gesperrt (läuft die App in VS?) – Build nicht geprüft."}'
  exit 0
fi

{
  echo "dotnet build ist fehlgeschlagen. Behebe die Fehler, bevor du die Aufgabe abschließt:"
  echo "$output" | grep -E ': (error|Fehler) ' | sed -E 's/ \[[^]]*\.csproj\]$//' | sort -u | head -30
} >&2
exit 2
