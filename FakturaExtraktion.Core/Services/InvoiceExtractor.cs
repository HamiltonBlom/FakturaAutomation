using System.Net.Http.Json;
using System.Text.Json;
using FakturaExtraktion.Models;
using FakturaExtraktion.Options;
using FakturaExtraktion.Services.Anthropic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FakturaExtraktion.Services;

/// <summary>
/// Extraherar fakturadata genom att anropa Anthropics Messages API med
/// tool_choice tvingat till verktyget "extrahera_faktura", så att svaret
/// alltid blir ett tool_use-block med giltig JSON enligt schemat.
/// System-prompt och verktygsschema är identiska för varje anrop och är
/// därför markerade med cache_control (ephemeral) för prompt caching.
///
/// Stöder två indatavägar: ren text (<see cref="ExtraheraAsync"/>) och en
/// PDF skickad direkt till modellen (<see cref="ExtraheraFranPdfAsync"/>).
/// PDF-vägen låter Claude läsa fakturan multimodalt (layout, kolumner,
/// tabeller) istället för att först gissa fram text lokalt med ett
/// textextraktionsbibliotek – betydligt mer träffsäkert för fakturor med
/// tabeller, vilket var källan till felaktiga belopp i tidigare version.
/// </summary>
public sealed class InvoiceExtractor : IInvoiceExtractor
{
    /// <summary>Namnet på verktyget, måste matcha input_schema och tool_choice.</summary>
    private const string ToolName = "extrahera_faktura";

    /// <summary>
    /// System-prompten, ordagrant enligt uppdragsspecifikationen. Ändras
    /// den, invalideras prompt-cachen (systemet cachas som prefix).
    /// </summary>
    private const string SystemPrompt = """
        Du är ett extraktionssystem som läser en svensk leverantörsfaktura och
        returnerar strukturerad data enligt det angivna verktygsschemat.

        Uppgift: Extrahera fälten ur fakturatexten och anropa verktyget
        "extrahera_faktura" med resultatet. Skriv ingen förklarande text.

        Regler:
        1. Extrahera endast information som faktiskt står i fakturan. Saknas ett
           fält eller är oläsligt: sätt värdet till null. Hitta aldrig på värden.
        2. Ändra eller beräkna inte belopp – återge dem exakt som de står tryckta.
           Validering av summor sker i ett senare steg, inte av dig.
        3. Normalisera format:
           - Datum  -> ISO 8601 (ÅÅÅÅ-MM-DD).
           - Belopp -> tal med punkt som decimaltecken, utan tusentalsavgränsare
             och utan valutasymbol.  Ex: "12 345,50 kr" -> 12345.50
           - Organisationsnummer -> formatet NNNNNN-NNNN.
        4. Behåll text (leverantörsnamn, adresser, radbeskrivningar) EXAKT
           ordagrant, tecken för tecken. Rätta ALDRIG stavning, även om ett ord
           ser ovanligt eller felstavat ut – normalisera aldrig till en "vanligare"
           stavning. Exempel: står det "Medieteknik" i fakturan ska du skriva
           "Medieteknik", inte "Mediateknik", även om den senare stavningen
           känns mer bekant.
        5. Momssats i procent om den framgår (25, 12, 6), annars null.
        6. Fånga betalningsreferens: bankgiro, plusgiro, IBAN och/eller OCR-nummer
           om de finns.
        7. Extrahera varje fakturarad separat i "radposter". Är raderna otydliga:
           lämna radposter tom men fyll ändå i totalsummorna.
        8. Markera osäkerhet: lägg fältnamn du är osäker på i "osakra_falt", och
           sätt "granskning_kravs" till true om ett centralt fält är osäkert eller
           saknas, eller om tryckta delsummor uppenbart inte går ihop. Motivera
           kort i "noteringar".
        """;

    private const string ToolDescription =
        "Returnerar strukturerad data extraherad ur en svensk leverantörsfaktura.";

