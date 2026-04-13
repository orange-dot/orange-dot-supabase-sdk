using System;

namespace OrangeDot.Supabase.Errors;

public sealed class SupabaseRequestException : SupabaseException
{
    public SupabaseRequestException(
        SupabaseErrorCode errorCode,
        string message,
        string module,
        string? operation = null,
        int? httpStatusCode = null,
        string? responseBody = null,
        string? correlationId = null,
        Exception? innerException = null)
        : base(
            errorCode,
            message,
            module: ValidateModule(module),
            operation: operation,
            correlationId: correlationId,
            innerException: innerException)
    {
        HttpStatusCode = httpStatusCode;
        ResponseBody = responseBody;
    }

    public int? HttpStatusCode { get; }

    public string? ResponseBody { get; }

    private static string ValidateModule(string module)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);

        return module;
    }
}
