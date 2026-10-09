using System.Net;
using System.Net.Http.Json;
using IotWelt.API.Tests.Infrastructure;
using IotWelt.Common.Admin;

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
    public async Task Logins_ansehen_nur_mit_Admin_Rolle()
    {
        var user = await factory.CreateUserAsync();

        var response = await user.Client.GetAsync("/api/admin/logins");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
