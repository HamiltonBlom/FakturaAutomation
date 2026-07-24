namespace FakturaExtraktion.Models;

/// <summary>
/// Resultat av C#-kodens egna aritmetiska och strukturella valideringar,
/// plus modellens egna osäkerhetssignaler – hållna separata med avsikt:
/// <list type="bullet">
/// <item><see cref="Avvikelser"/> kommer enbart från hårda, deterministiska
/// C#-kontroller (summa, organisationsnummer, datumordning) och styr
/// <see cref="KräverGranskning"/>, som blockerar godkännande.</item>
/// <item><see cref="ModellNoteringar"/> kommer enbart från modellens egna
/// flaggor (<see cref="ExtraheradFaktura.GranskningKravs"/>,
/// <see cref="ExtraheradFaktura.OsakraFalt"/>, <see cref="ExtraheradFaktura.Noteringar"/>).
/// De visas för granskaren men blockerar INTE godkännande – modellen kan
/// flagga saker som vid närmare granskning visar sig stämma.</item>
/// </list>
/// </summary>
public sealed record GranskningsResultat
{
    /// <summary>True om en hård regel (aritmetik/format/datumordning) bröts. Blockerar godkännande.</summary>
    public required bool KräverGranskning { get; init; }

    /// <summary>Hårda, blockerande avvikelser från C#:s egna kontroller (tom om inga).</summary>
    public required IReadOnlyList<string> Avvikelser { get; init; }

    /// <summary>Modellens egna, icke-blockerande osäkerhetssignaler (tom om inga).</summary>
    public required IReadOnlyList<string> ModellNoteringar { get; init; }
}
