using IotWelt.API.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace IotWelt.API.Tests.Infrastructure;

// Startet die echte API im Speicher gegen einen SQL Server im Docker-Container.
// Ein Container für alle Tests (Collection-Fixture) — Tests isolieren sich über eindeutige IDs.
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();

        // Echte Migrationen anwenden — prüft nebenbei, dass sie auf einer leeren DB durchlaufen
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" statt "Development": appsettings.Development.json (LocalDB) wird nicht geladen
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:iotweltdb", _sql.GetConnectionString());

        builder.ConfigureTestServices(services =>
        {
            // Entra-JWT durch Test-Login ersetzen (Header X-Test-User / X-Test-Roles)
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public Task WithDbAsync(Func<AppDbContext, Task> action) =>
        WithDbAsync(async db => { await action(db); return true; });

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
