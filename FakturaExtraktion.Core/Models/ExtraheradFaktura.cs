using System.Text.Json.Serialization;

namespace FakturaExtraktion.Models;

/// <summary>
/// Strukturerad data extraherad ur en svensk leverantörsfaktura av AI-laget.
/// Speglar exakt fälten i verktygsschemat "extrahera_faktura". Alla fält är
/// nullbara i linje med schemat – saknade eller oläsliga uppgifter ska vara
/// null snarare än påhittade. Valideringen av siffrorna sker separat i
/// <see cref="Services.IInvoiceValidator"/>, inte här.
/// </summary>
public sealed record ExtraheradFaktura
{
    [JsonPropertyName("leverantor_namn")]
    public string? LeverantorNamn { get; init; }

    [JsonPropertyName("organisationsnummer")]
    public string? Organisationsnummer { get; init; }

    [JsonPropertyName("fakturanummer")]
    public string? Fakturanummer { get; init; }

    /// <summary>Fakturadatum i formatet ÅÅÅÅ-MM-DD (ISO 8601).</summary>
    [JsonPropertyName("fakturadatum")]
    public string? Fakturadatum { get; init; }

    /// <summary>Förfallodatum i formatet ÅÅÅÅ-MM-DD (ISO 8601).</summary>
    [JsonPropertyName("forfallodatum")]
    public string? Forfallodatum { get; init; }

    /// <summary>Valutakod, t.ex. "SEK".</summary>
    [JsonPropertyName("valuta")]
    public string? Valuta { get; init; }

    [JsonPropertyName("belopp_ex_moms")]
    public decimal? BeloppExMoms { get; init; }

    [JsonPropertyName("momsbelopp")]
    public decimal? Momsbelopp { get; init; }

    [JsonPropertyName("moms_procent")]
    public decimal? MomsProcent { get; init; }

    [JsonPropertyName("totalt_belopp")]
    public decimal? TotaltBelopp { get; init; }

    [JsonPropertyName("betalning")]
    public Betalning Betalning { get; init; } = new();

    [JsonPropertyName("radposter")]
    public IReadOnlyList<Radpost> Radposter { get; init; } = [];

    /// <summary>Modellens egen bedömning: true om granskning bör ske.</summary>
    [JsonPropertyName("granskning_kravs")]
    public bool GranskningKravs { get; init; }

    /// <summary>Fältnamn modellen själv flaggat som osäkra.</summary>
    [JsonPropertyName("osakra_falt")]
    public IReadOnlyList<string> OsakraFalt { get; init; } = [];

    [JsonPropertyName("noteringar")]
    public string? Noteringar { get; init; }
}
