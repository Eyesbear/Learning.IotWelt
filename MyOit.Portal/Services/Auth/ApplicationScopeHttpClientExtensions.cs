using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace MyOit.Portal.Services.Auth;

// Nach Microsoft-Doku „Access AuthenticationStateProvider in outgoing request middleware“
// (Variante „Application scope handler“, empfohlen):
// IHttpClientFactory baut DelegatingHandler in einem EIGENEN DI-Scope — dort ist der angemeldete
// Benutzer unbekannt. Deshalb wird der HttpClient als keyed scoped Service registriert; sein äußerster
// Handler hängt den Scope der aufrufenden Komponente (Request beim Prerendering, Circuit im
// interaktiven Modus) an jede Anfrage. Weiter innen liegende Handler lesen ihn über ScopeKey.
public static class ApplicationScopeHttpClientExtensions
{
    public static readonly HttpRequestOptionsKey<IServiceProvider> ScopeKey = new("ApplicationScope");

    public static IHttpClientBuilder AddApplicationScopeHandler(this IHttpClientBuilder builder)
    {
        var name = builder.Name;

        builder.Services.AddTransient<ApplicationScopeHandler>();

        builder.Services.AddKeyedScoped<HttpClient>(name, (sp, _) =>
        {
            var handler = sp.GetRequiredService<ApplicationScopeHandler>();
            // Innere Handler-Kette weiter aus der Factory → Connection-Pooling bleibt erhalten
            handler.InnerHandler = sp.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(name);

            var client = new HttpClient(handler, disposeHandler: false);

            // Dieselbe Konfiguration (BaseAddress usw.) wie beim Basis-Client übernehmen
            var options = sp.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>().Get(name);
            foreach (var action in options.HttpClientActions)
                action(client);

            return client;
        });

        return builder;
    }
}

public sealed class ApplicationScopeHandler(IServiceProvider serviceProvider) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Options.Set(ApplicationScopeHttpClientExtensions.ScopeKey, serviceProvider);
        return base.SendAsync(request, cancellationToken);
    }
}
