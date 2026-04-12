using System;
using System.Linq;

namespace OrangeDot.Supabase.Internal;

internal static class SupabaseKeyClassifier
{
    internal static bool ShouldSendBearerAuthorization(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        return LooksLikeJwt(apiKey);
    }

    private static bool LooksLikeJwt(string value)
    {
        var segments = value.Split('.', StringSplitOptions.None);
        return segments.Length == 3 && segments.All(static segment =>
            segment.Length > 0 &&
            segment.All(static ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'));
    }
}
