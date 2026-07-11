using System.ComponentModel.DataAnnotations;

namespace IotWelt.API.Models;

public class CustomerProfile
{
    public int Id { get; set; }

    // OID-Claim aus dem External ID JWT — stabiler Benutzerbezeichner
    [Required, MaxLength(500)]
    public string OwnerId { get; set; } = string.Empty;

    // 16-stellige ID für ESP32-Geräte
    [Required, MaxLength(16)]
    public string CustomerId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? DisplayName { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }
}