    /// <summary>Instruktionstext som följer med PDF:en i user-meddelandet.</summary>
    private const string PdfInstruktion =
        "Extrahera fakturadata ur den bifogade PDF-fakturan enligt instruktionerna i systemprompten.";

    /// <summary>input_schema för verktyget, ordagrant enligt uppdragsspecifikationen.</summary>
    private const string InputSchemaJson = """
        {
          "type": "object",
          "properties": {
            "leverantor_namn":     { "type": ["string", "null"] },
            "organisationsnummer": { "type": ["string", "null"] },
            "fakturanummer":       { "type": ["string", "null"] },
            "fakturadatum":        { "type": ["string", "null"], "description": "ÅÅÅÅ-MM-DD" },
            "forfallodatum":       { "type": ["string", "null"], "description": "ÅÅÅÅ-MM-DD" },
            "valuta":              { "type": ["string", "null"], "description": "t.ex. SEK" },
            "belopp_ex_moms":      { "type": ["number", "null"] },
            "momsbelopp":          { "type": ["number", "null"] },
            "moms_procent":        { "type": ["number", "null"] },
            "totalt_belopp":       { "type": ["number", "null"] },
            "betalning": {
              "type": "object",
              "properties": {
                "bankgiro": { "type": ["string", "null"] },
                "plusgiro": { "type": ["string", "null"] },
                "iban":     { "type": ["string", "null"] },
                "ocr":      { "type": ["string", "null"] }
              },
              "required": ["bankgiro", "plusgiro", "iban", "ocr"]
            },
            "radposter": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "beskrivning": { "type": "string" },
                  "antal":       { "type": ["number", "null"] },
                  "enhetspris":  { "type": ["number", "null"] },
                  "radbelopp":   { "type": ["number", "null"] }
                },
                "required": ["beskrivning", "antal", "enhetspris", "radbelopp"]
              }
            },
            "granskning_kravs": { "type": "boolean" },
            "osakra_falt":      { "type": "array", "items": { "type": "string" } },
            "noteringar":       { "type": ["string", "null"] }
          },
          "required": [
            "leverantor_namn", "organisationsnummer", "fakturanummer",
            "fakturadatum", "forfallodatum", "valuta", "belopp_ex_moms",
            "momsbelopp", "moms_procent", "totalt_belopp", "betalning",
            "radposter", "granskning_kravs", "osakra_falt", "noteringar"
          ]
        }
        """;

    // Parsas en gång och återanvänds för varje anrop (samma referens krävs
    // inte för korrekthet, men slipper vi tolka om JSON-schemat varje gång).
    private static readonly JsonElement InputSchemaElement =
        JsonDocument.Parse(InputSchemaJson).RootElement.Clone();

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ClaudeOptions _options;
    private readonly ILogger<InvoiceExtractor> _logger;

    public InvoiceExtractor(
        IHttpClientFactory httpClientFactory,
        IOptions<ClaudeOptions> options,
        ILogger<InvoiceExtractor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public Task<ExtraheradFaktura> ExtraheraAsync(string fakturaText, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fakturaText);

        var request = ByggRequest(new AnthropicMessage
        {
            Role = "user",
            Content =
            [
                new AnthropicContentInputBlock { Type = "text", Text = fakturaText },
            ],
        });

        return SkickaOchTolkaSvarAsync(request, cancellationToken);
    }

