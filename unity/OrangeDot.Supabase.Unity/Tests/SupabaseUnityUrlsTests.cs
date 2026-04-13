using OrangeDot.Supabase.Unity;

namespace OrangeDot.Supabase.Unity.Tests;

public sealed class SupabaseUnityUrlsTests
{
    [Fact]
    public void FromProjectUrl_DerivesAuthAndRestEndpoints()
    {
        var urls = SupabaseUnityUrls.FromProjectUrl("https://project-ref.supabase.co/");

        Assert.Equal("https://project-ref.supabase.co", urls.NormalizedProjectUrl);
        Assert.Equal("https://project-ref.supabase.co/auth/v1", urls.AuthUrl);
        Assert.Equal("https://project-ref.supabase.co/rest/v1", urls.RestUrl);
    }

    [Fact]
    public void FromProjectUrl_PreservesExplicitPortAndBasePath()
    {
        var urls = SupabaseUnityUrls.FromProjectUrl("http://localhost:54321/custom-base");

        Assert.Equal("http://localhost:54321/custom-base", urls.NormalizedProjectUrl);
        Assert.Equal("http://localhost:54321/custom-base/auth/v1", urls.AuthUrl);
        Assert.Equal("http://localhost:54321/custom-base/rest/v1", urls.RestUrl);
    }
}
