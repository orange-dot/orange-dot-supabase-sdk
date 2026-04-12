using OrangeDot.Supabase.Internal;

namespace OrangeDot.Supabase.Tests.Configuration;

public sealed class SupabaseKeyClassifierTests
{
    [Theory]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJyb2xlIjoiYW5vbiJ9.signature", true)]
    [InlineData("sb_publishable_123", false)]
    [InlineData("sb_secret_123", false)]
    [InlineData("publishable-key", false)]
    public void Should_send_bearer_authorization_matches_expected_key_shape(string apiKey, bool expected)
    {
        var actual = SupabaseKeyClassifier.ShouldSendBearerAuthorization(apiKey);

        Assert.Equal(expected, actual);
    }
}
