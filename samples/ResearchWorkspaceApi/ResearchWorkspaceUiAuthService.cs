using System.Net.Http.Json;
using System.Security.Authentication;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OrangeDot.Supabase;

namespace ResearchWorkspaceApi;

public sealed class ResearchWorkspaceUiAuthService
{
    private readonly HttpClient _httpClient;
    private readonly SupabaseServerOptions _options;

    public ResearchWorkspaceUiAuthService(HttpClient httpClient, IOptions<SupabaseServerOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        if (Uri.TryCreate(_options.Url?.TrimEnd('/'), UriKind.Absolute, out var baseAddress))
        {
            _httpClient.BaseAddress = baseAddress;
        }
    }

    public async Task<UiSessionResponse> SignUpAsync(UiPasswordAuthRequest request, CancellationToken cancellationToken)
    {
        var normalizedRequest = NormalizeRequest(request);

        using (await SendAsync("/auth/v1/signup", normalizedRequest, cancellationToken))
        {
        }

        return await SignInAsync(normalizedRequest, cancellationToken);
    }

    public async Task<UiSessionResponse> SignInAsync(UiPasswordAuthRequest request, CancellationToken cancellationToken)
    {
        var normalizedRequest = NormalizeRequest(request);

        using var response = await SendAsync("/auth/v1/token?grant_type=password", normalizedRequest, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var root = document.RootElement;
        var user = root.TryGetProperty("user", out var userElement) ? userElement : default;
        var email = TryGetString(user, "email") ?? normalizedRequest.Email;

        return new UiSessionResponse(
            GetRequiredString(user, "id"),
            email,
            GetRequiredString(root, "access_token"),
            TryGetString(root, "token_type") ?? "bearer",
            root.TryGetProperty("expires_in", out var expiresIn) && expiresIn.TryGetInt32(out var value) ? value : 3600);
    }

    private async Task<HttpResponseMessage> SendAsync(string relativePath, UiPasswordAuthRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, relativePath)
        {
            Content = JsonContent.Create(request)
        };

        message.Headers.TryAddWithoutValidation("apikey", _options.PublishableKey);
        var response = await _httpClient.SendAsync(message, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var (status, error, detail) = await ParseErrorAsync(response, cancellationToken);
        response.Dispose();
        throw new UiAuthException(status, error, detail);
    }

    private static UiPasswordAuthRequest NormalizeRequest(UiPasswordAuthRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var email = request.Email?.Trim();
        var password = request.Password?.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("'email' is required.", nameof(request.Email));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("'password' is required.", nameof(request.Password));
        }

        return new UiPasswordAuthRequest(email, password);
    }

    private static async Task<(int Status, string Error, string Detail)> ParseErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                using var document = JsonDocument.Parse(responseBody);
                var root = document.RootElement;
                var detail = TryGetString(root, "msg")
                    ?? TryGetString(root, "message")
                    ?? TryGetString(root, "error_description")
                    ?? TryGetString(root, "error")
                    ?? response.ReasonPhrase
                    ?? "Supabase Auth returned an error.";
                var error = NormalizeError(root, (int)response.StatusCode);
                return (NormalizeStatusCode((int)response.StatusCode), error, detail);
            }
            catch (JsonException)
            {
                // Fall back to the raw response body below.
            }
        }

        var status = NormalizeStatusCode((int)response.StatusCode);
        return (status, NormalizeError(default, status), responseBody.Trim());
    }

    private static int NormalizeStatusCode(int statusCode)
    {
        return statusCode switch
        {
            422 => 400,
            0 => 500,
            _ => statusCode
        };
    }

    private static string NormalizeError(JsonElement root, int statusCode)
    {
        var status = NormalizeStatusCode(statusCode);
        var upstream = TryGetString(root, "error_code") ?? TryGetString(root, "error");

        return status switch
        {
            400 => "invalid_request",
            401 => "auth_invalid",
            403 => "forbidden",
            409 => "conflict",
            _ when string.IsNullOrWhiteSpace(upstream) => "ui_auth_error",
            _ => upstream!.Trim()
        };
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        var value = TryGetString(element, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AuthenticationException($"Supabase Auth response did not include '{propertyName}'.");
        }

        return value;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }
}

public sealed class UiAuthException : Exception
{
    public UiAuthException(int status, string error, string detail)
        : base(detail)
    {
        Status = status;
        Error = string.IsNullOrWhiteSpace(error) ? "ui_auth_error" : error;
        Detail = string.IsNullOrWhiteSpace(detail) ? "Supabase Auth returned an error." : detail;
    }

    public int Status { get; }

    public string Error { get; }

    public string Detail { get; }
}
