namespace OrangeDot.Supabase.Errors;

public enum SupabaseErrorCode
{
    Unknown = 0,
    ConfigurationInvalid,
    ConfigurationMissing,
    AuthFailed,
    AuthSessionLoadFailed,
    AuthRefreshFailed,
    AuthStatePropagationFailed,
    RequestFailed,
    RequestUnauthorized,
    RequestForbidden,
    RequestTimedOut,
    NetworkUnavailable,
    SerializationFailed,
    ReadinessGateFailed
}
