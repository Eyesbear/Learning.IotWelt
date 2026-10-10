using Microsoft.AspNetCore.Components;

namespace MyOit.Portal.Services.Auth;

public static class NavigationManagerExtensions
{
    // Zum Login und danach zurück zur aktuellen Seite. forceLoad: Die Login-Seite ist statisch gerendert
    // und braucht einen echten HTTP-Request — dabei verwirft die Cookie-Prüfung auch eine verwaiste Sitzung.
    public static void NavigateToLogin(this NavigationManager navigation)
    {
        var returnUrl = "/" + navigation.ToBaseRelativePath(navigation.Uri);
        navigation.NavigateTo($"account/login?ReturnUrl={Uri.EscapeDataString(returnUrl)}", forceLoad: true);
    }
}
