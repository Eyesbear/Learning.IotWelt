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

// Fehlercodes im "title" der ProblemDetails (wie AuthErrors)
public static class MemberErrors
{
    public const string AlreadyMember = "already_member";
}
