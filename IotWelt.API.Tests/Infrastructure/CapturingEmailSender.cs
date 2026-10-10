using System.Collections.Concurrent;
using IotWelt.API.Models;
using IotWelt.API.Services;
using Microsoft.AspNetCore.Identity;

namespace IotWelt.API.Tests.Infrastructure;

// Ersetzt im Test den LoggingEmailSender: merkt sich den zuletzt verschickten Link je Empfänger
public class CapturingEmailSender : IEmailSender<AppUser>, IInvitationEmailSender
{
    private readonly ConcurrentDictionary<string, string> _lastLink = new(StringComparer.OrdinalIgnoreCase);

    public string LastLinkFor(string email) =>
        _lastLink.TryGetValue(email, out var link) ? link : throw new InvalidOperationException($"Keine Mail an {email}");

    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink) => Store(email, confirmationLink);
    public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink) => Store(email, resetLink);
    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode) => Store(email, resetCode);
    public Task SendInvitationLinkAsync(string email, string accountName, AccountRole role, string invitationLink) => Store(email, invitationLink);

    private Task Store(string email, string link)
    {
        _lastLink[email] = link;
        return Task.CompletedTask;
    }
}
