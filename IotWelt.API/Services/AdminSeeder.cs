using IotWelt.API.Data;
using IotWelt.API.Models;
using Microsoft.AspNetCore.Identity;

namespace IotWelt.API.Services;

// Story E4: Legt beim Start den ersten System-Admin an, falls SeedAdmin:Email/Password konfiguriert sind
// (lokal User-Secrets, sonst Env-Vars). Ein vorhandener Login wird nur in die Rolle aufgenommen.
public static class AdminSeeder
{
    public static async Task SeedAdminAsync(this WebApplication app)
    {
        var email = app.Configuration["SeedAdmin:Email"];
        var password = app.Configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppUser>>();

        try
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var accounts = scope.ServiceProvider.GetRequiredService<AccountService>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (!await roles.RoleExistsAsync(Policies.AdminRole))
                await roles.CreateAsync(new IdentityRole(Policies.AdminRole));

            var admin = await users.FindByEmailAsync(email);
            if (admin is null)
            {
                admin = new AppUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = "Administrator" };
                var result = await users.CreateAsync(admin, password);
                if (!result.Succeeded)
                {
                    logger.LogError("Seed-Admin konnte nicht angelegt werden: {Errors}",
                        string.Join("; ", result.Errors.Select(e => e.Description)));
                    return;
                }

                await accounts.CreateOwnedAccountAsync(admin, "Administrator");
                await db.SaveChangesAsync();
                logger.LogInformation("Seed-Admin {Email} angelegt", email);
            }

            if (!await users.IsInRoleAsync(admin, Policies.AdminRole))
                await users.AddToRoleAsync(admin, Policies.AdminRole);
        }
        catch (Exception ex)
        {
            // Typisch: Datenbank noch nicht migriert → API trotzdem starten, Hinweis ins Log
            logger.LogError(ex, "Seed-Admin übersprungen — ist die Datenbank migriert (Update-Database)?");
        }
    }
}
