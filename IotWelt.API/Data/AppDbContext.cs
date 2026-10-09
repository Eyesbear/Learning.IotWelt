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
    public DbSet<AccountInvitation> AccountInvitations => Set<AccountInvitation>();

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

            // Invariante "genau ein Owner pro Konto" als Sicherheitsnetz in der DB:
            // gefilterter eindeutiger Index — gilt nur für Zeilen mit Role = 'Owner'
            e.HasIndex(m => m.AccountId)
                .IsUnique()
                .HasFilter("[Role] = 'Owner'")
                .HasDatabaseName("IX_AccountMemberships_OneOwnerPerAccount");

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

        builder.Entity<AccountInvitation>(e =>
        {
            e.HasIndex(i => i.TokenHash).IsUnique();
            e.HasIndex(i => new { i.AccountId, i.Email });
            e.Property(i => i.Role).HasConversion<string>().HasMaxLength(16);

            // Konto gelöscht → offene und angenommene Einladungen verschwinden mit (C4)
            e.HasOne(i => i.Account).WithMany().OnDelete(DeleteBehavior.Cascade);
        });
    }
}
