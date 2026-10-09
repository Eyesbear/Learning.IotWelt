namespace MyOit.Portal.Services.Auth;

// Die API meldet Identity-Fehler mit englischem Text, aber stabilem Code als Schlüssel im ValidationProblem.
// Codes: Microsoft.AspNetCore.Identity.IdentityErrorDescriber. Unbekannte Codes zeigen den API-Text.
public static class IdentityErrorTexts
{
    private static readonly Dictionary<string, string> Texts = new(StringComparer.Ordinal)
    {
        ["DuplicateEmail"] = "Mit dieser E-Mail-Adresse gibt es bereits ein Konto.",
        ["DuplicateUserName"] = "Mit dieser E-Mail-Adresse gibt es bereits ein Konto.",
        ["InvalidEmail"] = "Die E-Mail-Adresse ist ungültig.",
        ["InvalidUserName"] = "Die E-Mail-Adresse ist ungültig.",
        ["InvalidToken"] = "Der Link ist ungültig oder abgelaufen.",
        ["PasswordMismatch"] = "Das bisherige Passwort ist falsch.",
        ["PasswordTooShort"] = "Das Passwort ist zu kurz.",
        ["PasswordRequiresDigit"] = "Das Passwort muss mindestens eine Ziffer enthalten.",
        ["PasswordRequiresLower"] = "Das Passwort muss mindestens einen Kleinbuchstaben enthalten.",
        ["PasswordRequiresUpper"] = "Das Passwort muss mindestens einen Großbuchstaben enthalten.",
        ["PasswordRequiresNonAlphanumeric"] = "Das Passwort muss mindestens ein Sonderzeichen enthalten.",
        ["PasswordRequiresUniqueChars"] = "Das Passwort enthält zu wenige unterschiedliche Zeichen.",
    };

    public static bool TryTranslate(string code, out string text) =>
        Texts.TryGetValue(code, out text!);
}
