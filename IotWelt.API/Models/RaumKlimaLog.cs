namespace IotWelt.API.Models;

public class RaumKlimaLog
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public double? Temperatur { get; set; }
    public double? RelativeFeuchte { get; set; }
    public double? BatterieSpannung { get; set; }
    public bool S1Input { get; set; }
    public bool S2Input { get; set; }
    public DateTime Zeitstempel { get; set; }
}
