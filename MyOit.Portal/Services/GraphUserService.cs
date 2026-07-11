using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Identity.Web;
using Microsoft.Kiota.Abstractions.Authentication;
using System.Security.Claims;

namespace MyOit.Portal.Services;

public record UserRoleAssignment(string AssignmentId, string UserId, string DisplayName, string RoleName);
public record UserSearchResult(string UserId, string DisplayName, string Upn);

public class GraphUserService(
    ITokenAcquisition tokenAcquisition,
    IConfiguration config)
{
    private ServicePrincipal? _sp;

    private async Task<GraphServiceClient> CreateClientAsync()
    {
        // App-Level Token (Client Credentials) — zuverlässiger als delegiert für CIAM Directory-Ops
        var token = await tokenAcquisition.GetAccessTokenForAppAsync(
            "https://graph.microsoft.com/.default");
        return new GraphServiceClient(new BaseBearerTokenAuthenticationProvider(new StaticTokenProvider(token)));
    }

    private async Task<ServicePrincipal> GetServicePrincipalAsync(GraphServiceClient graph)
    {
        if (_sp is not null) return _sp;
        var clientId = config["AzureAd:ClientId"]!;
        var result = await graph.ServicePrincipals.GetAsync(req =>
        {
            req.QueryParameters.Filter = $"appId eq '{clientId}'";
            req.QueryParameters.Select = ["id", "appRoles"];
        });
        _sp = result!.Value!.First();
        return _sp;
    }

    public async Task<List<UserRoleAssignment>> GetAssignedUsersAsync()
    {
        var graph = await CreateClientAsync();
        var sp = await GetServicePrincipalAsync(graph);

        var roleMap = sp.AppRoles!
            .Where(r => r.Id.HasValue)
            .ToDictionary(r => r.Id!.Value, r => r.Value ?? "Unknown");

        var response = await graph.ServicePrincipals[sp.Id]
            .AppRoleAssignedTo
            .GetAsync(req => req.QueryParameters.Select =
                ["id", "principalId", "principalDisplayName", "appRoleId"]);

        return (response?.Value ?? [])
            .Where(a => a.AppRoleId.HasValue && a.AppRoleId != Guid.Empty)
            .Select(a => new UserRoleAssignment(
                a.Id!,
                a.PrincipalId!.Value.ToString(),
                a.PrincipalDisplayName ?? "(unbekannt)",
                roleMap.TryGetValue(a.AppRoleId!.Value, out var role) ? role : "Unbekannt"))
            .ToList();
    }

    public async Task<List<UserSearchResult>> SearchUsersAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2) return [];
        var graph = await CreateClientAsync();
        var safeQuery = query.Replace("'", "''");
        var response = await graph.Users.GetAsync(req =>
        {
            req.QueryParameters.Filter =
                $"startswith(displayName,'{safeQuery}') or startswith(userPrincipalName,'{safeQuery}')";
            req.QueryParameters.Select = ["id", "displayName", "userPrincipalName"];
            req.QueryParameters.Top = 10;
        });
        return (response?.Value ?? [])
            .Select(u => new UserSearchResult(u.Id!, u.DisplayName ?? "", u.UserPrincipalName ?? ""))
            .ToList();
    }

    public async Task AssignRoleAsync(string userId, string roleName)
    {
        var graph = await CreateClientAsync();
        var sp = await GetServicePrincipalAsync(graph);
        var role = sp.AppRoles!.First(r => r.Value == roleName);

        await graph.ServicePrincipals[sp.Id].AppRoleAssignedTo.PostAsync(new AppRoleAssignment
        {
            PrincipalId = Guid.Parse(userId),
            ResourceId = Guid.Parse(sp.Id!),
            AppRoleId = role.Id
        });
    }

    public async Task DeleteUserAsync(string userId)
    {
        var graph = await CreateClientAsync();
        await graph.Users[userId].DeleteAsync();
    }

    public async Task RemoveAssignmentAsync(string assignmentId)
    {
        var graph = await CreateClientAsync();
        var sp = await GetServicePrincipalAsync(graph);
        await graph.ServicePrincipals[sp.Id].AppRoleAssignedTo[assignmentId].DeleteAsync();
    }

    public bool IsAdmin(ClaimsPrincipal user) => user.IsInRole("Admin");

    private sealed class StaticTokenProvider(string token) : IAccessTokenProvider
    {
        public AllowedHostsValidator AllowedHostsValidator { get; } = new();

        public Task<string> GetAuthorizationTokenAsync(
            Uri uri,
            Dictionary<string, object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default) => Task.FromResult(token);
    }
}
