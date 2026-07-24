namespace FakturaExtraktion.Options;

/// <summary>
/// Konfiguration för aritmetisk och strukturell validering av extraherade
/// fakturor. Bindas från konfigurationssektionen "Validering".
/// </summary>
public sealed class ValideringsOptions
{
    /// <summary>
    /// Tillåten avvikelse (i valutaenheter) mellan belopp_ex_moms + momsbelopp
    /// och totalt_belopp innan det räknas som en avvikelse. Standard 0,01.
    /// </summary>
    public decimal SummaTolerans { get; set; } = 0.01m;
}
