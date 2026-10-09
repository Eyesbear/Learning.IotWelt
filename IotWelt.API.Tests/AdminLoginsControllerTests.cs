using System.Net;
using System.Net.Http.Json;
using IotWelt.API.Tests.Infrastructure;
using IotWelt.Common.Admin;
using IotWelt.Common.Auth;
using Microsoft.AspNetCore.Mvc;

namespace IotWelt.API.Tests;

// Login-Verwaltung für System-Admins (Epic E)
[Collection(ApiCollection.Name)]
public class AdminLoginsControllerTests(ApiFactory factory)
{
    [Fact]
    public async Task Admin_sieht_Logins_mit_Konten_und_Rollen()
    {
        var owner = await factory.CreateUserAsync();
        var member = await factory.InviteAndAcceptAsync(owner, await factory.CreateUserAsync(), "Editor");
        var admin = await factory.CreateUserAsync(admin: true);

        var logins = await admin.Client.GetFromJsonAsync<List<AdminLoginDto>>("/api/admin/logins");

        var entry = Assert.Single(logins!, l => l.UserId == member.UserId);
        Assert.Equal(member.Email, entry.Email);
        Assert.False(entry.IsAdmin);
        Assert.Null(entry.LockedUntil);
        Assert.Equal(2, entry.Accounts.Count);   // eigenes Konto + eingeladenes
        Assert.Contains(entry.Accounts, a => a.CustomerId == owner.CustomerId && a.Role == "Editor");
        Assert.Contains(entry.Accounts, a => a.CustomerId != owner.CustomerId && a.Role == "Owner");

        Assert.True(Assert.Single(logins!, l => l.UserId == admin.UserId).IsAdmin);
    }

    [Fact]
    public async Task Login_Verwaltung_nur_mit_Admin_Rolle()
    {
        var user = await factory.CreateUserAsync();
        var other = await factory.CreateUserAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.GetAsync("/api/admin/logins")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.PostAsync($"/api/admin/logins/{other.UserId}/lock", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.PostAsync($"/api/admin/logins/{other.UserId}/unlock", null)).StatusCode);
    }

    [Fact]
    public async Task Gesperrter_Login_kann_weder_anmelden_noch_refreshen()
    {
        var admin = await factory.CreateUserAsync(admin: true);
        var user = await factory.CreateUserAsync();

        var lockResponse = await admin.Client.PostAsync($"/api/admin/logins/{user.UserId}/lock", null);

        Assert.Equal(HttpStatusCode.NoContent, lockResponse.StatusCode);
        await AssertProblemAsync(await LoginAsync(user.Email), HttpStatusCode.Unauthorized, AuthErrors.LockedOut);
        await AssertProblemAsync(await factory.CreateClient().PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(user.Tokens.RefreshToken)), HttpStatusCode.Unauthorized, AuthErrors.InvalidRefreshToken);

        var logins = await admin.Client.GetFromJsonAsync<List<AdminLoginDto>>("/api/admin/logins");
        Assert.NotNull(Assert.Single(logins!, l => l.UserId == user.UserId).LockedUntil);
    }

    [Fact]
    public async Task Entsperrter_Login_kann_sich_wieder_anmelden()
    {
        var admin = await factory.CreateUserAsync(admin: true);
        var user = await factory.CreateUserAsync();
        await admin.Client.PostAsync($"/api/admin/logins/{user.UserId}/lock", null);

        var unlock = await admin.Client.PostAsync($"/api/admin/logins/{user.UserId}/unlock", null);

        Assert.Equal(HttpStatusCode.NoContent, unlock.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(user.Email)).StatusCode);
        var logins = await admin.Client.GetFromJsonAsync<List<AdminLoginDto>>("/api/admin/logins");
        Assert.Null(Assert.Single(logins!, l => l.UserId == user.UserId).LockedUntil);
    }

    [Fact]
    public async Task Admin_kann_sich_nicht_selbst_sperren()
    {
        var admin = await factory.CreateUserAsync(admin: true);

        var response = await admin.Client.PostAsync($"/api/admin/logins/{admin.UserId}/lock", null);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, AdminLoginErrors.SelfAction);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(admin.Email)).StatusCode);
    }

    [Fact]
    public async Task Unbekannter_Login_gibt_404()
    {
        var admin = await factory.CreateUserAsync(admin: true);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.PostAsync("/api/admin/logins/gibt-es-nicht/lock", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.PostAsync("/api/admin/logins/gibt-es-nicht/unlock", null)).StatusCode);
    }

    private Task<HttpResponseMessage> LoginAsync(string email) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(email, TestData.Password));

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string title)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(title, problem!.Title);
    }
}
