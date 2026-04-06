using System;
using System.Text;

namespace OrangeDot.Supabase.Auth;

public abstract record AuthState
{
    public sealed record Anonymous : AuthState;

    public sealed record Authenticated(
        string AccessToken,
        string RefreshToken,
        DateTimeOffset ExpiresAt) : AuthState
    {
        protected override bool PrintMembers(StringBuilder builder)
        {
            builder.Append(nameof(AccessToken));
            builder.Append(" = [redacted], ");
            builder.Append(nameof(RefreshToken));
            builder.Append(" = [redacted], ");
            builder.Append(nameof(ExpiresAt));
            builder.Append(" = ");
            builder.Append(ExpiresAt.ToString("O"));

            return true;
        }
    }

    public sealed record SignedOut : AuthState;
}
