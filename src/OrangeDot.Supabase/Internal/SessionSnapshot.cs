using System;

namespace OrangeDot.Supabase.Internal;

internal readonly record struct SessionSnapshot(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);
