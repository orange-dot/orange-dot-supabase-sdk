using System;

namespace OrangeDot.Supabase.Errors;

public sealed class SupabaseAuthException : SupabaseException
{
    public SupabaseAuthException(
        SupabaseErrorCode errorCode,
        string message,
        string? operation = null,
        string? module = "Auth",
        string? correlationId = null,
        Exception? innerException = null)
        : base(
            errorCode,
            message,
            module: string.IsNullOrWhiteSpace(module) ? "Auth" : module,
            operation: operation,
            correlationId: correlationId,
            innerException: innerException)
    {
    }
}
