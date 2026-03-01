namespace Tracker.Api.Endpoints;

public static partial class IngestEndpoints
{
    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return trimmed[..maxLength];
    }

    private static bool TryNormalizeRequired(string? value, int maxLength, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            trimmed = trimmed[..maxLength];
        }

        normalized = trimmed;
        return true;
    }
}
