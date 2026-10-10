using System.Net.Http.Headers;
using System.Net.Http.Json;
using IotWelt.API.Models;
using IotWelt.API.Services;
using IotWelt.Common.Auth;
using Microsoft.EntityFrameworkCore;

namespace IotWelt.API.Tests.Infrastructure;

// Ein angemeldeter Test-Login; Client schickt das Access-Token als Bearer-Header mit
public record TestUser(string UserId, string Email, string CustomerId, TokenResponse Tokens, HttpClient Client);

public static class TestUsers
{
    // Registriert über die API (Login + eigenes Konto), bestätigt die E-Mail per UserManager
    // (Abkürzung — der echte Bestätigungsweg hat einen eigenen Test) und meldet an.
    public static async Task<TestUser> CreateUserAsync(this ApiFactory factory, bool admin = false)
    {
        var email = TestData.Email();
        var register = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, TestData.Password, null));
        register.EnsureSuccessStatusCode();

        await factory.WithUserManagerAsync(async (users, roles) =>
        {
            var user = (await users.FindByEmailAsync(email))!;
            user.EmailConfirmed = true;
            await users.UpdateAsync(user);

            if (admin)
            {
                if (!await roles.RoleExistsAsync(Policies.AdminRole))
                    await roles.CreateAsync(new(Policies.AdminRole));
                await users.AddToRoleAsync(user, Policies.AdminRole);
            }
        });

        return await factory.LoginAsync(email);
    }

    public static async Task<TestUser> LoginAsync(this ApiFactory factory, string email, string password = TestData.Password)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var tokens = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        return await factory.AsTestUserAsync(email, tokens);
    }

    // Macht aus einem Token-Paar (nach Login, Refresh oder Kontowechsel) einen angemeldeten Test-Login
    public static async Task<TestUser> AsTestUserAsync(this ApiFactory factory, string email, TokenResponse tokens)
    {
        var client = factory.WithBearer(tokens.AccessToken);
        var me = (await client.GetFromJsonAsync<MeResponse>("/api/auth/me"))!;
        return new TestUser(me.UserId, email, me.CustomerId ?? string.Empty, tokens, client);
    }

    public static HttpClient WithBearer(this ApiFactory factory, string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    // Nimmt einen Login direkt per DB in ein fremdes Konto auf — schnelle Abkürzung für Tests,
    // in denen es nicht um Einladungen geht. Der echte Weg: factory.InviteAndAcceptAsync (TestInvitations).
    public static Task AddMemberAsync(this ApiFactory factory, string customerId, string userId, AccountRole role) =>
        factory.WithDbAsync(async db =>
        {
            var account = await db.Accounts.SingleAsync(a => a.CustomerId == customerId);
            db.AccountMemberships.Add(new AccountMembership
            {
                AccountId = account.Id, UserId = userId, Role = role, JoinedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

    // Token-Refresh wie im Client: Mitgliedschaft, Rolle und Sperre werden dabei neu aus der DB gelesen
    public static async Task<TestUser> RefreshAsync(this ApiFactory factory, TestUser user)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(user.Tokens.RefreshToken));
        response.EnsureSuccessStatusCode();
        var tokens = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        return await factory.AsTestUserAsync(user.Email, tokens);
    }

    // Wechselt per API in ein anderes Konto und liefert den Login im neuen Kontext
    public static async Task<TestUser> SwitchToAsync(this ApiFactory factory, TestUser user, string customerId)
    {
        var response = await user.Client.PostAsJsonAsync("/api/auth/switch-account",
            new SwitchAccountRequest(customerId, user.Tokens.RefreshToken));
        response.EnsureSuccessStatusCode();
        var tokens = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        return await factory.AsTestUserAsync(user.Email, tokens);
    }
}
