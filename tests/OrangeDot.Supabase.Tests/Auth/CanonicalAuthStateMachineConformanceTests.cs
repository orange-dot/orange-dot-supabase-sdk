using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using OrangeDot.Supabase.Auth;
using OrangeDot.Supabase.Internal;
using GotrueAuthState = global::Supabase.Gotrue.Constants.AuthState;

namespace OrangeDot.Supabase.Tests.Auth;

public sealed class CanonicalAuthStateMachineConformanceTests
{
    [Fact]
    public void Canonical_state_machine_matches_bridge_for_sign_in_refresh_sign_out_sequence()
    {
        var auth = CreateAuthClient();
        var observer = new AuthStateObserver();
        var published = new List<AuthState>();
        using var subscription = observer.Subscribe(published.Add);
        using var bridge = new GotrueAuthStateBridge(auth, observer, NullLogger<GotrueAuthStateBridge>.Instance, metrics: null);
        var machine = new CanonicalAuthStateMachine();

        published.Clear();

        var expected = new List<AuthState>();

        ApplyStep(
            auth,
            machine,
            expected,
            GotrueAuthState.SignedIn,
            CreateSession("signed-in-token", "refresh-token", 1800));
        ApplyStep(
            auth,
            machine,
            expected,
            GotrueAuthState.TokenRefreshed,
            CreateSession("refreshed-token", "refresh-token-2", 3600));
        ApplyStep(
            auth,
            machine,
            expected,
            GotrueAuthState.SignedOut,
            session: null);
        ApplyStep(
            auth,
            machine,
            expected,
            GotrueAuthState.TokenRefreshed,
            CreateSession("stale-token", "stale-refresh-token", 3600));

        Assert.Equal(Describe(expected), Describe(published));
    }

    [Fact]
    public void Canonical_state_machine_matches_bridge_for_missing_refresh_session_fault()
    {
        var auth = CreateAuthClient();
        var observer = new AuthStateObserver();
        var published = new List<AuthState>();
        using var subscription = observer.Subscribe(published.Add);
        using var bridge = new GotrueAuthStateBridge(auth, observer, NullLogger<GotrueAuthStateBridge>.Instance, metrics: null);
        var machine = new CanonicalAuthStateMachine();

        published.Clear();

        var expected = new List<AuthState>();

        ApplyStep(
            auth,
            machine,
            expected,
            GotrueAuthState.SignedIn,
            CreateSession("signed-in-token", "refresh-token", 1800));
        ApplyStep(
            auth,
            machine,
            expected,
            GotrueAuthState.TokenRefreshed,
            session: null);

        Assert.Equal(Describe(expected), Describe(published));
    }

    private static void ApplyStep(
        global::Supabase.Gotrue.Client auth,
        CanonicalAuthStateMachine machine,
        List<AuthState> expected,
        GotrueAuthState stateChanged,
        global::Supabase.Gotrue.Session? session)
    {
        if (stateChanged == GotrueAuthState.SignedOut)
        {
            if (machine.TrySignOut(out var signedOut))
            {
                expected.Add(signedOut);
            }

            SetCurrentSession(auth, session);
            auth.NotifyAuthStateChange(stateChanged);
            return;
        }

        if (stateChanged == GotrueAuthState.TokenRefreshed && machine.TryIgnoreStaleRefreshResultAfterSignOut())
        {
            SetCurrentSession(auth, session);
            auth.NotifyAuthStateChange(stateChanged);
            return;
        }

        if (!GotrueAuthStateBridge.TryCreateSessionSnapshot(session, out var snapshot))
        {
            if (stateChanged == GotrueAuthState.TokenRefreshed)
            {
                expected.Add(machine.Fault("Token refresh completed without a valid session."));
            }

            SetCurrentSession(auth, session);
            auth.NotifyAuthStateChange(stateChanged);
            return;
        }

        switch (stateChanged)
        {
            case GotrueAuthState.SignedIn:
                if (machine.TryAdvanceAuthenticated(snapshot, out var authenticated))
                {
                    expected.Add(authenticated);
                }

                break;
            case GotrueAuthState.TokenRefreshed:
                expected.Add(machine.BeginRefresh(snapshot));
                expected.Add(machine.CompleteRefresh(snapshot));
                break;
            default:
                throw new InvalidOperationException($"Unsupported test event '{stateChanged}'.");
        }

        SetCurrentSession(auth, session);
        auth.NotifyAuthStateChange(stateChanged);
    }

    private static string[] Describe(IEnumerable<AuthState> states)
    {
        var descriptions = new List<string>();

        foreach (var state in states)
        {
            descriptions.Add(state switch
            {
                AuthState.Authenticated authenticated => $"Authenticated:{authenticated.CanonicalVersion}:{authenticated.AccessToken}:{authenticated.RefreshToken}",
                AuthState.Refreshing refreshing => $"Refreshing:{refreshing.CanonicalVersion}:{refreshing.PendingRefreshVersion}:{refreshing.AccessToken}:{refreshing.RefreshToken}",
                AuthState.SignedOut signedOut => $"SignedOut:{signedOut.CanonicalVersion}",
                AuthState.Faulted faulted => $"Faulted:{faulted.CanonicalVersion}:{faulted.PendingRefreshVersion}:{faulted.Reason}",
                AuthState.Anonymous => "Anonymous",
                _ => throw new ArgumentOutOfRangeException(nameof(states), state, "Unknown auth state.")
            });
        }

        return descriptions.ToArray();
    }

    private static global::Supabase.Gotrue.Client CreateAuthClient()
    {
        return new global::Supabase.Gotrue.Client(new global::Supabase.Gotrue.ClientOptions
        {
            Url = "https://abc.supabase.co/auth/v1",
            Headers = new Dictionary<string, string>
            {
                ["apikey"] = "anon-key"
            }
        });
    }

    private static global::Supabase.Gotrue.Session CreateSession(string accessToken, string refreshToken, long expiresIn)
    {
        return new global::Supabase.Gotrue.Session
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = expiresIn,
            CreatedAt = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static void SetCurrentSession(global::Supabase.Gotrue.Client auth, global::Supabase.Gotrue.Session? session)
    {
        var property = typeof(global::Supabase.Gotrue.Client).GetProperty(
            nameof(global::Supabase.Gotrue.Client.CurrentSession),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(property);
        property!.SetValue(auth, session);
    }
}
