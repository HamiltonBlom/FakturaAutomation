namespace FakturaExtraktion.Services;

/// <summary>
/// Abstraherar inläsning av fakturans råtext. I detta steg finns bara en
/// filbaserad implementation (<see cref="FilTextExtractor"/>) som läser en
/// vanlig textfil, men interfacet gör det möjligt att senare koppla in en
/// riktig PDF-parser utan att röra resten av kärnan.
/// </summary>
public interface IPdfTextExtractor
{
    /// <summary>Läser och returnerar fakturans fulla text.</summary>
    /// <param name="filsokvag">Sökväg till filen som innehåller fakturatexten.</param>
    /// <param name="cancellationToken">Token för avbrytning.</param>
    Task<string> ExtraheraTextAsync(string filsokvag, CancellationToken cancellationToken = default);
}
