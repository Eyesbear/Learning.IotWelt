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

    [Fact]
    public async Task Neue_Person_legt_Login_an_und_ist_nur_Mitglied_des_einladenden_Kontos()
    {
        var owner = await factory.CreateUserAsync();
        var email = TestData.Email();
        var (_, token) = await factory.InviteAsync(owner, email, "Editor");

        var response = await factory.CreateClient().PostAsJsonAsync($"/api/invitations/{token}/register",
            new RegisterFromInvitationRequest(TestData.Password, "Gast"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        var invitee = await factory.AsTestUserAsync(email, tokens);
        Assert.Equal(owner.CustomerId, invitee.CustomerId);

        // Kein eigenes leeres Konto — nur die Mitgliedschaft aus der Einladung
        var accounts = await invitee.Client.GetFromJsonAsync<List<AccountSummaryDto>>("/api/auth/accounts");
        var account = Assert.Single(accounts!);
        Assert.Equal("Editor", account.Role);
        Assert.True(account.IsActive);
    }

    [Fact]
    public async Task Ueber_Einladung_angelegter_Login_kann_sich_ohne_Bestaetigungsmail_anmelden()
    {
        var owner = await factory.CreateUserAsync();
        var email = TestData.Email();
        var (_, token) = await factory.InviteAsync(owner, email);
        await factory.CreateClient().PostAsJsonAsync($"/api/invitations/{token}/register",
            new RegisterFromInvitationRequest(TestData.Password, null));

        var login = await factory.LoginAsync(email);

        Assert.Equal(owner.CustomerId, login.CustomerId);
    }

    [Fact]
    public async Task Registrieren_mit_vorhandenem_Login_gibt_409()
    {
        var owner = await factory.CreateUserAsync();
        var invitee = await factory.CreateUserAsync();
        var (_, token) = await factory.InviteAsync(owner, invitee.Email);

        var response = await factory.CreateClient().PostAsJsonAsync($"/api/invitations/{token}/register",
            new RegisterFromInvitationRequest(TestData.Password, null));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, MemberErrors.LoginExists);
    }

    [Fact]
    public async Task Ungueltiges_Passwort_verbraucht_die_Einladung_nicht()
    {
        var owner = await factory.CreateUserAsync();
        var email = TestData.Email();
        var (_, token) = await factory.InviteAsync(owner, email);

        var response = await factory.CreateClient().PostAsJsonAsync($"/api/invitations/{token}/register",
            new RegisterFromInvitationRequest("kurz", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var info = await factory.CreateClient().GetFromJsonAsync<InvitationInfoDto>($"/api/invitations/{token}");
        Assert.Equal(InvitationStatus.Open, info!.Status);
        Assert.False(info.HasLogin);
    }

    [Fact]
    public async Task Registrieren_mit_benutzter_oder_abgelaufener_Einladung_gibt_410()
    {
        var owner = await factory.CreateUserAsync();
        var (_, usedToken) = await factory.InviteAsync(owner, TestData.Email());
        await factory.CreateClient().PostAsJsonAsync($"/api/invitations/{usedToken}/register",
            new RegisterFromInvitationRequest(TestData.Password, null));

        var expiredEmail = TestData.Email();
        var (expired, expiredToken) = await factory.InviteAsync(owner, expiredEmail);
        await factory.WithDbAsync(db => db.AccountInvitations
            .Where(i => i.Id == expired.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.ExpiresAt, DateTime.UtcNow.AddMinutes(-1))));

        foreach (var token in new[] { usedToken, expiredToken })
        {
            var response = await factory.CreateClient().PostAsJsonAsync($"/api/invitations/{token}/register",
                new RegisterFromInvitationRequest(TestData.Password, null));
            await AssertProblemAsync(response, HttpStatusCode.Gone, MemberErrors.InvitationInvalid);
        }
        var info = await factory.CreateClient().GetFromJsonAsync<InvitationInfoDto>($"/api/invitations/{expiredToken}");
        Assert.False(info!.HasLogin);   // für die abgelaufene Einladung wurde kein Login angelegt
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string title)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(title, problem!.Title);
    }
}
