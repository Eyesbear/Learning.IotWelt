using MailKit.Security;

namespace IotWelt.API.Services;

// Section "Smtp" — ist Host gesetzt, verschickt die API echte Mails (SmtpEmailSender), sonst nur ins Log.
// Password kommt NIE aus appsettings, sondern aus User-Secrets / Env-Var (Smtp__Password).
public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 25;

    // None (Mailpit), StartTls (Port 587), SslOnConnect (Port 465) oder Auto (MailKit entscheidet nach Port)
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.Auto;

    // Leer = ohne Anmeldung
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public string From { get; set; } = "IotWelt <noreply@iotwelt.local>";
}
