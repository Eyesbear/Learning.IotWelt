using System.ComponentModel.DataAnnotations;

namespace IotWelt.API.Models;

// Einladung einer Person per E-Mail in ein Konto (Stories B1–B3).
// Wie beim RefreshToken wird nur der SHA-256-Hash des Tokens gespeichert; das Token selbst
// steht ausschließlich im Link der Einladungsmail. Einmal nutzbar (AcceptedAt), 7 Tage gültig.
public class AccountInvitation
{
    public int Id { get; set; }

    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    [Required, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    // Nur Editor oder Reader — Owner wird man ausschließlich per Übertragung (C5)
    public AccountRole Role { get; set; }

    [Required, MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    public bool IsPending(DateTime utcNow) => AcceptedAt is null && ExpiresAt > utcNow;
}
