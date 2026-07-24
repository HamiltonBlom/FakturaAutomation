using System.Net;
using FakturaExtraktion.Options;
using FakturaExtraktion.Services;
using FakturaExtraktion.Web.Components;
using FakturaExtraktion.Web.Services;
using Microsoft.Extensions.Http.Resilience;
using Polly;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------
// Samma DI-uppsättning för Anthropic-anropet som i konsolappen
// (FakturaExtraktion/Program.cs) – kopierad rakt av, inte omskriven,
// eftersom uppdraget bad om att återanvända backend, inte bygga om den.
// ---------------------------------------------------------------------

builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true, reloadOnChange: false);

builder.Services.Configure<ClaudeOptions>(builder.Configuration.GetSection("Claude"));
builder.Services.Configure<ValideringsOptions>(builder.Configuration.GetSection("Validering"));

var claudeOptions = builder.Configuration.GetSection("Claude").Get<ClaudeOptions>() ?? new ClaudeOptions();

builder.Services.AddHttpClient("anthropic", client =>
{
    // API-nyckeln hårdkodas ALDRIG – läses från miljövariabeln
    // ANTHROPIC_API_KEY, eller från user-secrets under samma nyckelnamn.
    var apiKey = builder.Configuration["ANTHROPIC_API_KEY"];

    if (string.IsNullOrWhiteSpace(apiKey))
    {
        throw new InvalidOperationException(
            "ANTHROPIC_API_KEY saknas. Sätt miljövariabeln ANTHROPIC_API_KEY, eller kör " +
            "'dotnet user-secrets set \"ANTHROPIC_API_KEY\" \"din-nyckel\"' i FakturaExtraktion.Web-mappen.");
    }

    client.BaseAddress = new Uri(claudeOptions.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(claudeOptions.TimeoutSekunder);
    client.DefaultRequestHeaders.Add("x-api-key", apiKey);
    client.DefaultRequestHeaders.Add("anthropic-version", claudeOptions.AnthropicVersion);
})
.AddResilienceHandler("anthropic-retry", static resilienceBuilder =>
{
    resilienceBuilder.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        Delay = TimeSpan.FromSeconds(1),
        ShouldHandle = retryArgs => retryArgs.Outcome switch
        {
            { Exception: HttpRequestException or TaskCanceledException } => PredicateResult.True(),
            { Result.StatusCode: HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout } => PredicateResult.True(),
            { Result: { } response } when (int)response.StatusCode >= 500 => PredicateResult.True(),
            _ => PredicateResult.False(),
        },
    });
});

// Befintliga backend-tjänster – används som de är.
builder.Services.AddSingleton<IInvoiceExtractor, InvoiceExtractor>();
builder.Services.AddSingleton<IInvoiceValidator, InvoiceValidator>();

// ---------------------------------------------------------------------
// UI-specifika tjänster (nya för det här steget – se README för
// antagandet bakom IFakturaService-signaturen)
// ---------------------------------------------------------------------
builder.Services.AddSingleton<IFakturaService, FakturaService>();
builder.Services.AddSingleton<IExportService, ExportService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
