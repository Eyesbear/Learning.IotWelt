using System.Security.Claims;
using IotWelt.API.Models;

namespace IotWelt.API.Services;

// Liefert Login und aktives Konto des aktuellen Requests — ausschließlich aus den Token-Claims,
// ohne DB-Zugriff. Die API stellt diese Claims selbst aus (TokenService), sie sind signiert.
public class CurrentAccount(IHttpContextAccessor accessor)
{
    public const string UserIdClaim = "sub";
    public const string AccountIdClaim = "account_id";      // = Account.CustomerId des aktiven Kontos
    public const string AccountRoleClaim = "account_role";  // Owner / Editor / Reader

    private ClaimsPrincipal User => accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public string? UserId => User.FindFirstValue(UserIdClaim);

    // Kontokennung, nach der alle Gerätedaten gefiltert werden (Device.CustomerId)
    public string? CustomerId => User.FindFirstValue(AccountIdClaim);

    public AccountRole? Role =>
        Enum.TryParse<AccountRole>(User.FindFirstValue(AccountRoleClaim), out var role) ? role : null;

    public bool HasRole(AccountRole minimum) => Role >= minimum;
}
