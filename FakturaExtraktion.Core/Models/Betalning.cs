using System.Text.Json.Serialization;

namespace FakturaExtraktion.Models;

/// <summary>
/// Betalningsreferenser som extraherats ur fakturan. Samtliga fält är
/// nullbara – fyll endast i de referenser som faktiskt förekommer.
/// </summary>
public sealed record Betalning
{
    [JsonPropertyName("bankgiro")]
    public string? Bankgiro { get; init; }

    [JsonPropertyName("plusgiro")]
    public string? Plusgiro { get; init; }

    [JsonPropertyName("iban")]
    public string? Iban { get; init; }

    [JsonPropertyName("ocr")]
    public string? Ocr { get; init; }
}
