using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace OrangeDot.Supabase.Tests.Spec;

internal static class SpecVectorTestSupport
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    internal static TheoryData<string> CreateVectorPathTheoryData(string domain)
    {
        return new TheoryData<string>(FindVectorPaths(domain).ToArray());
    }

    internal static IReadOnlyList<string> FindVectorPaths(string domain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        var repoRoot = FindRepoRoot(domain);
        var vectorsPath = Path.Combine(repoRoot, "spec", "test-vectors", domain);

        return Directory.GetFiles(vectorsPath, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    internal static T Deserialize<T>(string vectorPath)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vectorPath);

        var json = File.ReadAllText(vectorPath);
        var vector = JsonSerializer.Deserialize<T>(json, SerializerOptions);

        return vector ?? throw new InvalidOperationException($"Failed to deserialize vector: {vectorPath}");
    }

    private static string FindRepoRoot(string domain)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "OrangeDot.Supabase.sln");
            var specPath = Path.Combine(current.FullName, "spec", "test-vectors", domain);

            if (File.Exists(solutionPath) && Directory.Exists(specPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test execution directory.");
    }
}
