namespace FakturaExtraktion.Options;

/// <summary>
/// Konfiguration för anrop till Anthropics Messages API. Bindas från
/// konfigurationssektionen "Claude" (t.ex. i appsettings.json).
/// Observera: API-nyckeln finns INTE här – den läses separat från
/// miljövariabeln ANTHROPIC_API_KEY (eller user-secrets) i Program.cs.
/// </summary>
public sealed class ClaudeOptions
{
    /// <summary>Modellsträng, t.ex. "claude-haiku-4-5-20251001".</summary>
    public string Model { get; set; } = "claude-haiku-4-5-20251001";

    /// <summary>Bas-URL för Anthropic Messages API.</summary>
    public string BaseUrl { get; set; } = "https://api.anthropic.com/";

    /// <summary>Värde för HTTP-headern anthropic-version.</summary>
    public string AnthropicVersion { get; set; } = "2023-06-01";

    /// <summary>Max antal output-tokens per anrop.</summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>Timeout för HTTP-anropet, i sekunder.</summary>
    public int TimeoutSekunder { get; set; } = 60;
}
