using System.Net;
using System.Net.Http.Json;
using IotWelt.API.Tests.Infrastructure;
using IotWelt.Common.Admin;
using IotWelt.Common.Auth;
using IotWelt.Common.Members;
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
        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.PutAsync($"/api/admin/logins/{user.UserId}/admin", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.DeleteAsync($"/api/admin/logins/{other.UserId}/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.DeleteAsync($"/api/admin/logins/{other.UserId}")).StatusCode);
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
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.PutAsync("/api/admin/logins/gibt-es-nicht/admin", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.DeleteAsync("/api/admin/logins/gibt-es-nicht/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.Client.DeleteAsync("/api/admin/logins/gibt-es-nicht")).StatusCode);
    }

    [Fact]
    public async Task Ernannter_Admin_bekommt_die_Rolle_beim_naechsten_Refresh()
    {
        var admin = await factory.CreateUserAsync(admin: true);
        var user = await factory.CreateUserAsync();

        var grant = await admin.Client.PutAsync($"/api/admin/logins/{user.UserId}/admin", null);

        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);
        var refreshed = await factory.RefreshAsync(user);
        var me = await refreshed.Client.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.True(me!.IsAdmin);
        Assert.Equal(HttpStatusCode.OK, (await refreshed.Client.GetAsync("/api/admin/logins")).StatusCode);
    }

    [Fact]
    public async Task Entzogene_Admin_Rolle_wirkt_beim_naechsten_Refresh()
    {
        var admin = await factory.CreateUserAsync(admin: true);
        var other = await factory.CreateUserAsync(admin: true);

        var revoke = await admin.Client.DeleteAsync($"/api/admin/logins/{other.UserId}/admin");

        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        var refreshed = await factory.RefreshAsync(other);
        Assert.Equal(HttpStatusCode.Forbidden, (await refreshed.Client.GetAsync("/api/admin/logins")).StatusCode);
        var logins = await admin.Client.GetFromJsonAsync<List<AdminLoginDto>>("/api/admin/logins");
        Assert.False(Assert.Single(logins!, l => l.UserId == other.UserId).IsAdmin);
    }

    [Fact]
    public async Task Admin_kann_sich_nicht_selbst_entmachten_oder_loeschen()
    {
        var admin = await factory.CreateUserAsync(admin: true);

        await AssertProblemAsync(await admin.Client.DeleteAsync($"/api/admin/logins/{admin.UserId}/admin"),
            HttpStatusCode.Conflict, AdminLoginErrors.SelfAction);
        await AssertProblemAsync(await admin.Client.DeleteAsync($"/api/admin/logins/{admin.UserId}"),
            HttpStatusCode.Conflict, AdminLoginErrors.SelfAction);

        var refreshed = await factory.RefreshAsync(admin);
        Assert.Equal(HttpStatusCode.OK, (await refreshed.Client.GetAsync("/api/admin/logins")).StatusCode);
    }

    [Fact]
    public async Task Admin_loescht_Login_ohne_eigenes_Konto()
    {
        var admin = await factory.CreateUserAsync(admin: true);
        var owner = await factory.CreateUserAsync();
        var member = await factory.CreateUserAsync();
        var (_, token) = await factory.InviteAsync(owner, member.Email);
        await member.Client.PostAsync($"/api/invitations/{token}/accept", null);
        await member.Client.DeleteAsync("/api/members/account");   // eigenes Konto weg → nur noch Reader bei owner

        var delete = await admin.Client.DeleteAsync($"/api/admin/logins/{member.UserId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(member.Email)).StatusCode);
        var overview = await owner.Client.GetFromJsonAsync<MembersOverviewDto>("/api/members");
        Assert.DoesNotContain(overview!.Members, m => m.UserId == member.UserId);
    }

    [Fact]
    public async Task Admin_kann_Owner_Login_nicht_loeschen()
    {
        var admin = await factory.CreateUserAsync(admin: true);
        var owner = await factory.CreateUserAsync();

        var delete = await admin.Client.DeleteAsync($"/api/admin/logins/{owner.UserId}");

        await AssertProblemAsync(delete, HttpStatusCode.Conflict, DeleteLoginErrors.OwnsAccounts);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(owner.Email)).StatusCode);
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
