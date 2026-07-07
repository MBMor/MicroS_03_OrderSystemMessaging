namespace Observability.Shared.Correlation;

public static class CorrelationIdValidator
{
    public static string? Normalize(string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return null;
        }

        var normalized = correlationId.Trim();

        return IsValid(normalized)
            ? normalized
            : null;
    }

    public static bool IsValid(string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return false;
        }

        var normalized = correlationId.Trim();

        if (normalized.Length < CorrelationIdConstants.MinLength)
        {
            return false;
        }

        if (normalized.Length > CorrelationIdConstants.MaxLength)
        {
            return false;
        }

        foreach (var character in normalized)
        {
            if (!IsAllowedCharacter(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAllowedCharacter(char character)
    {
        return character is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '-'
            or '_'
            or '.';
    }
}