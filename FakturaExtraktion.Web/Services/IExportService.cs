using FakturaExtraktion.Models;

namespace FakturaExtraktion.Web.Services;

/// <summary>
/// Stub för att skicka en godkänd faktura vidare till ett ekonomisystem.
/// Enligt uppdraget: ingen riktig integration i det här steget – en
/// implementation som loggar/skriver ut JSON räcker.
/// </summary>
public interface IExportService
{
    Task ExporteraAsync(ExtraheradFaktura faktura, CancellationToken cancellationToken = default);
}
