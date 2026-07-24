namespace FakturaExtraktion.Services;

/// <summary>
/// Kastas när fakturaextraktion via Anthropics Messages API misslyckas –
/// t.ex. vid API-fel (icke-2xx-svar), uteblivet tool_use-block eller
/// ogiltig/oväntad JSON i verktygsanropets input.
/// </summary>
public sealed class FakturaExtraktionException : Exception
{
    public FakturaExtraktionException(string message)
        : base(message)
    {
    }

    public FakturaExtraktionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
