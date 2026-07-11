namespace IotWelt.Common;

public record DeviceUpdateDto(string Name, string? Standort, string? Caption);

public record AdminDeviceUpdateDto(string Name, string? Standort, string? Caption, string? CustomerId);
