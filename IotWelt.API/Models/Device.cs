using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IotWelt.API.Models;

public class Device
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Typ { get; set; }

    public uint? DeviceId { get; set; }

    [MaxLength(24)]
    [Column(TypeName = "nvarchar(24)")]
    public string? Standort { get; set; }

    public DateTime? ZuerstGesehen { get; set; }

    [MaxLength(16)]
    public string? CustomerId { get; set; }

    [MaxLength(200)]
    public string? Caption { get; set; }
}
