using System.Text.Encodings.Web;
using System.Text.Json;
using FakturaExtraktion.Models;
using Microsoft.Extensions.Logging;

namespace FakturaExtraktion.Web.Services;

/// <inheritdoc cref="IExportService" />
public sealed class ExportService : IExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly ILogger<ExportService> _logger;

    public ExportService(ILogger<ExportService> logger)
    {
        _logger = logger;
    }

    public Task ExporteraAsync(ExtraheradFaktura faktura, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(faktura, JsonOptions);

        // Stub: i en riktig integration skulle det här vara ett API-anrop
        // eller en filexport till ekonomisystemet. Här loggar vi bara.
        _logger.LogInformation(
            "Exporterar godkänd faktura {Fakturanummer} till ekonomisystem (stub):\n{Json}",
            faktura.Fakturanummer ?? "(okänt fakturanummer)",
            json);

        return Task.CompletedTask;
    }
}
