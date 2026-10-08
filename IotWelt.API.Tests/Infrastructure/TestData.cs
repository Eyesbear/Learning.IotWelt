namespace IotWelt.API.Tests.Infrastructure;

public static class TestData
{
    public const string Password = "Test-Passwort1";

    // Eindeutige Werte pro Test, passend zu den Spaltenlängen (HardwareId 14, CustomerId 16)
    public static string HardwareId() => Guid.NewGuid().ToString("N")[..14].ToUpperInvariant();
    public static string CustomerId() => Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
    public static string Email() => $"user-{Guid.NewGuid():N}@test.local";
    public static string DeviceName() => $"Sensor-{Guid.NewGuid():N}"[..20];
}
