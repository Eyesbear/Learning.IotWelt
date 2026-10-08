using System.Net;
using System.Net.Http.Json;
using IotWelt.API.Models;
using IotWelt.API.Tests.Infrastructure;
using IotWelt.Common.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace IotWelt.API.Tests;

// Registrierung, Login, Token-Rotation, Kontowechsel und Passwort-Flows (Epic A + D der User Stories)
[Collection(ApiCollection.Name)]
public class AuthControllerTests(ApiFactory factory)
{
    private readonly HttpClient _anonymous = factory.CreateClient();

    private Task<HttpResponseMessage> LoginAsync(string email, string password = TestData.Password) =>
        _anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _anonymous.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(refreshToken));

    private static async Task AssertAuthErrorAsync(HttpResponseMessage response, string error)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(error, problem!.Title);
    }

    // Query-Parameter aus einem abgefangenen Mail-Link (Bestätigung, Passwort-Reset)
    private static Dictionary<string, string> LinkParameters(string link) =>
        QueryHelpers.ParseQuery(new Uri(link).Query).ToDictionary(p => p.Key, p => p.Value.ToString());

    // --- A1/A2: Registrierung und Login ---

    [Fact]
    public async Task Registrierung_Bestaetigungslink_und_Login_ergeben_Owner_des_eigenen_Kontos()
    {
        var email = TestData.Email();
        var register = await _anonymous.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, TestData.Password, "Anna"));
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        await AssertAuthErrorAsync(await LoginAsync(email), AuthErrors.EmailNotConfirmed);

        var link = LinkParameters(factory.Emails.LastLinkFor(email));
        var confirm = await _anonymous.PostAsJsonAsync("/api/auth/confirm-email", new ConfirmEmailRequest(link["userId"], link["code"]));
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var login = await LoginAsync(email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;

        var me = await factory.WithBearer(tokens.AccessToken).GetFromJsonAsync<MeResponse>("/api/auth/me");
        Assert.Equal(email, me!.Email);
        Assert.Equal("Anna", me.DisplayName);
        Assert.False(me.IsAdmin);
        Assert.Matches("^[A-Z0-9]{16}$", me.CustomerId);
        Assert.Equal(nameof(AccountRole.Owner), me.AccountRole);
    }

    [Fact]
    public async Task Doppelte_Registrierung_wird_abgelehnt()
    {
        var user = await factory.CreateUserAsync();

        var response = await _anonymous.PostAsJsonAsync("/api/auth/register", new RegisterRequest(user.Email, TestData.Password, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Falsches_Passwort_und_unbekannte_Adresse_geben_dieselbe_Antwort()
    {
        var user = await factory.CreateUserAsync();

        await AssertAuthErrorAsync(await LoginAsync(user.Email, "Falsch-123"), AuthErrors.InvalidCredentials);
        await AssertAuthErrorAsync(await LoginAsync(TestData.Email()), AuthErrors.InvalidCredentials);
    }

    [Fact]
    public async Task Nach_5_Fehlversuchen_ist_der_Login_gesperrt_auch_mit_richtigem_Passwort()
    {
        var user = await factory.CreateUserAsync();

        for (var i = 0; i < 5; i++)
            await LoginAsync(user.Email, "Falsch-123");

        await AssertAuthErrorAsync(await LoginAsync(user.Email), AuthErrors.LockedOut);
    }

    // --- Refresh-Rotation ---

    [Fact]
    public async Task Refresh_liefert_neues_Token_Paar_und_verbraucht_das_alte()
    {
        var user = await factory.CreateUserAsync();

        var response = await RefreshAsync(user.Tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var neu = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;

        Assert.NotEqual(user.Tokens.RefreshToken, neu.RefreshToken);
        Assert.NotEqual(user.Tokens.AccessToken, neu.AccessToken);
        var devices = await factory.WithBearer(neu.AccessToken).GetAsync("/api/devices");
        Assert.Equal(HttpStatusCode.OK, devices.StatusCode);
    }

    [Fact]
    public async Task Wiederverwendetes_Refresh_Token_widerruft_alle_Sitzungen()
    {
        var user = await factory.CreateUserAsync();
        var rotiert = (await (await RefreshAsync(user.Tokens.RefreshToken)).Content.ReadFromJsonAsync<TokenResponse>())!;

        // Angreifer spielt das alte (bereits ersetzte) Token ein …
        await AssertAuthErrorAsync(await RefreshAsync(user.Tokens.RefreshToken), AuthErrors.InvalidRefreshToken);

        // … daraufhin ist auch das rechtmäßig rotierte Token ungültig
        await AssertAuthErrorAsync(await RefreshAsync(rotiert.RefreshToken), AuthErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Nach_Logout_ist_das_Refresh_Token_ungueltig()
    {
        var user = await factory.CreateUserAsync();

        var logout = await _anonymous.PostAsJsonAsync("/api/auth/logout", new RefreshRequest(user.Tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        await AssertAuthErrorAsync(await RefreshAsync(user.Tokens.RefreshToken), AuthErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Token_mit_fremdem_Schluessel_wird_abgewiesen()
    {
        var user = await factory.CreateUserAsync();
        var teile = user.Tokens.AccessToken.Split('.');
        var manipuliert = $"{teile[0]}.{teile[1]}.{new string('A', teile[2].Length)}";

        var response = await factory.WithBearer(manipuliert).GetAsync("/api/devices");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- D1/D2: Konten und Kontowechsel ---

    [Fact]
    public async Task Kontowechsel_liefert_Token_im_fremden_Konto_mit_dortiger_Rolle()
    {
        var owner = await factory.CreateUserAsync();
        var gast = await factory.CreateUserAsync();
        await factory.AddMemberAsync(owner.CustomerId, gast.UserId, AccountRole.Reader);

        var konten = await gast.Client.GetFromJsonAsync<List<AccountSummaryDto>>("/api/auth/accounts");
        Assert.Equal(2, konten!.Count);
        Assert.True(konten.Single(k => k.CustomerId == gast.CustomerId).IsActive);

        var gewechselt = await factory.SwitchToAsync(gast, owner.CustomerId);
        var me = await gewechselt.Client.GetFromJsonAsync<MeResponse>("/api/auth/me");

        Assert.Equal(owner.CustomerId, me!.CustomerId);
        Assert.Equal(nameof(AccountRole.Reader), me.AccountRole);
    }

    [Fact]
    public async Task Kontowechsel_in_Konto_ohne_Mitgliedschaft_gibt_403()
    {
        var alice = await factory.CreateUserAsync();
        var bob = await factory.CreateUserAsync();

        var response = await bob.Client.PostAsJsonAsync("/api/auth/switch-account",
            new SwitchAccountRequest(alice.CustomerId, bob.Tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Entzogene_Mitgliedschaft_wirkt_beim_naechsten_Refresh()
    {
        var owner = await factory.CreateUserAsync();
        var gast = await factory.CreateUserAsync();
        await factory.AddMemberAsync(owner.CustomerId, gast.UserId, AccountRole.Reader);
        var reader = await factory.SwitchToAsync(gast, owner.CustomerId);

        await factory.WithDbAsync(async db =>
        {
            db.AccountMemberships.RemoveRange(db.AccountMemberships.Where(m => m.UserId == gast.UserId && m.Account.CustomerId == owner.CustomerId));
            await db.SaveChangesAsync();
        });

        var refreshed = (await (await RefreshAsync(reader.Tokens.RefreshToken)).Content.ReadFromJsonAsync<TokenResponse>())!;
        var me = await factory.WithBearer(refreshed.AccessToken).GetFromJsonAsync<MeResponse>("/api/auth/me");

        // Zurück im eigenen Konto — das fremde ist nicht mehr erreichbar
        Assert.Equal(gast.CustomerId, me!.CustomerId);
    }

    // --- A3: Passwort ---

    [Fact]
    public async Task Passwort_zuruecksetzen_per_Link_beendet_alle_Sitzungen()
    {
        var user = await factory.CreateUserAsync();

        var forgot = await _anonymous.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(user.Email));
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);

        var link = LinkParameters(factory.Emails.LastLinkFor(user.Email));
        var reset = await _anonymous.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(link["email"], link["code"], "Neues-Passwort2"));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        await AssertAuthErrorAsync(await RefreshAsync(user.Tokens.RefreshToken), AuthErrors.InvalidRefreshToken);
        await AssertAuthErrorAsync(await LoginAsync(user.Email), AuthErrors.InvalidCredentials);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(user.Email, "Neues-Passwort2")).StatusCode);
    }

    [Fact]
    public async Task Passwort_vergessen_fuer_unbekannte_Adresse_gibt_trotzdem_200()
    {
        var response = await _anonymous.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(TestData.Email()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Passwort_aendern_beendet_andere_Sitzungen_und_liefert_neues_Token_Paar()
    {
        var user = await factory.CreateUserAsync();
        var zweitesGeraet = await factory.LoginAsync(user.Email);

        var response = await user.Client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest(TestData.Password, "Neues-Passwort2"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var neu = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;

        await AssertAuthErrorAsync(await RefreshAsync(zweitesGeraet.Tokens.RefreshToken), AuthErrors.InvalidRefreshToken);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(neu.RefreshToken)).StatusCode);
    }
}
