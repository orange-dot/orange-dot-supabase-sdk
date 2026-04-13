using System;
using OrangeDot.Supabase.Errors;
using Xunit;

namespace OrangeDot.Supabase.Tests.Errors;

public sealed class SupabaseExceptionTests
{
    [Fact]
    public void Configuration_exception_preserves_metadata()
    {
        var exception = new SupabaseConfigurationException(
            SupabaseErrorCode.ConfigurationInvalid,
            "Invalid configuration.",
            module: "Postgrest",
            operation: "Configure",
            correlationId: "corr-123");

        Assert.Equal(SupabaseErrorCode.ConfigurationInvalid, exception.ErrorCode);
        Assert.Equal("Postgrest", exception.Module);
        Assert.Equal("Configure", exception.Operation);
        Assert.Equal("corr-123", exception.CorrelationId);
        Assert.IsAssignableFrom<SupabaseException>(exception);
    }

    [Fact]
    public void Auth_exception_defaults_module_to_auth()
    {
        var exception = new SupabaseAuthException(
            SupabaseErrorCode.AuthFailed,
            "Auth failed.");

        Assert.Equal("Auth", exception.Module);
        Assert.IsAssignableFrom<SupabaseException>(exception);
    }

    [Fact]
    public void Request_exception_preserves_metadata_and_http_details()
    {
        var innerException = new InvalidOperationException("inner");
        var exception = new SupabaseRequestException(
            SupabaseErrorCode.RequestForbidden,
            "Request failed.",
            module: "Postgrest",
            operation: "Select",
            httpStatusCode: 403,
            responseBody: "{\"message\":\"forbidden\"}",
            correlationId: "corr-456",
            innerException: innerException);

        Assert.Equal(SupabaseErrorCode.RequestForbidden, exception.ErrorCode);
        Assert.Equal("Postgrest", exception.Module);
        Assert.Equal("Select", exception.Operation);
        Assert.Equal(403, exception.HttpStatusCode);
        Assert.Equal("{\"message\":\"forbidden\"}", exception.ResponseBody);
        Assert.Equal("corr-456", exception.CorrelationId);
        Assert.Same(innerException, exception.InnerException);
        Assert.IsAssignableFrom<SupabaseException>(exception);
    }

    [Fact]
    public void Correlation_id_is_generated_when_omitted()
    {
        var exception = new SupabaseException(
            SupabaseErrorCode.Unknown,
            "Unknown failure.");

        Assert.False(string.IsNullOrWhiteSpace(exception.CorrelationId));
        Assert.Equal(exception.CorrelationId, exception.CorrelationId);
    }

    [Fact]
    public void Request_exception_rejects_null_module()
    {
        Assert.Throws<ArgumentNullException>(() => new SupabaseRequestException(
            SupabaseErrorCode.RequestFailed,
            "Request failed.",
            null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Request_exception_rejects_empty_or_whitespace_module(string module)
    {
        Assert.Throws<ArgumentException>(() => new SupabaseRequestException(
            SupabaseErrorCode.RequestFailed,
            "Request failed.",
            module));
    }

    [Fact]
    public void Message_and_error_code_round_trip_through_constructor()
    {
        var exception = new SupabaseAuthException(
            SupabaseErrorCode.AuthRefreshFailed,
            "Refresh failed.",
            operation: "Refresh");

        Assert.Equal("Refresh failed.", exception.Message);
        Assert.Equal(SupabaseErrorCode.AuthRefreshFailed, exception.ErrorCode);
        Assert.Equal("Refresh", exception.Operation);
    }
}
