using System.Net;
using System.Net.Http.Json;
using IotWelt.API.Models;
using IotWelt.API.Services;
using IotWelt.API.Tests.Infrastructure;
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
}
