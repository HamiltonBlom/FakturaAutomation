namespace FakturaExtraktion.Web.Components;

/// <summary>
/// Allvarlighetsgrad för ett fälts markering i granskningsvyn. Två skilda
/// källor ger två skilda nivåer, med avsikt: <see cref="Varning"/> kommer
/// enbart från C#:s hårda validering (blockerar godkännande) och
/// <see cref="Info"/> enbart från modellens egna, icke-blockerande
/// osäkerhetssignaler – så att granskaren visuellt kan skilja "matten
/// stämmer inte" från "Claude har en synpunkt".
/// </summary>
public enum FaltNiva
{
    /// <summary>Inget att flagga.</summary>
    Ok,

    /// <summary>Modellens egen, icke-blockerande notering (osakra_falt/noteringar).</summary>
    Info,

    /// <summary>Hård, blockerande avvikelse från C#:s validering.</summary>
    Varning,
}