    public Task<ExtraheradFaktura> ExtraheraFranPdfAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);

        if (pdfBytes.Length == 0)
        {
            throw new ArgumentException("PDF-datan är tom.", nameof(pdfBytes));
        }

        var request = ByggRequest(new AnthropicMessage
        {
            Role = "user",
            Content =
            [
                new AnthropicContentInputBlock
                {
                    Type = "document",
                    Source = new AnthropicDocumentSource
                    {
                        MediaType = "application/pdf",
                        Data = Convert.ToBase64String(pdfBytes),
                    },
                },
                new AnthropicContentInputBlock { Type = "text", Text = PdfInstruktion },
            ],
        });

        return SkickaOchTolkaSvarAsync(request, cancellationToken);
    }

    /// <summary>Bygger själva Anthropic-anropet – identiskt oavsett om indatan är text eller PDF, förutom user-meddelandet.</summary>
    private AnthropicRequest ByggRequest(AnthropicMessage userMessage) => new()
    {
        Model = _options.Model,
        MaxTokens = _options.MaxTokens,
        Temperature = 0,
        SystemBlocks =
        [
            new AnthropicSystemBlock
            {
                Text = SystemPrompt,
                CacheControl = new AnthropicCacheControl(),
            }
        ],
        Messages = [userMessage],
        Tools =
        [
            new AnthropicTool
            {
                Name = ToolName,
                Description = ToolDescription,
                InputSchema = InputSchemaElement,
                CacheControl = new AnthropicCacheControl(),
            }
        ],
        ToolChoice = new AnthropicToolChoice { Name = ToolName },
    };

    /// <summary>Skickar anropet, hanterar fel och tolkar tool_use-svaret – delad av text- och PDF-vägen.</summary>
    private async Task<ExtraheradFaktura> SkickaOchTolkaSvarAsync(AnthropicRequest request, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient("anthropic");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsJsonAsync("v1/messages", request, SerializerOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Nätverksfel eller timeout vid anrop till Anthropic API.");
            throw new FakturaExtraktionException(
                "Kunde inte nå Anthropic API (nätverksfel eller timeout). Försök igen.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var felmeddelande = await LasFelmeddelandeAsync(response, cancellationToken);
                _logger.LogError(
                    "Anthropic API svarade med statuskod {StatusCode}: {Felmeddelande}",
                    (int)response.StatusCode, felmeddelande);
                throw new FakturaExtraktionException(
                    $"Anthropic API svarade med statuskod {(int)response.StatusCode}: {felmeddelande}");
            }

            var anthropicResponse = await response.Content
                .ReadFromJsonAsync<AnthropicResponse>(SerializerOptions, cancellationToken);

            if (anthropicResponse is null)
            {
                throw new FakturaExtraktionException("Anthropic API returnerade ett tomt eller otolkbart svar.");
            }

            var toolUseBlock = anthropicResponse.Content
                .FirstOrDefault(block => block.Type == "tool_use" && block.Name == ToolName);

            if (toolUseBlock is null)
            {
                _logger.LogError(
                    "Inget tool_use-block för verktyget {ToolName} hittades i svaret. stop_reason={StopReason}",
                    ToolName, anthropicResponse.StopReason);
                throw new FakturaExtraktionException(
                    $"Modellen returnerade inget giltigt verktygsanrop för \"{ToolName}\" " +
                    $"(stop_reason: {anthropicResponse.StopReason ?? "okänd"}).");
            }

            try
            {
                var faktura = toolUseBlock.Input.Deserialize<ExtraheradFaktura>(SerializerOptions);

                if (faktura is null)
                {
                    throw new FakturaExtraktionException(
                        "Verktygsanropets input kunde inte tolkas som fakturadata (null-resultat).");
                }

                return faktura;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "Misslyckades att deserialisera tool_use-input till ExtraheradFaktura. stop_reason={StopReason}",
                    anthropicResponse.StopReason);
                throw new FakturaExtraktionException(
                    "Modellens verktygsanrop innehöll data som inte matchar det förväntade schemat " +
                    $"(stop_reason: {anthropicResponse.StopReason ?? "okänd"}).", ex);
            }
        }
    }

    private static async Task<string> LasFelmeddelandeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            var error = JsonSerializer.Deserialize<AnthropicErrorEnvelope>(body, SerializerOptions);
            return error?.Error?.Message is { Length: > 0 } message ? message : body;
        }
        catch
        {
            return "(kunde inte tolka felmeddelandets innehåll)";
        }
    }
}
