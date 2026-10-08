using IotWelt.API.Models;
using Microsoft.AspNetCore.Authorization;

namespace IotWelt.API.Services;

// Autorisierung über die Konto-Rolle im Token (Rechte-Matrix in docs/user-stories/benutzerverwaltung.md).
// Jede Policy verlangt ein aktives Konto (Claim account_id) — ohne Konto gibt es keinen Datenzugriff,
// sonst würde der Filter "CustomerId == null" die herrenlosen Legacy-Geräte liefern.
public static class Policies
{
    public const string CanRead = nameof(CanRead);
    public const string CanEdit = nameof(CanEdit);
    public const string IsOwner = nameof(IsOwner);

    public const string AdminRole = "Admin";

    public static void AddAccountPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(CanRead, p => RequireAccountRole(p, AccountRole.Reader, AccountRole.Editor, AccountRole.Owner));
        options.AddPolicy(CanEdit, p => RequireAccountRole(p, AccountRole.Editor, AccountRole.Owner));
        options.AddPolicy(IsOwner, p => RequireAccountRole(p, AccountRole.Owner));
    }

    private static void RequireAccountRole(AuthorizationPolicyBuilder policy, params AccountRole[] roles) =>
        policy.RequireAuthenticatedUser()
              .RequireClaim(CurrentAccount.AccountIdClaim)
              .RequireClaim(CurrentAccount.AccountRoleClaim, roles.Select(r => r.ToString()));
}
