using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using FakturaExtraktion.Options;
using FakturaExtraktion.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;

// ---------------------------------------------------------------------
// Minimal konsolapp: dotnet run -- <sokvag-till-fakturatext.txt>
// Läser fakturatext -> extraherar via Claude (tvingat verktygsanrop) ->
// validerar summor/format i C# -> skriver resultatet som JSON på stdout.
//
// Avslutningskoder:
//   0 = lyckad extraktion, ingen granskning krävs
//   2 = lyckad extraktion, men granskning krävs (se "granskning" i JSON)
//   1 = fel (se stderr och loggar)
// ---------------------------------------------------------------------

if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("Användning: dotnet run -- <sokvag-till-fakturatext.txt>");
    return 1;
}

var fakturaSokvag = args[0];

var builder = Host.CreateApplicationBuilder(args);

// Stödjer user-secrets som alternativ till miljövariabeln ANTHROPIC_API_KEY
// (kräver <UserSecretsId> i .csproj, se leveransanteckningar).
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
            "'dotnet user-secrets set \"ANTHROPIC_API_KEY\" \"din-nyckel\"' i projektmappen.");
    }

    client.BaseAddress = new Uri(claudeOptions.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(claudeOptions.TimeoutSekunder);
    client.DefaultRequestHeaders.Add("x-api-key", apiKey);
    client.DefaultRequestHeaders.Add("anthropic-version", claudeOptions.AnthropicVersion);
})
.AddResilienceHandler("anthropic-retry", static resilienceBuilder =>
{
    // Enkel retry med exponentiell backoff (Polly v8) för transienta fel:
    // nätverksfel, timeouts, 429 (rate limit), 408 och 5xx.
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

builder.Services.AddSingleton<IPdfTextExtractor, FilTextExtractor>();
builder.Services.AddSingleton<IInvoiceExtractor, InvoiceExtractor>();
builder.Services.AddSingleton<IInvoiceValidator, InvoiceValidator>();

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

try
{
    var textExtractor = host.Services.GetRequiredService<IPdfTextExtractor>();
    var invoiceExtractor = host.Services.GetRequiredService<IInvoiceExtractor>();
    var invoiceValidator = host.Services.GetRequiredService<IInvoiceValidator>();

    var fakturaText = await textExtractor.ExtraheraTextAsync(fakturaSokvag, cts.Token);
    var faktura = await invoiceExtractor.ExtraheraAsync(fakturaText, cts.Token);
    var granskning = invoiceValidator.Validera(faktura);

    var resultat = new
    {
        Faktura = faktura,
        Granskning = granskning,
    };

    var json = JsonSerializer.Serialize(resultat, new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // undvik \uXXXX-eskapering av åäö
    });

    Console.WriteLine(json);

    return granskning.KräverGranskning ? 2 : 0;
}
catch (FileNotFoundException ex)
{
    logger.LogError(ex, "Fakturafilen hittades inte.");
    Console.Error.WriteLine($"Fel: {ex.Message}");
    return 1;
}
catch (FakturaExtraktionException ex)
{
    logger.LogError(ex, "Fakturaextraktionen misslyckades.");
    Console.Error.WriteLine($"Fel: {ex.Message}");
    return 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Avbrutet.");
    return 130;
}
catch (Exception ex)
{
    logger.LogError(ex, "Oväntat fel.");
    Console.Error.WriteLine($"Oväntat fel: {ex.Message}");
    return 1;
}
