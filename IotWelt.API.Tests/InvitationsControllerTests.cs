using System.Net;
using System.Net.Http.Json;
using IotWelt.API.Tests.Infrastructure;
using IotWelt.Common.Auth;
using IotWelt.Common.Members;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IotWelt.API.Tests;

// Einladungslink ansehen und einlösen (Epic B)
[Collection(ApiCollection.Name)]
public class InvitationsControllerTests(ApiFactory factory)
{
    [Fact]
    public async Task Annahmeseite_zeigt_Konto_Rolle_und_Status_ohne_Anmeldung()
    {
        var owner = await factory.CreateUserAsync();
        var email = TestData.Email();
        var (_, token) = await factory.InviteAsync(owner, email, "Editor");

        var info = await factory.CreateClient().GetFromJsonAsync<InvitationInfoDto>($"/api/invitations/{token}");

        Assert.Equal(email, info!.Email);
        Assert.Equal("Editor", info.Role);
        Assert.Equal(InvitationStatus.Open, info.Status);
        Assert.False(info.HasLogin);
        Assert.False(string.IsNullOrEmpty(info.AccountName));
    }

    [Fact]
    public async Task Unbekanntes_Token_gibt_404()
    {
        var response = await factory.CreateClient().GetAsync("/api/invitations/gibt-es-nicht");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Vorhandener_Login_nimmt_an_und_sieht_Konto_im_Wechsler()
    {
        var owner = await factory.CreateUserAsync();
        var invitee = await factory.CreateUserAsync();
        var (_, token) = await factory.InviteAsync(owner, invitee.Email, "Editor");

        var info = await factory.CreateClient().GetFromJsonAsync<InvitationInfoDto>($"/api/invitations/{token}");
        Assert.True(info!.HasLogin);

        var accept = await invitee.Client.PostAsync($"/api/invitations/{token}/accept", null);

        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var accounts = await invitee.Client.GetFromJsonAsync<List<AccountSummaryDto>>("/api/auth/accounts");
        var joined = Assert.Single(accounts!, a => a.CustomerId == owner.CustomerId);
        Assert.Equal("Editor", joined.Role);

        var overview = await owner.Client.GetFromJsonAsync<MembersOverviewDto>("/api/members");
        Assert.Contains(overview!.Members, m => m.Email == invitee.Email && m.Role == "Editor");
        Assert.Empty(overview.Invitations);
    }

    [Fact]
    public async Task Angenommenes_Mitglied_arbeitet_im_Konto_mit_seiner_Rolle()
    {
        var owner = await factory.CreateUserAsync();
        var reader = await factory.InviteAndAcceptAsync(owner, await factory.CreateUserAsync(), "Reader");

        Assert.Equal(owner.CustomerId, reader.CustomerId);
        Assert.Equal(HttpStatusCode.OK, (await reader.Client.GetAsync("/api/devices")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await reader.Client.PostAsJsonAsync("/api/devices", new { name = "Nein" })).StatusCode);
    }

    [Fact]
    public async Task Einladung_ist_nur_einmal_nutzbar()
    {
        var owner = await factory.CreateUserAsync();
        var invitee = await factory.CreateUserAsync();
        var (_, token) = await factory.InviteAsync(owner, invitee.Email);
        await invitee.Client.PostAsync($"/api/invitations/{token}/accept", null);

        var again = await invitee.Client.PostAsync($"/api/invitations/{token}/accept", null);

        await AssertProblemAsync(again, HttpStatusCode.Gone, MemberErrors.InvitationInvalid);
        var info = await factory.CreateClient().GetFromJsonAsync<InvitationInfoDto>($"/api/invitations/{token}");
        Assert.Equal(InvitationStatus.Accepted, info!.Status);
    }

    [Fact]
    public async Task Abgelaufene_Einladung_kann_nicht_angenommen_werden()
    {
        var owner = await factory.CreateUserAsync();
        var invitee = await factory.CreateUserAsync();
        var (invitation, token) = await factory.InviteAsync(owner, invitee.Email);

        // Abkürzung statt Uhr vorstellen: Ablauf direkt in der DB in die Vergangenheit legen
        await factory.WithDbAsync(db => db.AccountInvitations
            .Where(i => i.Id == invitation.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.ExpiresAt, DateTime.UtcNow.AddMinutes(-1))));

        var accept = await invitee.Client.PostAsync($"/api/invitations/{token}/accept", null);

        await AssertProblemAsync(accept, HttpStatusCode.Gone, MemberErrors.InvitationInvalid);
        var info = await factory.CreateClient().GetFromJsonAsync<InvitationInfoDto>($"/api/invitations/{token}");
        Assert.Equal(InvitationStatus.Expired, info!.Status);
        var accounts = await invitee.Client.GetFromJsonAsync<List<AccountSummaryDto>>("/api/auth/accounts");
        Assert.DoesNotContain(accounts!, a => a.CustomerId == owner.CustomerId);
    }

    [Fact]
    public async Task Login_mit_anderer_EMail_kann_nicht_annehmen()
    {
        var owner = await factory.CreateUserAsync();
        var (_, token) = await factory.InviteAsync(owner, TestData.Email());
        var fremd = await factory.CreateUserAsync();

        var accept = await fremd.Client.PostAsync($"/api/invitations/{token}/accept", null);

        await AssertProblemAsync(accept, HttpStatusCode.Forbidden, MemberErrors.InvitationEmailMismatch);
        var accounts = await fremd.Client.GetFromJsonAsync<List<AccountSummaryDto>>("/api/auth/accounts");
        Assert.DoesNotContain(accounts!, a => a.CustomerId == owner.CustomerId);
    }

    [Fact]
    public async Task Annehmen_ohne_Anmeldung_gibt_401()
    {
        var owner = await factory.CreateUserAsync();
        var (_, token) = await factory.InviteAsync(owner, TestData.Email());

        var accept = await factory.CreateClient().PostAsync($"/api/invitations/{token}/accept", null);

        Assert.Equal(HttpStatusCode.Unauthorized, accept.StatusCode);
    }

    [Fact]
    public async Task Zurueckgezogene_und_ersetzte_Links_sind_ungueltig()
    {
        var owner = await factory.CreateUserAsync();
        var invitee = await factory.CreateUserAsync();

        var (revoked, revokedToken) = await factory.InviteAsync(owner, TestData.Email());
        await owner.Client.DeleteAsync($"/api/members/invitations/{revoked.Id}");

        var (_, replacedToken) = await factory.InviteAsync(owner, invitee.Email, "Reader");
        await factory.InviteAsync(owner, invitee.Email, "Editor");

        Assert.Equal(HttpStatusCode.NotFound,
            (await factory.CreateClient().GetAsync($"/api/invitations/{revokedToken}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await invitee.Client.PostAsync($"/api/invitations/{replacedToken}/accept", null)).StatusCode);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string title)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(title, problem!.Title);
    }
}
