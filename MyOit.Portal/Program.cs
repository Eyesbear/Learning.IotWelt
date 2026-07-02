using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using MyOit.Portal.Components;
using MyOit.Portal.Services;
using Radzen;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi(
        new[] { builder.Configuration["IotWeltApi:Scopes"]! })
    .AddInMemoryTokenCaches();

builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();

var superAdminOid = builder.Configuration["SuperAdmin:Oid"] ?? "";
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdmin", policy =>
        policy.RequireAssertion(ctx =>
            GetOid(ctx.User) == superAdminOid));

    options.AddPolicy("CanManageUsers", policy =>
        policy.RequireAssertion(ctx =>
            GetOid(ctx.User) == superAdminOid || ctx.User.IsInRole("UserAdmin")));
});

builder.Services.AddScoped<GraphUserService>();
builder.Services.AddRadzenComponents();

builder.Services.AddHttpClient<IotWeltApiClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["IotWeltApi:BaseUrl"] ?? "http://iotwelt-api"));

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

app.Run();

static string? GetOid(ClaimsPrincipal user) =>
    user.FindFirstValue("oid") ??
    user.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");