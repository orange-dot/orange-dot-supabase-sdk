using System;
using System.Collections.Generic;
using OrangeDot.Supabase.Auth;
using Xunit;

namespace OrangeDot.Supabase.Tests.Spec;

public sealed class AuthVectorReplayTests
{
    public static TheoryData<string> AuthVectorPaths =>
        SpecVectorTestSupport.CreateVectorPathTheoryData("auth");

    [Theory]
    [MemberData(nameof(AuthVectorPaths))]
    public void Replay_matches_expected_state_from_vector(string vectorPath)
    {
        var vector = SpecVectorTestSupport.Deserialize<AuthVector>(vectorPath);
        var replay = AuthVectorReplayState.Create(vector.InitialState, vector.Id);

        foreach (var @event in vector.Events)
        {
            replay.Apply(@event);
        }

        Assert.Equal(vector.Expected, replay.ToSnapshot());
    }

    private enum BindingKind
    {
        Postgrest,
        Realtime,
        Storage,
        Functions
    }

    // The vector schema models canonical auth transitions separately from async binding convergence.
    // `project_binding` advances one live binding to the current canonical version.
    private sealed class AuthVectorReplayState
    {
        private const string PlaceholderAccessToken = "access-token";
        private const string PlaceholderRefreshToken = "refresh-token";

        private readonly string _vectorId;
        private readonly Dictionary<BindingKind, BindingProjection> _bindings;
        private AuthState _state;
        private long _pendingRefreshVersion;

        private AuthVectorReplayState(
            AuthState state,
            long pendingRefreshVersion,
            Dictionary<BindingKind, BindingProjection> bindings,
            string vectorId)
        {
            _state = state;
            _pendingRefreshVersion = pendingRefreshVersion;
            _bindings = bindings;
            _vectorId = vectorId;
        }

        internal static AuthVectorReplayState Create(AuthVectorStateSnapshot initialState, string vectorId)
        {
            return new AuthVectorReplayState(
                CreateAuthState(initialState),
                initialState.PendingRefreshVersion,
                new Dictionary<BindingKind, BindingProjection>
                {
                    [BindingKind.Postgrest] = new(initialState.LiveBindings.Postgrest, initialState.ProjectedVersions.Postgrest),
                    [BindingKind.Realtime] = new(initialState.LiveBindings.Realtime, initialState.ProjectedVersions.Realtime),
                    [BindingKind.Storage] = new(initialState.LiveBindings.Storage, initialState.ProjectedVersions.Storage),
                    [BindingKind.Functions] = new(initialState.LiveBindings.Functions, initialState.ProjectedVersions.Functions)
                },
                vectorId);
        }

        internal void Apply(AuthVectorEvent @event)
        {
            switch (@event.Type)
            {
                case "start_binding":
                    {
                        var binding = ParseBinding(@event.Binding);
                        var projection = _bindings[binding];

                        _bindings[binding] = projection with
                        {
                            IsLive = true,
                            ProjectedVersion = GetProjectedVersionForState(_state)
                        };
                        break;
                    }
                case "begin_refresh":
                    Require(
                        _state is AuthState.Authenticated,
                        "begin_refresh requires an authenticated canonical state.");

                    _pendingRefreshVersion = _state.CanonicalVersion + 1;
                    _state = new AuthState.Refreshing(
                        _state.CanonicalVersion,
                        _pendingRefreshVersion,
                        PlaceholderAccessToken,
                        PlaceholderRefreshToken,
                        DateTimeOffset.UnixEpoch);
                    break;
                case "complete_refresh":
                    Require(_pendingRefreshVersion > 0, "complete_refresh requires a pending refresh version.");

                    var canonicalVersion = _pendingRefreshVersion > _state.CanonicalVersion
                        ? _pendingRefreshVersion
                        : _state.CanonicalVersion + 1;

                    _pendingRefreshVersion = 0;
                    _state = new AuthState.Authenticated(
                        canonicalVersion,
                        PlaceholderAccessToken,
                        PlaceholderRefreshToken,
                        DateTimeOffset.UnixEpoch);
                    break;
                case "project_binding":
                    {
                        var binding = ParseBinding(@event.Binding);
                        var projection = _bindings[binding];

                        Require(projection.IsLive, $"project_binding requires binding '{binding}' to be live.");

                        _bindings[binding] = projection with
                        {
                            ProjectedVersion = GetProjectedVersionForState(_state)
                        };
                        break;
                    }
                case "sign_out":
                    _pendingRefreshVersion = 0;
                    _state = new AuthState.SignedOut(_state.CanonicalVersion);

                    foreach (var binding in Enum.GetValues<BindingKind>())
                    {
                        _bindings[binding] = _bindings[binding] with { ProjectedVersion = 0 };
                    }

                    break;
                case "ignore_stale_refresh_result":
                    Require(
                        _pendingRefreshVersion > 0 || _state is AuthState.SignedOut,
                        "ignore_stale_refresh_result requires a pending refresh version or a signed-out canonical state.");
                    _pendingRefreshVersion = 0;
                    break;
                default:
                    throw new InvalidOperationException($"{_vectorId}: Unsupported auth vector event '{@event.Type}'.");
            }

            AssertProjectedVersionsBounded();
        }

