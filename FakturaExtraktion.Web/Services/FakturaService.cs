using FakturaExtraktion.Services;

namespace FakturaExtraktion.Web.Services;

/// <summary>
/// Se <see cref="IFakturaService"/> för antagandet bakom det här lagret.
/// Exempeltexterna är inbäddade som strängar (inte filer på disk) för att
/// vyn ska fungera oavsett hur/var Blazor-appen hostas.
/// </summary>
public sealed class FakturaService : IFakturaService
{
    private readonly IInvoiceExtractor _extractor;
    private readonly IInvoiceValidator _validator;

    private static readonly Dictionary<string, string> Exempel = new()
    {
        ["Kontorsmaterial Svenska AB (komplett faktura)"] = ExempelKomplett,
        ["Nordic IT-Konsult AB (summan stämmer inte)"] = ExempelMedAvvikelse,
    };

    public FakturaService(IInvoiceExtractor extractor, IInvoiceValidator validator)
    {
        _extractor = extractor;
        _validator = validator;
    }

    public IReadOnlyList<string> Exempelfakturor { get; } = Exempel.Keys.ToList();

    public Task<string> LasExempelAsync(string exempelNamn, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Exempel.TryGetValue(exempelNamn, out var text) ? text : string.Empty);
    }

    public async Task<FakturaGranskningResultat> GranskaAsync(string fakturaText, CancellationToken cancellationToken = default)
    {
        var faktura = await _extractor.ExtraheraAsync(fakturaText, cancellationToken);
        var granskning = _validator.Validera(faktura);
        return new FakturaGranskningResultat(faktura, granskning);
    }

    public async Task<FakturaGranskningResultat> GranskaPdfAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        var faktura = await _extractor.ExtraheraFranPdfAsync(pdfBytes, cancellationToken);
        var granskning = _validator.Validera(faktura);
        return new FakturaGranskningResultat(faktura, granskning);
    }

    // Ren, konsekvent faktura: alla summor stämmer, giltigt org.nr, giltiga
    // datum -> ska ge KräverGranskning = false ("Redo att godkänna").
    private const string ExempelKomplett = """
        FAKTURA

        Kontorsmaterial Svenska AB
        Organisationsnummer: 556677-8899
        Storgatan 12
        111 22 Stockholm

        Fakturanummer: 2026-0417
        Fakturadatum: 2026-06-02
        Förfallodatum: 2026-06-16

        Fakturamottagare:
        Exempelbolaget AB
        Kundnummer: 4711

        Specifikation:
        Antal   Beskrivning                          À-pris      Belopp
        10      A4-papper, 500 ark                   45,00 kr     450,00 kr
        2       Bläckpatron HP 305 svart              299,00 kr    598,00 kr
        1       Skrivbordslampa LED                   649,00 kr    649,00 kr

        Belopp exkl. moms:            1 697,00 kr
        Moms (25 %):                    424,25 kr
        Att betala:                   2 121,25 kr

        Betalningsvillkor: 14 dagar netto
        Bankgiro: 123-4567
        OCR-nummer: 34029981123

        Valuta: SEK
        """;

    // Fakturan har ett äkta tryckfel: raderna och "Belopp exkl. moms" stämmer
    // (10 000,00 + 4 500,00 = 14 500,00), men slutsumman är felräknad på
    // originalfakturan (18 225,00 i stället för korrekta 18 125,00).
    // Eftersom extraktionen ska återge belopp exakt som tryckt (inte räkna
    // om dem) fångas det här av IInvoiceValidator, inte av Claude.
    private const string ExempelMedAvvikelse = """
        FAKTURA

        Nordic IT-Konsult AB
        Organisationsnummer: 556012-3456
        Kungsgatan 45
        411 19 Göteborg

        Fakturanummer: F-2026-0892
        Fakturadatum: 2026-05-14
        Förfallodatum: 2026-05-28

        Fakturamottagare:
        Exempelbolaget AB

        Specifikation:
        Antal   Beskrivning                       À-pris          Belopp
        8       Konsulttimmar, backend-utveckling  1 250,00 kr    10 000,00 kr
        1       Projektledning, maj                4 500,00 kr     4 500,00 kr

        Belopp exkl. moms:            14 500,00 kr
        Moms (25 %):                   3 625,00 kr
        Att betala:                   18 225,00 kr

        Betalningsvillkor: 30 dagar netto
        Plusgiro: 98 76 54-3

        Valuta: SEK
        """;
}
