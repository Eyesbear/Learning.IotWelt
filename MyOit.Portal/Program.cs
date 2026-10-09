using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using MyOit.Portal.Components;
using MyOit.Portal.Models;
using MyOit.Portal.Services;
using MyOit.Portal.Services.Auth;
using Radzen;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

// Anmeldung per Cookie; die API-Tokens liegen serverseitig im ITokenStore, im Cookie nur die Sitzungs-ID
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "IotWelt.Portal.Auth";
        options.LoginPath = "/account/login";
        // Ablauf kommt fest vom Refresh-Token (PortalSignInService) — Sliding würde ihn darüber hinaus verlängern
        options.SlidingExpiration = false;
        options.EventsType = typeof(SessionCookieEvents);
    });
builder.Services.AddScoped<SessionCookieEvents>();

builder.Services.AddAuthorization();

builder.Services.AddRadzenComponents();

builder.Services.Configure<DashboardOptions>(
    builder.Configuration.GetSection(DashboardOptions.SectionName));

builder.Services.AddMemoryCache();
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITokenStore, InMemoryTokenStore>();
builder.Services.AddSingleton<TokenSessionManager>();
builder.Services.AddScoped<PortalSignInService>();
builder.Services.AddTransient<BearerTokenHandler>();

var apiBaseUrl = new Uri(builder.Configuration["IotWeltApi:BaseUrl"] ?? "http://iotwelt-api");

// Die ServiceDefaults wiederholen auch POSTs. Ein wiederholter Refresh schickt ein schon verbrauchtes
// Refresh-Token — die API widerruft dann alle Sitzungen. Daher hier: keine Wiederholung für POST & Co.
// RemoveAllResilienceHandlers ist als experimentell markiert, aber der dokumentierte Weg, die Defaults
// für einen einzelnen Client zu ersetzen.
#pragma warning disable EXTEXP0001
builder.Services.AddHttpClient<AuthApiClient>(client => client.BaseAddress = apiBaseUrl)
    .RemoveAllResilienceHandlers()
    .AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());
#pragma warning restore EXTEXP0001

builder.Services.AddHttpClient(IotWeltApiClient.HttpClientName, client => client.BaseAddress = apiBaseUrl)
    .AddApplicationScopeHandler()
    .AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddScoped<IotWeltApiClient>();

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

app.MapAccountEndpoints();

app.Run();