        internal AuthVectorStateSnapshot ToSnapshot()
        {
            return new AuthVectorStateSnapshot
            {
                AuthState = ToStateName(_state),
                CanonicalVersion = _state.CanonicalVersion,
                PendingRefreshVersion = _pendingRefreshVersion,
                LiveBindings = new BindingLiveSnapshot
                {
                    Postgrest = _bindings[BindingKind.Postgrest].IsLive,
                    Realtime = _bindings[BindingKind.Realtime].IsLive,
                    Storage = _bindings[BindingKind.Storage].IsLive,
                    Functions = _bindings[BindingKind.Functions].IsLive
                },
                ProjectedVersions = new BindingVersionSnapshot
                {
                    Postgrest = _bindings[BindingKind.Postgrest].ProjectedVersion,
                    Realtime = _bindings[BindingKind.Realtime].ProjectedVersion,
                    Storage = _bindings[BindingKind.Storage].ProjectedVersion,
                    Functions = _bindings[BindingKind.Functions].ProjectedVersion
                }
            };
        }

        private static AuthState CreateAuthState(AuthVectorStateSnapshot snapshot)
        {
            return snapshot.AuthState switch
            {
                "Authenticated" => new AuthState.Authenticated(
                    snapshot.CanonicalVersion,
                    PlaceholderAccessToken,
                    PlaceholderRefreshToken,
                    DateTimeOffset.UnixEpoch),
                "SignedOut" => new AuthState.SignedOut(snapshot.CanonicalVersion),
                _ => throw new InvalidOperationException($"Unsupported auth state '{snapshot.AuthState}'.")
            };
        }

        private static long GetProjectedVersionForState(AuthState state)
        {
            return state switch
            {
                AuthState.Authenticated => state.CanonicalVersion,
                AuthState.Refreshing => state.CanonicalVersion,
                AuthState.SignedOut => 0,
                AuthState.Anonymous => 0,
                AuthState.Faulted => 0,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown auth state.")
            };
        }

        private static string ToStateName(AuthState state)
        {
            return state switch
            {
                AuthState.Authenticated => "Authenticated",
                AuthState.Refreshing => "Authenticated",
                AuthState.SignedOut => "SignedOut",
                AuthState.Anonymous => "Anonymous",
                AuthState.Faulted => "Faulted",
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown auth state.")
            };
        }

        private BindingKind ParseBinding(string? bindingName)
        {
            Require(!string.IsNullOrWhiteSpace(bindingName), "Binding name is required for this auth vector event.");

            return bindingName switch
            {
                "Postgrest" => BindingKind.Postgrest,
                "Realtime" => BindingKind.Realtime,
                "Storage" => BindingKind.Storage,
                "Functions" => BindingKind.Functions,
                _ => throw new InvalidOperationException($"{_vectorId}: Unsupported binding '{bindingName}'.")
            };
        }

        private void AssertProjectedVersionsBounded()
        {
            foreach (var (binding, projection) in _bindings)
            {
                Require(
                    projection.ProjectedVersion <= _state.CanonicalVersion,
                    $"Binding '{binding}' projected version {projection.ProjectedVersion} exceeded canonical version {_state.CanonicalVersion}.");
            }
        }

        private void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"{_vectorId}: {message}");
            }
        }

        private readonly record struct BindingProjection(bool IsLive, long ProjectedVersion);
    }

    private sealed record AuthVector
    {
        public required string Id { get; init; }

        public required string Title { get; init; }

        public required AuthVectorStateSnapshot InitialState { get; init; }

        public required AuthVectorEvent[] Events { get; init; }

        public required AuthVectorStateSnapshot Expected { get; init; }

        public required string[] Invariants { get; init; }

        public string? Notes { get; init; }
    }

    private sealed record AuthVectorEvent
    {
        public required string Type { get; init; }

        public string? Binding { get; init; }
    }

    private sealed record AuthVectorStateSnapshot
    {
        public required string AuthState { get; init; }

        public required long CanonicalVersion { get; init; }

        public required long PendingRefreshVersion { get; init; }

        public required BindingLiveSnapshot LiveBindings { get; init; }

        public required BindingVersionSnapshot ProjectedVersions { get; init; }
    }

    private sealed record BindingLiveSnapshot
    {
        public required bool Postgrest { get; init; }

        public required bool Realtime { get; init; }

        public required bool Storage { get; init; }

        public required bool Functions { get; init; }
    }

    private sealed record BindingVersionSnapshot
    {
        public required long Postgrest { get; init; }

        public required long Realtime { get; init; }

        public required long Storage { get; init; }

        public required long Functions { get; init; }
    }
}
