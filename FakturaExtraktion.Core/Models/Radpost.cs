using System.Text.Json.Serialization;

namespace FakturaExtraktion.Models;

/// <summary>En enskild rad på fakturan.</summary>
public sealed record Radpost
{
    [JsonPropertyName("beskrivning")]
    public string Beskrivning { get; init; } = string.Empty;

    [JsonPropertyName("antal")]
    public decimal? Antal { get; init; }

    [JsonPropertyName("enhetspris")]
    public decimal? Enhetspris { get; init; }

    [JsonPropertyName("radbelopp")]
    public decimal? Radbelopp { get; init; }
}
