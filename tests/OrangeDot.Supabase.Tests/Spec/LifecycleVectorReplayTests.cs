using System;
using Xunit;

namespace OrangeDot.Supabase.Tests.Spec;

public sealed class LifecycleVectorReplayTests
{
    public static TheoryData<string> LifecycleVectorPaths =>
        SpecVectorTestSupport.CreateVectorPathTheoryData("lifecycle");

    [Theory]
    [MemberData(nameof(LifecycleVectorPaths))]
    public void Replay_matches_expected_state_from_vector(string vectorPath)
    {
        var vector = SpecVectorTestSupport.Deserialize<LifecycleVector>(vectorPath);
        var replay = LifecycleReplayState.Create(vector.InitialState, vector.Id);

        foreach (var @event in vector.Events)
        {
            replay.Apply(@event);
        }

        Assert.Equal(vector.Expected, replay.ToSnapshot());
    }

    private enum LifecyclePhase
    {
        Configured,
        LoadingSession,
        Initializing,
        Ready,
        Disposed
    }

    // The vector schema keeps "load complete" and "initialize complete" inside their in-flight phases
    // until the next explicit event advances to the next public phase.
    private sealed class LifecycleReplayState
    {
        private readonly string _vectorId;
        private LifecyclePhase _phase;
        private int _childCalls;
        private int _publicAttempts;
        private int _publicDenied;

        private LifecycleReplayState(
            LifecyclePhase phase,
            int childCalls,
            int publicAttempts,
            int publicDenied,
            string vectorId)
        {
            _phase = phase;
            _childCalls = childCalls;
            _publicAttempts = publicAttempts;
            _publicDenied = publicDenied;
            _vectorId = vectorId;
        }

        internal static LifecycleReplayState Create(LifecycleStateSnapshot initialState, string vectorId)
        {
            return new LifecycleReplayState(
                ParsePhase(initialState.Phase, vectorId),
                initialState.ChildCalls,
                initialState.PublicAttempts,
                initialState.PublicDenied,
                vectorId);
        }

        internal void Apply(LifecycleVectorEvent @event)
        {
            switch (@event.Type)
            {
                case "attempt_public_operation":
                    _publicAttempts++;

                    if (_phase == LifecyclePhase.Ready)
                    {
                        _childCalls++;
                    }
                    else
                    {
                        _publicDenied++;
                    }

                    break;
                case "load_persisted_session_start":
                    Require(_phase == LifecyclePhase.Configured, "load_persisted_session_start requires the configured phase.");
                    _phase = LifecyclePhase.LoadingSession;
                    break;
                case "load_persisted_session_complete":
                    Require(_phase == LifecyclePhase.LoadingSession, "load_persisted_session_complete requires the loading phase.");
                    break;
                case "initialize_start":
                    Require(_phase == LifecyclePhase.LoadingSession, "initialize_start requires the loading phase.");
                    _phase = LifecyclePhase.Initializing;
                    break;
                case "initialize_complete":
                    Require(_phase == LifecyclePhase.Initializing, "initialize_complete requires the initializing phase.");
                    break;
                case "signal_ready":
                    Require(_phase == LifecyclePhase.Initializing, "signal_ready requires the initializing phase.");
                    _phase = LifecyclePhase.Ready;
                    break;
                default:
                    throw new InvalidOperationException($"{_vectorId}: Unsupported lifecycle vector event '{@event.Type}'.");
            }
        }

        internal LifecycleStateSnapshot ToSnapshot()
        {
            return new LifecycleStateSnapshot
            {
                Phase = _phase.ToString(),
                ChildCalls = _childCalls,
                PublicAttempts = _publicAttempts,
                PublicDenied = _publicDenied
            };
        }

        private static LifecyclePhase ParsePhase(string phase, string vectorId)
        {
            return phase switch
            {
                nameof(LifecyclePhase.Configured) => LifecyclePhase.Configured,
                nameof(LifecyclePhase.LoadingSession) => LifecyclePhase.LoadingSession,
                nameof(LifecyclePhase.Initializing) => LifecyclePhase.Initializing,
                nameof(LifecyclePhase.Ready) => LifecyclePhase.Ready,
                nameof(LifecyclePhase.Disposed) => LifecyclePhase.Disposed,
                _ => throw new InvalidOperationException($"{vectorId}: Unsupported lifecycle phase '{phase}'.")
            };
        }

        private void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"{_vectorId}: {message}");
            }
        }
    }

    private sealed record LifecycleVector
    {
        public required string Id { get; init; }

        public required string Title { get; init; }

        public required LifecycleStateSnapshot InitialState { get; init; }

        public required LifecycleVectorEvent[] Events { get; init; }

        public required LifecycleStateSnapshot Expected { get; init; }

        public required string[] Invariants { get; init; }

        public string? Notes { get; init; }
    }

    private sealed record LifecycleVectorEvent
    {
        public required string Type { get; init; }
    }

    private sealed record LifecycleStateSnapshot
    {
        public required string Phase { get; init; }

        public required int ChildCalls { get; init; }

        public required int PublicAttempts { get; init; }

        public required int PublicDenied { get; init; }
    }
}
