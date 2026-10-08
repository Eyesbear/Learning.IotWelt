using IotWelt.API.Data;
using IotWelt.API.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace IotWelt.API.Tests.Infrastructure;

// Startet die echte API im Speicher gegen einen SQL Server im Docker-Container.
// Ein Container für alle Tests (Collection-Fixture) — Tests isolieren sich über eindeutige IDs.
// Anmeldung wie in echt: register → login → Bearer-Token (siehe TestUsers).
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Fester Schlüssel nur für Tests (32 Bytes, Base64) — hat mit echten Umgebungen nichts zu tun
    private const string TestSigningKey = "VGVzdC1TY2hsdWVzc2VsLW51ci1mdWVyLVRlc3RzLTMyQg==";

    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    // Fängt Bestätigungs- und Reset-Links ab, die sonst nur ins Log geschrieben würden
    public CapturingEmailSender Emails { get; } = new();

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();

        // Echte Migrationen anwenden — prüft nebenbei, dass sie auf einer leeren DB durchlaufen
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" statt "Development": appsettings.Development.json (LocalDB) und User-Secrets werden nicht geladen
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:iotweltdb", _sql.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);

        builder.ConfigureTestServices(services =>
            services.AddSingleton<IEmailSender<AppUser>>(Emails));
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public Task WithDbAsync(Func<AppDbContext, Task> action) =>
        WithDbAsync(async db => { await action(db); return true; });

    public async Task WithUserManagerAsync(Func<UserManager<AppUser>, RoleManager<IdentityRole>, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(
            scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(),
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _sql.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "API";
}
