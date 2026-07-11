namespace IotWelt.Common;

public record CustomerProfileDto(
    string OwnerId,
    string CustomerId,
    string? DisplayName,
    string? Email);
