using System;
using OrangeDot.Supabase.Errors;
using GotrueAuthState = global::Supabase.Gotrue.Constants.AuthState;

namespace OrangeDot.Supabase.Internal;

internal static class SessionStoreAuthExceptions
{
    internal static SupabaseAuthException Create(
        GotrueAuthState source,
        bool persist,
        Exception exception)
    {
        return source switch
        {
            GotrueAuthState.SignedIn => new SupabaseAuthException(
                SupabaseErrorCode.AuthFailed,
                persist ? "Failed to persist auth session after sign-in." : "Failed to clear persisted auth session after sign-in.",
                operation: "SignIn",
                innerException: exception),
            GotrueAuthState.TokenRefreshed => new SupabaseAuthException(
                SupabaseErrorCode.AuthRefreshFailed,
                persist ? "Failed to persist refreshed auth session." : "Failed to clear persisted auth session after refresh.",
                operation: "RefreshSession",
                innerException: exception),
            GotrueAuthState.SignedOut => new SupabaseAuthException(
                SupabaseErrorCode.AuthStatePropagationFailed,
                "Failed to clear persisted auth session after sign-out.",
                operation: "SignOut",
                innerException: exception),
            GotrueAuthState.UserUpdated => new SupabaseAuthException(
                SupabaseErrorCode.AuthStatePropagationFailed,
                persist ? "Failed to persist updated auth session." : "Failed to clear persisted auth session after user update.",
                operation: "UserUpdated",
                innerException: exception),
            _ => new SupabaseAuthException(
                SupabaseErrorCode.AuthStatePropagationFailed,
                persist ? "Failed to persist auth session state." : "Failed to clear persisted auth session state.",
                operation: source.ToString(),
                innerException: exception)
        };
    }
}
