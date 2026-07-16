namespace IotWelt.Common;

public record AdminDeviceDto(
    int Id,
    string Name,
    string? Caption,
    string? Standort,
    string? Typ,
    uint? DeviceId,
    string? HardwareId,
    string? CustomerId,
    DateTime? ZuerstGesehen,
    string? OwnerDisplayName,
    string? OwnerEmail);
