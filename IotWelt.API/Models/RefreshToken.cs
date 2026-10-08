using System.ComponentModel.DataAnnotations;

namespace IotWelt.API.Models;

// Gespeichert wird nur der SHA-256-Hash — ein DB-Leak liefert keine verwendbaren Tokens.
// Rotation: jede Verwendung erzeugt ein neues Token (ReplacedByHash); wird ein bereits ersetztes
// Token erneut vorgelegt, gilt die ganze Kette als kompromittiert und wird widerrufen.
public class RefreshToken
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;

    [Required, MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    // Aktives Konto, für das das Token ausgestellt wurde (Kontowechsel erzeugt neues Token)
    public int? AccountId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    [MaxLength(64)]
    public string? ReplacedByHash { get; set; }

    public bool IsActive(DateTime utcNow) => RevokedAt is null && ExpiresAt > utcNow;
}
