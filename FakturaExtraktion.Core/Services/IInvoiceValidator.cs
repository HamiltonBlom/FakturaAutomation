using FakturaExtraktion.Models;

namespace FakturaExtraktion.Services;

/// <summary>
/// Validerar en extraherad faktura. All aritmetik och regelkontroll sker i
/// C# – modellen (AI-laget) ombeds aldrig räkna eller avgöra giltighet.
/// </summary>
public interface IInvoiceValidator
{
    /// <summary>
    /// Kör samtliga kontroller och returnerar ett sammanslaget resultat av
    /// modellens egna osäkerhetsflaggor och kodens egna valideringar.
    /// </summary>
    GranskningsResultat Validera(ExtraheradFaktura faktura);
}
