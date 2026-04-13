using System;

namespace OrangeDot.Supabase.Errors;

public sealed class SupabaseConfigurationException : SupabaseException
{
    public SupabaseConfigurationException(
        SupabaseErrorCode errorCode,
        string message,
        string? module = null,
        string? operation = null,
        string? correlationId = null,
        Exception? innerException = null)
        : base(
            errorCode,
            message,
            module: module,
            operation: operation,
            correlationId: correlationId,
            innerException: innerException)
    {
    }
}
