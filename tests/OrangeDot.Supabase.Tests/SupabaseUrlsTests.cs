using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OrangeDot.Supabase.Urls;
using Xunit;

namespace OrangeDot.Supabase.Tests;

public sealed class SupabaseUrlsTests
{
    public static TheoryData<string> UrlVectorPaths =>
        new(FindUrlVectorPaths().ToArray());

    [Theory]
    [MemberData(nameof(UrlVectorPaths))]
    public void FromBaseUrl_matches_expected_values_from_vector(string vectorPath)
    {
        var vector = DeserializeVector(vectorPath);

        var actual = SupabaseUrls.FromBaseUrl(vector.InitialState.BaseUrl);

        Assert.Equal(vector.Expected.NormalizedBaseUrl, actual.NormalizedBaseUrl);
        Assert.Equal(vector.Expected.AuthUrl, actual.AuthUrl);
        Assert.Equal(vector.Expected.RestUrl, actual.RestUrl);
        Assert.Equal(vector.Expected.RealtimeUrl, actual.RealtimeUrl);
        Assert.Equal(vector.Expected.StorageUrl, actual.StorageUrl);
        Assert.Equal(vector.Expected.FunctionsUrl, actual.FunctionsUrl);
    }

    [Fact]
    public void FromBaseUrl_throws_for_invalid_uri_string()
    {
        Assert.Throws<ArgumentException>(() => SupabaseUrls.FromBaseUrl("not a url"));
    }

    [Fact]
    public void FromBaseUrl_throws_for_relative_uri()
    {
        Assert.Throws<ArgumentException>(() => SupabaseUrls.FromBaseUrl("/relative/path"));
    }

    [Fact]
    public void FromBaseUrl_throws_for_unsupported_scheme()
    {
        Assert.Throws<ArgumentException>(() => SupabaseUrls.FromBaseUrl("ftp://example.com"));
    }

    [Fact]
    public void FromBaseUrl_throws_for_blank_input()
    {
        Assert.Throws<ArgumentException>(() => SupabaseUrls.FromBaseUrl(" "));
    }

    [Fact]
    public void FromBaseUrl_throws_for_null_input()
    {
        Assert.Throws<ArgumentNullException>(() => SupabaseUrls.FromBaseUrl(null!));
    }

    [Fact]
    public void FromBaseUrl_does_not_add_default_realtime_port_when_base_has_no_explicit_port()
    {
        var urls = SupabaseUrls.FromBaseUrl("https://abc.supabase.co");

        Assert.Equal("wss://abc.supabase.co/realtime/v1", urls.RealtimeUrl);
    }

    private static UrlVector DeserializeVector(string vectorPath)
    {
        var json = File.ReadAllText(vectorPath);
        var vector = JsonSerializer.Deserialize<UrlVector>(json, SerializerOptions);

        return vector ?? throw new InvalidOperationException($"Failed to deserialize vector: {vectorPath}");
    }

    private static IEnumerable<string> FindUrlVectorPaths()
    {
        var repoRoot = FindRepoRoot();
        var vectorsPath = Path.Combine(repoRoot, "spec", "test-vectors", "urls");

        return Directory.GetFiles(vectorsPath, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "OrangeDot.Supabase.sln");
            var specPath = Path.Combine(current.FullName, "spec", "test-vectors", "urls");

            if (File.Exists(solutionPath) && Directory.Exists(specPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test execution directory.");
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public sealed class UrlVector
    {
        public required string Id { get; init; }

        public required string Title { get; init; }

        public required UrlVectorInitialState InitialState { get; init; }

        public required UrlVectorExpected Expected { get; init; }
    }

    public sealed class UrlVectorInitialState
    {
        public required string BaseUrl { get; init; }
    }

    public sealed class UrlVectorExpected
    {
        public required string NormalizedBaseUrl { get; init; }

        public required string AuthUrl { get; init; }

        public required string RestUrl { get; init; }

        public required string RealtimeUrl { get; init; }

        public required string StorageUrl { get; init; }

        public required string FunctionsUrl { get; init; }
    }
}
