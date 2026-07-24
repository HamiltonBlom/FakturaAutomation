using FakturaExtraktion.Models;

namespace FakturaExtraktion.Web.Services;

/// <summary>
/// ANTAGANDE: backend (IInvoiceExtractor/IInvoiceValidator) har inget
/// samlat "hämta en vald/uppladdad faktura och granska den"-flöde – det är
/// två separata steg (extrahera, validera). Det här interfacet är den
/// tunna sammanhållande ytan UI:t kodar mot. Det innehåller INGEN egen
/// extraktions- eller valideringslogik: <see cref="GranskaAsync"/> och
/// <see cref="GranskaPdfAsync"/> är bara ett de-facto-anrop av
/// IInvoiceExtractor (text- respektive PDF-vägen) följt av
/// IInvoiceValidator.Validera, plus två inbyggda exempeltexter så att
/// granskningsvyn kan demonstreras utan filuppladdning.
/// </summary>
public interface IFakturaService
{
    /// <summary>Namn på inbyggda exempelfakturor, för en enkel dropdown.</summary>
    IReadOnlyList<string> Exempelfakturor { get; }

    /// <summary>Hämtar den råa fakturatexten för ett inbyggt exempel.</summary>
    Task<string> LasExempelAsync(string exempelNamn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kör extraktion (Claude, via IInvoiceExtractor) och validering
    /// (C#, via IInvoiceValidator) på given fakturatext.
    /// </summary>
    Task<FakturaGranskningResultat> GranskaAsync(string fakturaText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kör extraktion direkt ur en PDF (Claude läser den multimodalt, ingen
    /// lokal textextraktion) och validering (C#).
    /// </summary>
    Task<FakturaGranskningResultat> GranskaPdfAsync(byte[] pdfBytes, CancellationToken cancellationToken = default);
}

/// <summary>Sammanslaget resultat: extraherad faktura + granskningsresultat.</summary>
public sealed record FakturaGranskningResultat(ExtraheradFaktura Faktura, GranskningsResultat Granskning);
