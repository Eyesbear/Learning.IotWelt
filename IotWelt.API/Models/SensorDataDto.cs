using System.Text.Json.Serialization;

namespace IotWelt.API.Models;

public class SensorDataDto
{
    public string DeviceName { get; set; } = string.Empty;
    public string? HardwareId { get; set; }
    public double? Temperatur { get; set; }
    public double? RelativeFeuchte { get; set; }
    public bool WasserAlarm { get; set; }
    [JsonPropertyName("customer_id")]
    public string? CustomerId { get; set; }
}
