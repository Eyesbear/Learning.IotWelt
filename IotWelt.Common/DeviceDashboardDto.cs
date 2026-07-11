namespace IotWelt.Common;

public class DeviceDashboardDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Typ { get; set; }
    public string? Standort { get; set; }
    public DateTime? ZuerstGesehen { get; set; }
    public double? Temperatur { get; set; }
    public double? RelativeFeuchte { get; set; }
    public bool? Wassertank { get; set; }
    public DateTime? ZuletztGemeldet { get; set; }
    public string? Caption { get; set; }
    public string? CustomerId { get; set; }
}
