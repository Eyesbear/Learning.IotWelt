namespace IotWelt.Common.Members;

// Verträge der Mitgliederverwaltung (/api/members, Epic B + C) — wirken immer auf das aktive Konto.
// Rollen als Text: "Owner", "Editor", "Reader" (wie AccountSummaryDto.Role).

// C1: aktive Mitglieder und noch nicht angenommene Einladungen
public record MembersOverviewDto(List<MemberDto> Members, List<InvitationDto> Invitations);

public record MemberDto(string UserId, string Email, string? DisplayName, string Role, DateTime JoinedAt);

// IsExpired: Link ist abgelaufen — Owner kann dieselbe Adresse erneut einladen (ersetzt die Einladung)
public record InvitationDto(int Id, string Email, string Role, DateTime CreatedAt, DateTime ExpiresAt, bool IsExpired);

// B1: Role nur "Editor" oder "Reader" — Owner wird man ausschließlich per Übertragung
public record InviteMemberRequest(string Email, string Role);

// Annahmeseite (/api/invitations/{token}): Status siehe InvitationStatus.
// HasLogin: Für die E-Mail gibt es schon einen Login → anmelden und annehmen (B3), sonst Login anlegen (B2).
public record InvitationInfoDto(string AccountName, string Email, string Role, DateTime ExpiresAt, string Status, bool HasLogin);

// B2: Login über den Einladungslink anlegen — die E-Mail kommt aus der Einladung, nicht vom Client
public record RegisterFromInvitationRequest(string Password, string? DisplayName);

public static class InvitationStatus
{
    public const string Open = "open";
    public const string Expired = "expired";
    public const string Accepted = "accepted";
}

// Fehlercodes im "title" der ProblemDetails (wie AuthErrors)
public static class MemberErrors
{
    public const string AlreadyMember = "already_member";
    public const string InvitationInvalid = "invitation_invalid";             // abgelaufen oder schon angenommen (410)
    public const string InvitationEmailMismatch = "invitation_email_mismatch"; // angemeldet mit anderer E-Mail (403)
    public const string LoginExists = "login_exists";                         // B2, obwohl es schon einen Login gibt → B3 (409)
}
