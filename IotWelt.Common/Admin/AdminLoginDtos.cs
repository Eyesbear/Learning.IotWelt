namespace IotWelt.Common.Admin;

// Verträge der Login-Verwaltung für System-Admins (/api/admin/logins, Epic E)

// E1: ein Login mit allen Konten, denen er angehört
public record AdminLoginDto(
    string UserId,
    string Email,
    string? DisplayName,
    bool EmailConfirmed,
    bool IsAdmin,
    DateTimeOffset? LockedUntil,     // null = nicht gesperrt
    List<AdminLoginAccountDto> Accounts);

// Role: "Owner", "Editor", "Reader"
public record AdminLoginAccountDto(string CustomerId, string Name, string Role);
