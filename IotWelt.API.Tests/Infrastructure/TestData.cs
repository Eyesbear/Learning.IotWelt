namespace IotWelt.API.Tests.Infrastructure;

public static class TestData
{
    // Eindeutige Werte pro Test, passend zu den Spaltenlängen (HardwareId 14, CustomerId 16)
    public static string HardwareId() => Guid.NewGuid().ToString("N")[..14].ToUpperInvariant();
    public static string CustomerId() => Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
    public static string UserId() => $"user-{Guid.NewGuid():N}";
    public static string DeviceName() => $"Sensor-{Guid.NewGuid():N}"[..20];

    public static HttpClient AsUser(this ApiFactory factory, string userId, params string[] roles)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
        if (roles.Length > 0)
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        return client;
    }
}
