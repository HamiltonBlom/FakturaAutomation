using System.Text.Json;
using System.Text.Json.Serialization;

namespace FakturaExtraktion.Services.Anthropic;

// Interna DTO:er som speglar formatet för Anthropics Messages API
// (POST /v1/messages). De är avsiktligt hållna separata från
// FakturaExtraktion.Models, som representerar vår egen domänmodell.

internal sealed class AnthropicRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("max_tokens")]
    public required int MaxTokens { get; init; }

    // Sätts till 0 för att göra extraktionen så deterministisk som möjligt
    // (detta är en strukturerad extraktionsuppgift, inte kreativ text –
    // vi vill ha samma tolkning av samma faktura varje gång).
    [JsonPropertyName("temperature")]
    public required double Temperature { get; init; }

    [JsonPropertyName("system")]
    public required List<AnthropicSystemBlock> SystemBlocks { get; init; }

    [JsonPropertyName("messages")]
    public required List<AnthropicMessage> Messages { get; init; }

    [JsonPropertyName("tools")]
    public required List<AnthropicTool> Tools { get; init; }

    [JsonPropertyName("tool_choice")]
    public required AnthropicToolChoice ToolChoice { get; init; }
}

internal sealed class AnthropicSystemBlock
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "text";

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("cache_control")]
    public AnthropicCacheControl? CacheControl { get; init; }
}

internal sealed class AnthropicCacheControl
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "ephemeral";
}

internal sealed class AnthropicMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    // En lista av innehållsblock (inte bara en sträng) så att samma DTO kan
    // representera både ren text OCH ett PDF-dokument + text ihop – Claude
    // kan läsa en PDF direkt (multimodalt), vilket är mer träffsäkert än att
    // först gissa fram text lokalt med ett textextraktionsbibliotek.
    [JsonPropertyName("content")]
    public required List<AnthropicContentInputBlock> Content { get; init; }
}

internal sealed class AnthropicContentInputBlock
{
    [JsonPropertyName("type")]
    public required string Type { get; init; } // "text" eller "document"

    /// <summary>Satt när Type är "text".</summary>
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; init; }

    /// <summary>Satt när Type är "document".</summary>
    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AnthropicDocumentSource? Source { get; init; }
}

internal sealed class AnthropicDocumentSource
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "base64";

    [JsonPropertyName("media_type")]
    public required string MediaType { get; init; }

    [JsonPropertyName("data")]
    public required string Data { get; init; }
}

internal sealed class AnthropicTool
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("input_schema")]
    public required JsonElement InputSchema { get; init; }

    [JsonPropertyName("cache_control")]
    public AnthropicCacheControl? CacheControl { get; init; }
}

internal sealed class AnthropicToolChoice
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "tool";

    [JsonPropertyName("name")]
    public required string Name { get; init; }
}

internal sealed class AnthropicResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; init; }

    [JsonPropertyName("content")]
    public List<AnthropicContentBlock> Content { get; init; } = [];

    [JsonPropertyName("usage")]
    public AnthropicUsage? Usage { get; init; }
}

internal sealed class AnthropicContentBlock
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>Verktygsnamnet, satt när Type är "tool_use".</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>Verktygsanropets argument, satt när Type är "tool_use".</summary>
    [JsonPropertyName("input")]
    public JsonElement Input { get; init; }

    /// <summary>Fritext, satt när Type är "text" (bör inte förekomma vid tvingat tool_choice).</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

internal sealed class AnthropicUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; init; }

    [JsonPropertyName("cache_creation_input_tokens")]
    public int? CacheCreationInputTokens { get; init; }

    [JsonPropertyName("cache_read_input_tokens")]
    public int? CacheReadInputTokens { get; init; }
}

/// <summary>Felkuvert som Anthropic API returnerar vid icke-2xx-svar.</summary>
internal sealed class AnthropicErrorEnvelope
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("error")]
    public AnthropicErrorDetail? Error { get; init; }
}

internal sealed class AnthropicErrorDetail
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
