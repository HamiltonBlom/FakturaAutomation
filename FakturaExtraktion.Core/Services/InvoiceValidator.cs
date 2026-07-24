using System.Globalization;
using System.Text.RegularExpressions;
using FakturaExtraktion.Models;
using FakturaExtraktion.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FakturaExtraktion.Services;

/// <summary>
/// Gör all aritmetik och regelkontroll i C#, aldrig i modellen:
/// <list type="bullet">
/// <item>belopp_ex_moms + momsbelopp ≈ totalt_belopp (inom en tolerans)</item>
/// <item>organisationsnummer på formatet NNNNNN-NNNN</item>
/// <item>forfallodatum ≥ fakturadatum</item>
/// </list>
/// Dessa hårda kontroller styr <see cref="GranskningsResultat.KräverGranskning"/>
/// och blockerar godkännande. Modellens egna osakra_falt/granskning_kravs
/// hamnar separat i <see cref="GranskningsResultat.ModellNoteringar"/> –
/// informativt, men blockerar inte, eftersom modellen kan flagga saker som
/// visar sig stämma vid närmare granskning.
/// </summary>
public sealed class InvoiceValidator : IInvoiceValidator
{
    private static readonly Regex OrganisationsnummerRegex = new(@"^\d{6}-\d{4}$", RegexOptions.Compiled);

    private readonly ValideringsOptions _options;
    private readonly ILogger<InvoiceValidator> _logger;

    public InvoiceValidator(IOptions<ValideringsOptions> options, ILogger<InvoiceValidator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public GranskningsResultat Validera(ExtraheradFaktura faktura)
    {
        ArgumentNullException.ThrowIfNull(faktura);

        var avvikelser = new List<string>();
        var modellNoteringar = new List<string>();

        LaggTillModellensEgnaFlaggor(faktura, modellNoteringar);
        ValideraSummor(faktura, avvikelser);
        ValideraOrganisationsnummer(faktura, avvikelser);
        ValideraDatum(faktura, avvikelser);

        var kraverGranskning = avvikelser.Count > 0;

        if (kraverGranskning || modellNoteringar.Count > 0)
        {
            _logger.LogWarning(
                "Faktura {Fakturanummer}: kräver granskning={KraverGranskning}, " +
                "{AntalAvvikelser} hård(a) avvikelse(r) [{Avvikelser}], " +
                "{AntalNoteringar} modellnotering(ar) [{ModellNoteringar}]",
                faktura.Fakturanummer ?? "(okänt fakturanummer)",
                kraverGranskning,
                avvikelser.Count,
                string.Join(" | ", avvikelser),
                modellNoteringar.Count,
                string.Join(" | ", modellNoteringar));
        }

        return new GranskningsResultat
        {
            KräverGranskning = kraverGranskning,
            Avvikelser = avvikelser,
            ModellNoteringar = modellNoteringar,
        };
    }

    private static void LaggTillModellensEgnaFlaggor(ExtraheradFaktura faktura, List<string> modellNoteringar)
    {
        if (faktura.OsakraFalt.Count > 0)
        {
            modellNoteringar.Add($"Modellen flaggade osäkra fält: {string.Join(", ", faktura.OsakraFalt)}.");
        }

        // OBS: "noteringar" tas bara med när modellen själv bett om
        // granskning (granskning_kravs=true) eller flaggat osäkra fält.
        // Annars kan noteringar vara en ren bekräftelse ("allt ser bra ut")
        // som inte är värd att visa som notering överhuvudtaget.
        if (faktura.GranskningKravs)
        {
            if (!string.IsNullOrWhiteSpace(faktura.Noteringar))
            {
                modellNoteringar.Add($"Modellens notering: {faktura.Noteringar}");
            }
            else if (faktura.OsakraFalt.Count == 0)
            {
                modellNoteringar.Add("Modellen begärde granskning (granskning_kravs=true) utan angiven notering.");
            }
        }
    }

    private void ValideraSummor(ExtraheradFaktura faktura, List<string> avvikelser)
    {
        if (faktura.BeloppExMoms is { } exMoms && faktura.Momsbelopp is { } moms && faktura.TotaltBelopp is { } totalt)
        {
            var berakningsSumma = exMoms + moms;
            var diff = Math.Abs(berakningsSumma - totalt);

            if (diff > _options.SummaTolerans)
            {
                avvikelser.Add(
                    $"Summan går inte ihop: belopp_ex_moms ({exMoms}) + momsbelopp ({moms}) = " +
                    $"{berakningsSumma}, men totalt_belopp är {totalt} (avvikelse {diff:0.00}).");
            }
        }
        else
        {
            avvikelser.Add(
                "Kan inte summakontrollera: belopp_ex_moms, momsbelopp eller totalt_belopp saknas.");
        }
    }

    private static void ValideraOrganisationsnummer(ExtraheradFaktura faktura, List<string> avvikelser)
    {
        if (faktura.Organisationsnummer is null)
        {
            avvikelser.Add("Organisationsnummer saknas.");
        }
        else if (!OrganisationsnummerRegex.IsMatch(faktura.Organisationsnummer))
        {
            avvikelser.Add(
                $"Organisationsnummer \"{faktura.Organisationsnummer}\" följer inte formatet NNNNNN-NNNN.");
        }
    }

    private static void ValideraDatum(ExtraheradFaktura faktura, List<string> avvikelser)
    {
        var harFakturadatum = TryParseIsoDatum(faktura.Fakturadatum, out var fakturadatum);
        var harForfallodatum = TryParseIsoDatum(faktura.Forfallodatum, out var forfallodatum);

        if (!harFakturadatum || !harForfallodatum)
        {
            avvikelser.Add(
                "Kan inte jämföra datum: fakturadatum eller förfallodatum saknas eller är inte på " +
                "formatet ÅÅÅÅ-MM-DD.");
            return;
        }

        if (forfallodatum < fakturadatum)
        {
            avvikelser.Add(
                $"Förfallodatum ({faktura.Forfallodatum}) ligger före fakturadatum ({faktura.Fakturadatum}).");
        }
    }

    private static bool TryParseIsoDatum(string? varde, out DateOnly datum)
    {
        return DateOnly.TryParseExact(
            varde,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out datum);
    }
}
