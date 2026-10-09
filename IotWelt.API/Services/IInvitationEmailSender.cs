using IotWelt.API.Models;

namespace IotWelt.API.Services;

// Eigenes Interface statt IEmailSender<AppUser>: Dessen Methoden verlangen einen AppUser,
// eine eingeladene Person hat aber oft noch gar keinen Login (Story B2).
public interface IInvitationEmailSender
{
    Task SendInvitationLinkAsync(string email, string accountName, AccountRole role, string invitationLink);
}
