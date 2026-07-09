namespace IotWelt.API.Models;

public class SensorDataDto
{
    public string DeviceName { get; set; } = string.Empty;
    public double? Temperatur { get; set; }
    public double? RelativeFeuchte { get; set; }
    public bool WasserAlarm { get; set; }
    public string? CustomerId { get; set; }
}
