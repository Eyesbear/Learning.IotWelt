using System.ComponentModel.DataAnnotations;

namespace IotWelt.API.Models;

// Mandant: besitzt Geräte und Messwerte. Ersetzt das frühere CustomerProfile.
public class Account
{
    public int Id { get; set; }

    // 16-stellige Kennung, die die ESP32-Geräte als customer_id senden (Device.CustomerId)
    [Required, MaxLength(16)]
    public string CustomerId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public List<AccountMembership> Memberships { get; set; } = [];
}
