using IotWelt.API.Models;
using Microsoft.AspNetCore.Identity;

namespace IotWelt.API.Services;

public static class EmailServiceCollectionExtensions
{
    // Mit Smtp:Host echte Mails per SMTP, ohne nur Log-Ausgabe (Tests, Aspire ohne Mailpit).
    // Beide Interfaces zeigen immer auf denselben Sender.
    public static IServiceCollection AddEmailSenders(this IServiceCollection services, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration[$"{SmtpOptions.SectionName}:Host"]))
        {
            services.AddTransient<IEmailSender<AppUser>, LoggingEmailSender>();
            services.AddTransient<IInvitationEmailSender, LoggingEmailSender>();
        }
        else
        {
            services.AddOptions<SmtpOptions>().BindConfiguration(SmtpOptions.SectionName);
            services.AddTransient<IEmailSender<AppUser>, SmtpEmailSender>();
            services.AddTransient<IInvitationEmailSender, SmtpEmailSender>();
        }

        return services;
    }
}
