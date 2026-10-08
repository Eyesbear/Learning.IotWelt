using IotWelt.API.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IotWelt.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<RaumKlimaLog> RaumKlimaLogs => Set<RaumKlimaLog>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountMembership> AccountMemberships => Set<AccountMembership>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity-Tabellen (AspNetUsers, AspNetRoles, …) zuerst konfigurieren lassen
        base.OnModelCreating(builder);

        builder.Entity<Account>()
            .HasIndex(a => a.CustomerId)
            .IsUnique();

        builder.Entity<AccountMembership>(e =>
        {
            e.HasIndex(m => new { m.AccountId, m.UserId }).IsUnique();

            // Als Text speichern — in SSMS lesbar und unabhängig von der Enum-Reihenfolge
            e.Property(m => m.Role).HasConversion<string>().HasMaxLength(16);

            e.HasOne(m => m.Account).WithMany(a => a.Memberships).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.User).WithMany().OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RefreshToken>(e =>
        {
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasOne(t => t.User).WithMany().OnDelete(DeleteBehavior.Cascade);
        });
    }
}
