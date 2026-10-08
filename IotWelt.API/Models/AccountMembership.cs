namespace IotWelt.API.Models;

// Reihenfolge ist Absicht: höhere Rolle = mehr Rechte (Vergleich per >=)
public enum AccountRole
{
    Reader = 0,
    Editor = 1,
    Owner = 2
}

// Verbindung Login ↔ Konto. Rechte hängen an der Mitgliedschaft, nicht am Login.
public class AccountMembership
{
    public int Id { get; set; }

    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;

    public AccountRole Role { get; set; }

    public DateTime JoinedAt { get; set; }
}
