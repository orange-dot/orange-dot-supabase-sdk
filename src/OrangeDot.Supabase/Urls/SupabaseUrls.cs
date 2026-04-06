using System;

namespace OrangeDot.Supabase.Urls;

public sealed record SupabaseUrls
{
    private SupabaseUrls(
        string normalizedBaseUrl,
        string authUrl,
        string restUrl,
        string realtimeUrl,
        string storageUrl,
        string functionsUrl)
    {
        NormalizedBaseUrl = normalizedBaseUrl;
        AuthUrl = authUrl;
        RestUrl = restUrl;
        RealtimeUrl = realtimeUrl;
        StorageUrl = storageUrl;
        FunctionsUrl = functionsUrl;
    }

    public string NormalizedBaseUrl { get; }

    public string AuthUrl { get; }

    public string RestUrl { get; }

    public string RealtimeUrl { get; }

    public string StorageUrl { get; }

    public string FunctionsUrl { get; }

    public static SupabaseUrls FromBaseUrl(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed))
        {
            throw new ArgumentException("Base URL must be a valid absolute URI.", nameof(baseUrl));
        }

        if (parsed.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("Base URL scheme must be http or https.", nameof(baseUrl));
        }

        var normalizedBase = NormalizeBaseUri(parsed);
        var normalizedBaseUrl = FormatNormalizedBaseUrl(normalizedBase);
        var hasExplicitPort = HasExplicitPort(baseUrl);

        return new SupabaseUrls(
            normalizedBaseUrl,
            BuildEndpoint(normalizedBase, normalizedBase.Scheme, "auth/v1", hasExplicitPort),
            BuildEndpoint(normalizedBase, normalizedBase.Scheme, "rest/v1", hasExplicitPort),
            BuildEndpoint(normalizedBase, normalizedBase.Scheme == "https" ? "wss" : "ws", "realtime/v1", hasExplicitPort),
            BuildEndpoint(normalizedBase, normalizedBase.Scheme, "storage/v1", hasExplicitPort),
            BuildEndpoint(normalizedBase, normalizedBase.Scheme, "functions/v1", hasExplicitPort));
    }

    private static Uri NormalizeBaseUri(Uri parsed)
    {
        var builder = new UriBuilder(parsed)
        {
            Path = parsed.AbsolutePath == "/" ? "/" : "/" + parsed.AbsolutePath.Trim('/')
        };

        return builder.Uri;
    }

    private static string FormatNormalizedBaseUrl(Uri baseUri)
    {
        return baseUri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }

    private static bool HasExplicitPort(string baseUrl)
    {
        var schemeSeparatorIndex = baseUrl.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparatorIndex < 0)
        {
            return false;
        }

        var authorityStart = schemeSeparatorIndex + 3;
        var authorityEnd = baseUrl.IndexOfAny(['/', '?', '#'], authorityStart);
        var authority = authorityEnd >= 0
            ? baseUrl[authorityStart..authorityEnd]
            : baseUrl[authorityStart..];

        if (string.IsNullOrEmpty(authority))
        {
            return false;
        }

        var hostPort = authority;
        var userInfoSeparatorIndex = hostPort.LastIndexOf('@');
        if (userInfoSeparatorIndex >= 0)
        {
            hostPort = hostPort[(userInfoSeparatorIndex + 1)..];
        }

        if (hostPort.StartsWith("[", StringComparison.Ordinal))
        {
            var closingBracketIndex = hostPort.IndexOf(']');

            return closingBracketIndex >= 0
                && closingBracketIndex + 1 < hostPort.Length
                && hostPort[closingBracketIndex + 1] == ':';
        }

        return hostPort.Contains(':');
    }

    private static string BuildEndpoint(Uri baseUri, string scheme, string relativePath, bool hasExplicitPort)
    {
        var builder = new UriBuilder(baseUri)
        {
            Scheme = scheme,
            Path = CombinePath(baseUri.AbsolutePath, relativePath)
        };

        if (!hasExplicitPort && baseUri.IsDefaultPort)
        {
            builder.Port = -1;
        }

        return builder.Uri.AbsoluteUri;
    }

    private static string CombinePath(string basePath, string relativePath)
    {
        var normalizedBasePath = basePath == "/" ? string.Empty : basePath.Trim('/');
        var normalizedRelativePath = relativePath.Trim('/');

        return string.IsNullOrEmpty(normalizedBasePath)
            ? "/" + normalizedRelativePath
            : "/" + normalizedBasePath + "/" + normalizedRelativePath;
    }
}
