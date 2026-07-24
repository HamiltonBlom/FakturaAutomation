using FakturaExtraktion.Models;

namespace FakturaExtraktion.Services;

/// <summary>
/// Extraherar strukturerad fakturadata via ett AI-lager (Anthropics Messages
/// API), antingen ur fri text eller direkt ur en PDF. Gör ingen aritmetisk
/// validering – det är <see cref="IInvoiceValidator"/>:s ansvar.
/// </summary>
public interface IInvoiceExtractor
{
    /// <summary>
    /// Skickar fakturatexten till modellen och returnerar det strukturerade
    /// resultatet av det tvingade verktygsanropet "extrahera_faktura".
    /// </summary>
    /// <exception cref="FakturaExtraktionException">
    /// Vid API-fel, uteblivet tool_use-block eller ogiltig JSON i svaret.
    /// </exception>
    Task<ExtraheradFaktura> ExtraheraAsync(string fakturaText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Skickar en PDF direkt till modellen (multimodalt, ingen lokal
    /// textextraktion) och returnerar samma strukturerade resultat. Mer
    /// träffsäkert för fakturor med tabeller/kolumner än att först gissa
    /// fram text lokalt med ett textextraktionsbibliotek.
    /// </summary>
    /// <exception cref="FakturaExtraktionException">
    /// Vid API-fel, uteblivet tool_use-block eller ogiltig JSON i svaret.
    /// </exception>
    Task<ExtraheradFaktura> ExtraheraFranPdfAsync(byte[] pdfBytes, CancellationToken cancellationToken = default);
}
