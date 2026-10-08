using System.Collections.Concurrent;
using IotWelt.API.Models;
using Microsoft.AspNetCore.Identity;

namespace IotWelt.API.Tests.Infrastructure;

// Ersetzt im Test den LoggingEmailSender: merkt sich den zuletzt verschickten Link je Empfänger
public class CapturingEmailSender : IEmailSender<AppUser>
{
    private readonly ConcurrentDictionary<string, string> _lastLink = new(StringComparer.OrdinalIgnoreCase);

    public string LastLinkFor(string email) =>
        _lastLink.TryGetValue(email, out var link) ? link : throw new InvalidOperationException($"Keine Mail an {email}");

    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink) => Store(email, confirmationLink);
    public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink) => Store(email, resetLink);
    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode) => Store(email, resetCode);

    private Task Store(string email, string link)
    {
        _lastLink[email] = link;
        return Task.CompletedTask;
    }
}
