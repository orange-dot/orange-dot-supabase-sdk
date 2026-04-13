using System;
using System.Text;

namespace OrangeDot.Supabase.Auth;

public abstract record AuthState(long CanonicalVersion)
{
    public sealed record Anonymous() : AuthState(0);

    public sealed record Authenticated(
        long CanonicalVersion,
        string AccessToken,
        string RefreshToken,
        DateTimeOffset ExpiresAt) : AuthState(CanonicalVersion)
    {
        public Authenticated(
            string AccessToken,
            string RefreshToken,
            DateTimeOffset ExpiresAt)
            : this(0, AccessToken, RefreshToken, ExpiresAt)
        {
        }

        protected override bool PrintMembers(StringBuilder builder)
        {
            builder.Append(nameof(CanonicalVersion));
            builder.Append(" = ");
            builder.Append(CanonicalVersion);
            builder.Append(", ");
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

    public sealed record Refreshing(
        long CanonicalVersion,
        long PendingRefreshVersion,
        string AccessToken,
        string RefreshToken,
        DateTimeOffset ExpiresAt) : AuthState(CanonicalVersion)
    {
        protected override bool PrintMembers(StringBuilder builder)
        {
            builder.Append(nameof(CanonicalVersion));
            builder.Append(" = ");
            builder.Append(CanonicalVersion);
            builder.Append(", ");
            builder.Append(nameof(PendingRefreshVersion));
            builder.Append(" = ");
            builder.Append(PendingRefreshVersion);
            builder.Append(", ");
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

    public sealed record SignedOut(long CanonicalVersion) : AuthState(CanonicalVersion)
    {
        public SignedOut()
            : this(0)
        {
        }
    }

    public sealed record Faulted(
        long CanonicalVersion,
        long PendingRefreshVersion,
        string Reason) : AuthState(CanonicalVersion)
    {
        public Faulted(string Reason)
            : this(0, 0, Reason)
        {
        }
    }
}
