using System.Text.Json.Serialization;

namespace IotWelt.API.Models;

public class SensorDataDto
{
    public string DeviceName { get; set; } = string.Empty;
    public string? HardwareId { get; set; }
    public double? Temperatur { get; set; }
    public double? RelativeFeuchte { get; set; }
    /// <summary>Spannung des 18650-Akkus in Volt; null = Gerät ohne Akku (oder ältere Firmware).</summary>
    public double? BatterieSpannung { get; set; }
    /// <summary>Kontakteingang S1 (true = Kontakt aktiv). Fehlt das Feld (ältere Firmware), gilt false.</summary>
    public bool S1Input { get; set; }
    /// <summary>Kontakteingang S2 (true = Kontakt aktiv).</summary>
    public bool S2Input { get; set; }
    [JsonPropertyName("customer_id")]
    public string? CustomerId { get; set; }
}
