using System.Security.Cryptography;
using IotWelt.API.Data;
using IotWelt.API.Models;
using Microsoft.EntityFrameworkCore;

namespace IotWelt.API.Services;

public class AccountService(AppDbContext db, TimeProvider time)
{
    private const string CustomerIdAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    // Legt ein neues Konto an und macht den Login zu dessen Owner (Story A1).
    // Speichert nicht — der Aufrufer entscheidet über SaveChanges/Transaktion.
    public async Task<Account> CreateOwnedAccountAsync(AppUser owner, string name)
    {
        var account = new Account
        {
            CustomerId = await NewCustomerIdAsync(),
            Name = name,
            CreatedAt = time.GetUtcNow().UtcDateTime
        };
        account.Memberships.Add(new AccountMembership
        {
            User = owner,
            Role = AccountRole.Owner,
            JoinedAt = account.CreatedAt
        });
        db.Accounts.Add(account);
        return account;
    }

    // Bestimmt das aktive Konto: bevorzugt das gewünschte, sonst das zuletzt genutzte,
    // sonst die älteste Mitgliedschaft. null = Login gehört keinem Konto (mehr) an.
    public async Task<AccountMembership?> ResolveActiveAsync(AppUser user, int? preferredAccountId = null)
    {
        var memberships = await db.AccountMemberships
            .Include(m => m.Account)
            .Where(m => m.UserId == user.Id)
            .OrderBy(m => m.JoinedAt)
            .ToListAsync();

        return memberships.FirstOrDefault(m => m.AccountId == preferredAccountId)
            ?? memberships.FirstOrDefault(m => m.AccountId == user.LastActiveAccountId)
            ?? memberships.FirstOrDefault();
    }

    public Task<List<AccountMembership>> GetMembershipsAsync(string userId) =>
        db.AccountMemberships
            .Include(m => m.Account)
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.JoinedAt)
            .ToListAsync();

    private async Task<string> NewCustomerIdAsync()
    {
        // 36^16 Möglichkeiten — Kollision praktisch ausgeschlossen, aber der eindeutige Index ist das Sicherheitsnetz
        string id;
        do id = RandomNumberGenerator.GetString(CustomerIdAlphabet, 16);
        while (await db.Accounts.AnyAsync(a => a.CustomerId == id));
        return id;
    }
}
