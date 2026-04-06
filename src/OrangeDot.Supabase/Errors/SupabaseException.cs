using System;

namespace OrangeDot.Supabase.Errors;

public class SupabaseException : Exception
{
    public SupabaseException(
        SupabaseErrorCode errorCode,
        string message,
        string? module = null,
        string? operation = null,
        string? correlationId = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Module = module;
        Operation = operation;
        CorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId;
    }

    public SupabaseErrorCode ErrorCode { get; }

    public string? Module { get; }

    public string? Operation { get; }

    public string CorrelationId { get; }
}
