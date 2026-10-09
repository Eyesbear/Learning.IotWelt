using System.Net.Http.Json;
using IotWelt.Common.Members;
using Microsoft.AspNetCore.WebUtilities;

namespace IotWelt.API.Tests.Infrastructure;

public static class TestInvitations
{
    // Owner lädt per API ein; liefert die Einladung und das Token aus dem abgefangenen Mail-Link
    public static async Task<(InvitationDto Invitation, string Token)> InviteAsync(
        this ApiFactory factory, TestUser owner, string email, string role = "Reader")
    {
        var response = await owner.Client.PostAsJsonAsync("/api/members/invitations", new InviteMemberRequest(email, role));
        response.EnsureSuccessStatusCode();
        var invitation = (await response.Content.ReadFromJsonAsync<InvitationDto>())!;
        return (invitation, TokenFrom(factory.Emails.LastLinkFor(email)));
    }

    public static string TokenFrom(string link) =>
        QueryHelpers.ParseQuery(new Uri(link).Query)["token"].ToString();
}
