using System;

namespace OrangeDot.Supabase.Unity
{
public sealed class SupabaseUnityUrls
{
    private SupabaseUnityUrls(
        string normalizedProjectUrl,
        string authUrl,
        string restUrl,
        string functionsUrl,
        string storageUrl)
    {
        NormalizedProjectUrl = normalizedProjectUrl;
        AuthUrl = authUrl;
        RestUrl = restUrl;
        FunctionsUrl = functionsUrl;
        StorageUrl = storageUrl;
    }

    public string NormalizedProjectUrl { get; }

    public string AuthUrl { get; }

    public string RestUrl { get; }

    public string FunctionsUrl { get; }

    public string StorageUrl { get; }

    public static SupabaseUnityUrls FromProjectUrl(string projectUrl)
    {
        if (string.IsNullOrWhiteSpace(projectUrl))
        {
            throw new ArgumentException("Project URL is required.", nameof(projectUrl));
        }

        if (!Uri.TryCreate(projectUrl, UriKind.Absolute, out var parsed))
        {
            throw new ArgumentException("Project URL must be a valid absolute URI.", nameof(projectUrl));
        }

        if (parsed.Scheme != "http" && parsed.Scheme != "https")
        {
            throw new ArgumentException("Project URL scheme must be http or https.", nameof(projectUrl));
        }

        var normalizedProject = NormalizeProjectUri(parsed);
        var normalizedProjectUrl = normalizedProject.GetLeftPart(UriPartial.Path).TrimEnd('/');
        var hasExplicitPort = HasExplicitPort(projectUrl);

        return new SupabaseUnityUrls(
            normalizedProjectUrl,
            BuildEndpoint(normalizedProject, normalizedProject.Scheme, "auth/v1", hasExplicitPort),
            BuildEndpoint(normalizedProject, normalizedProject.Scheme, "rest/v1", hasExplicitPort),
            BuildEndpoint(normalizedProject, normalizedProject.Scheme, "functions/v1", hasExplicitPort),
            BuildEndpoint(normalizedProject, normalizedProject.Scheme, "storage/v1", hasExplicitPort));
    }

    private static Uri NormalizeProjectUri(Uri parsed)
    {
        var builder = new UriBuilder(parsed)
        {
            Path = parsed.AbsolutePath == "/" ? "/" : "/" + parsed.AbsolutePath.Trim('/')
        };

        return builder.Uri;
    }

    private static bool HasExplicitPort(string projectUrl)
    {
        var schemeSeparatorIndex = projectUrl.IndexOf("://", StringComparison.Ordinal);

        if (schemeSeparatorIndex < 0)
        {
            return false;
        }

        var authorityStart = schemeSeparatorIndex + 3;
        var authorityEnd = projectUrl.IndexOfAny(new[] { '/', '?', '#' }, authorityStart);
        var authority = authorityEnd >= 0
            ? projectUrl.Substring(authorityStart, authorityEnd - authorityStart)
            : projectUrl.Substring(authorityStart);

        if (string.IsNullOrEmpty(authority))
        {
            return false;
        }

        var hostPort = authority;
        var userInfoSeparatorIndex = hostPort.LastIndexOf('@');

        if (userInfoSeparatorIndex >= 0)
        {
            hostPort = hostPort.Substring(userInfoSeparatorIndex + 1);
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
}
