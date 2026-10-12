using IotWelt.API.Models;
using IotWelt.API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IotWelt.API.Tests;

// Reine DI-Tests ohne Datenbank: welcher Mail-Sender wird je nach Konfiguration registriert?
public class EmailSenderRegistrationTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddEmailSenders(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Without_smtp_host_uses_logging_sender()
    {
        using var provider = Build([]);

        Assert.IsType<LoggingEmailSender>(provider.GetRequiredService<IEmailSender<AppUser>>());
        Assert.IsType<LoggingEmailSender>(provider.GetRequiredService<IInvitationEmailSender>());
    }

    [Fact]
    public void With_smtp_host_uses_smtp_sender()
    {
        using var provider = Build(new() { ["Smtp:Host"] = "localhost", ["Smtp:Port"] = "1025", ["Smtp:Security"] = "None" });

        Assert.IsType<SmtpEmailSender>(provider.GetRequiredService<IEmailSender<AppUser>>());
        Assert.IsType<SmtpEmailSender>(provider.GetRequiredService<IInvitationEmailSender>());
    }
}
