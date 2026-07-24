using System.Text;
using Microsoft.Extensions.Logging;

namespace FakturaExtraktion.Services;

/// <summary>
/// Läser fakturatext från en fil på disk. Antagande: filen är redan ren text
/// (UTF-8), t.ex. resultatet av en tidigare OCR/PDF-textextraktion. Denna
/// klass gör alltså ingen PDF-tolkning – det är avsiktligt utanför scope för
/// detta steg (se uppdragets "Gör INTE i detta steg").
/// </summary>
public sealed class FilTextExtractor : IPdfTextExtractor
{
    private readonly ILogger<FilTextExtractor> _logger;

    public FilTextExtractor(ILogger<FilTextExtractor> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExtraheraTextAsync(string filsokvag, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filsokvag);

        if (!File.Exists(filsokvag))
        {
            throw new FileNotFoundException($"Hittade ingen fakturafil på sökvägen \"{filsokvag}\".", filsokvag);
        }

        _logger.LogInformation("Läser fakturatext från {Filsokvag}", filsokvag);

        var text = await File.ReadAllTextAsync(filsokvag, Encoding.UTF8, cancellationToken);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"Fakturafilen \"{filsokvag}\" är tom.");
        }

        return text;
    }
}
