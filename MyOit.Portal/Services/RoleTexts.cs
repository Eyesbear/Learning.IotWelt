namespace MyOit.Portal.Services;

// Anzeigenamen der Konto-Rollen (Rollennamen aus AccountRole der API, als Text in den DTOs)
public static class RoleTexts
{
    public const string Owner = "Owner";
    public const string Editor = "Editor";
    public const string Reader = "Reader";

    public static string ToDisplay(string role) => role switch
    {
        Owner => "Eigentümer",
        Editor => "Bearbeiter",
        Reader => "Leser",
        _ => role,
    };
}
