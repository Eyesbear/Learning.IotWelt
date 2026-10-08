using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace IotWelt.API.Models;

// Login einer Person. Besitzt selbst keine Daten — Zugriff auf Konten über AccountMembership.
public class AppUser : IdentityUser
{
    [MaxLength(200)]
    public string? DisplayName { get; set; }

    // Zuletzt gewähltes Konto (Kontowechsel) — wird beim nächsten Login wieder aktiv
    public int? LastActiveAccountId { get; set; }
}
