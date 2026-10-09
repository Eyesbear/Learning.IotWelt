using System.Net;
using System.Net.Http.Json;
using IotWelt.API.Models;
using IotWelt.API.Services;
using IotWelt.API.Tests.Infrastructure;
using IotWelt.Common.Auth;
using IotWelt.Common.Members;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IotWelt.API.Tests;

// Mitglieder und Einladungen des aktiven Kontos (Epic B + C, nur Owner)
[Collection(ApiCollection.Name)]
public class MembersControllerTests(ApiFactory factory)
{
    [Fact]
    public async Task Owner_laedt_ein_und_sieht_Einladung_als_ausstehend()
    {
        var owner = await factory.CreateUserAsync();
        var email = TestData.Email();

        var (invitation, _) = await factory.InviteAsync(owner, email, "Editor");

        Assert.Equal("Editor", invitation.Role);
        Assert.False(invitation.IsExpired);
        Assert.Contains("/account/accept-invitation?token=", factory.Emails.LastLinkFor(email));

        var overview = await owner.Client.GetFromJsonAsync<MembersOverviewDto>("/api/members");
        var member = Assert.Single(overview!.Members);
        Assert.Equal(owner.Email, member.Email);
        Assert.Equal("Owner", member.Role);
        var open = Assert.Single(overview.Invitations);
        Assert.Equal(email, open.Email);
    }

    [Fact]
    public async Task Einladung_ist_7_Tage_gueltig_und_speichert_nur_den_Hash()
    {
        var owner = await factory.CreateUserAsync();
        var (invitation, token) = await factory.InviteAsync(owner, TestData.Email());

        Assert.Equal(TimeSpan.FromDays(7), invitation.ExpiresAt - invitation.CreatedAt);
        var stored = await factory.WithDbAsync(db => db.AccountInvitations.SingleAsync(i => i.Id == invitation.Id));
        Assert.NotEqual(token, stored.TokenHash);
        Assert.Equal(SecureToken.Hash(token), stored.TokenHash);
    }

    [Theory]
    [InlineData(AccountRole.Reader)]
    [InlineData(AccountRole.Editor)]
    public async Task Nur_der_Owner_verwaltet_Mitglieder(AccountRole role)
    {
        var owner = await factory.CreateUserAsync();
        var other = await factory.CreateUserAsync();
        await factory.AddMemberAsync(owner.CustomerId, other.UserId, role);
        var member = await factory.SwitchToAsync(other, owner.CustomerId);
        var (invitation, _) = await factory.InviteAsync(owner, TestData.Email());

        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.GetAsync("/api/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.PostAsJsonAsync("/api/members/invitations",
            new InviteMemberRequest(TestData.Email(), "Reader"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.DeleteAsync(
            $"/api/members/invitations/{invitation.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.PutAsJsonAsync(
            $"/api/members/{member.UserId}/role", new ChangeMemberRoleRequest("Editor"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.DeleteAsync(
            $"/api/members/{owner.UserId}")).StatusCode);
    }

    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    [InlineData("1")]
    [InlineData("")]
    public async Task Einladung_nur_als_Editor_oder_Reader(string role)
    {
        var owner = await factory.CreateUserAsync();

        var response = await owner.Client.PostAsJsonAsync("/api/members/invitations",
            new InviteMemberRequest(TestData.Email(), role));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Einladung_an_ungueltige_Adresse_wird_abgewiesen()
    {
        var owner = await factory.CreateUserAsync();

        var response = await owner.Client.PostAsJsonAsync("/api/members/invitations",
            new InviteMemberRequest("keine-adresse", "Reader"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bestehendes_Mitglied_kann_nicht_eingeladen_werden()
    {
        var owner = await factory.CreateUserAsync();
        var reader = await factory.CreateUserAsync();
        await factory.AddMemberAsync(owner.CustomerId, reader.UserId, AccountRole.Reader);

        // Schreibweise egal — auch der Owner selbst ist Mitglied
        foreach (var email in new[] { reader.Email.ToUpperInvariant(), owner.Email })
        {
            var response = await owner.Client.PostAsJsonAsync("/api/members/invitations",
                new InviteMemberRequest(email, "Editor"));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(MemberErrors.AlreadyMember, problem!.Title);
        }
    }

    [Fact]
    public async Task Erneute_Einladung_ersetzt_die_offene()
    {
        var owner = await factory.CreateUserAsync();
        var email = TestData.Email();

        var (first, firstToken) = await factory.InviteAsync(owner, email, "Reader");
        var (second, secondToken) = await factory.InviteAsync(owner, email.ToUpperInvariant(), "Editor");

        Assert.Equal(first.Id, second.Id);
        Assert.NotEqual(firstToken, secondToken);
        var overview = await owner.Client.GetFromJsonAsync<MembersOverviewDto>("/api/members");
        var open = Assert.Single(overview!.Invitations);
        Assert.Equal("Editor", open.Role);
        Assert.Equal(email, open.Email);
    }

    [Fact]
    public async Task Owner_zieht_Einladung_zurueck()
    {
        var owner = await factory.CreateUserAsync();
        var (invitation, _) = await factory.InviteAsync(owner, TestData.Email());

        var delete = await owner.Client.DeleteAsync($"/api/members/invitations/{invitation.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var overview = await owner.Client.GetFromJsonAsync<MembersOverviewDto>("/api/members");
        Assert.Empty(overview!.Invitations);
    }

    [Fact]
    public async Task Einladung_eines_fremden_Kontos_kann_nicht_zurueckgezogen_werden()
    {
        var owner = await factory.CreateUserAsync();
        var fremd = await factory.CreateUserAsync();
        var (invitation, _) = await factory.InviteAsync(owner, TestData.Email());

        var delete = await fremd.Client.DeleteAsync($"/api/members/invitations/{invitation.Id}");

        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        var overview = await owner.Client.GetFromJsonAsync<MembersOverviewDto>("/api/members");
        Assert.Single(overview!.Invitations);
    }

    [Fact]
    public async Task Rollenwechsel_wirkt_beim_naechsten_Refresh()
    {
        var owner = await factory.CreateUserAsync();
        var member = await factory.InviteAndAcceptAsync(owner, await factory.CreateUserAsync(), "Reader");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await member.Client.PostAsJsonAsync("/api/devices", new { name = "Vorher" })).StatusCode);

        var change = await owner.Client.PutAsJsonAsync($"/api/members/{member.UserId}/role",
            new ChangeMemberRoleRequest("Editor"));

        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        var overview = await owner.Client.GetFromJsonAsync<MembersOverviewDto>("/api/members");
        Assert.Contains(overview!.Members, m => m.UserId == member.UserId && m.Role == "Editor");

        var refreshed = await factory.RefreshAsync(member);
        var me = await refreshed.Client.GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.Equal(owner.CustomerId, me!.CustomerId);
        Assert.Equal("Editor", me.AccountRole);
        Assert.Equal(HttpStatusCode.Created,
            (await refreshed.Client.PostAsJsonAsync("/api/devices", new { name = "Nachher" })).StatusCode);
    }

    [Theory]
    [InlineData("Owner")]
    [InlineData("Chef")]
    public async Task Rolle_nur_Editor_oder_Reader(string role)
    {
        var owner = await factory.CreateUserAsync();
        var member = await factory.InviteAndAcceptAsync(owner, await factory.CreateUserAsync());

        var change = await owner.Client.PutAsJsonAsync($"/api/members/{member.UserId}/role",
            new ChangeMemberRoleRequest(role));

        Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);
    }

    [Fact]
    public async Task Owner_kann_sich_weder_herabstufen_noch_entfernen()
    {
        var owner = await factory.CreateUserAsync();

        var change = await owner.Client.PutAsJsonAsync($"/api/members/{owner.UserId}/role",
            new ChangeMemberRoleRequest("Editor"));
        var remove = await owner.Client.DeleteAsync($"/api/members/{owner.UserId}");

        foreach (var response in new[] { change, remove })
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(MemberErrors.OwnerMembership, problem!.Title);
        }
    }

    [Fact]
    public async Task Mitglieder_fremder_Konten_sind_nicht_erreichbar()
    {
        var owner = await factory.CreateUserAsync();
        var fremdOwner = await factory.CreateUserAsync();
        var fremdMember = await factory.InviteAndAcceptAsync(fremdOwner, await factory.CreateUserAsync());

        var change = await owner.Client.PutAsJsonAsync($"/api/members/{fremdMember.UserId}/role",
            new ChangeMemberRoleRequest("Editor"));
        var remove = await owner.Client.DeleteAsync($"/api/members/{fremdMember.UserId}");

        Assert.Equal(HttpStatusCode.NotFound, change.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
        var overview = await fremdOwner.Client.GetFromJsonAsync<MembersOverviewDto>("/api/members");
        Assert.Contains(overview!.Members, m => m.UserId == fremdMember.UserId && m.Role == "Reader");
    }

    [Fact]
    public async Task Entzogener_Zugriff_wirkt_beim_naechsten_Refresh()
    {
        var owner = await factory.CreateUserAsync();
        var member = await factory.InviteAndAcceptAsync(owner, await factory.CreateUserAsync());

        var remove = await owner.Client.DeleteAsync($"/api/members/{member.UserId}");

        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        var overview = await owner.Client.GetFromJsonAsync<MembersOverviewDto>("/api/members");
        Assert.DoesNotContain(overview!.Members, m => m.UserId == member.UserId);

        // Nach dem Refresh landet der Login wieder in seinem eigenen Konto — das fremde ist weg
        var refreshed = await factory.RefreshAsync(member);
        Assert.NotEqual(owner.CustomerId, refreshed.CustomerId);
        var accounts = await refreshed.Client.GetFromJsonAsync<List<AccountSummaryDto>>("/api/auth/accounts");
        Assert.DoesNotContain(accounts!, a => a.CustomerId == owner.CustomerId);
    }
}
