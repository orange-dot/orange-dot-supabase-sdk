using System;
using OrangeDot.Supabase.Internal;
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

    private sealed class LifecycleReplayState
    {
        private readonly string _vectorId;
        private readonly LifecycleStateMachine _machine;

        private LifecycleReplayState(LifecycleStateMachine machine, string vectorId)
        {
            _machine = machine;
            _vectorId = vectorId;
        }

        internal static LifecycleReplayState Create(LifecycleStateSnapshot initialState, string vectorId)
        {
            return new LifecycleReplayState(
                new LifecycleStateMachine(
                    ParsePhase(initialState.Phase, vectorId),
                    initialState.ChildCalls,
                    initialState.PublicAttempts,
                    initialState.PublicDenied),
                vectorId);
        }

        internal void Apply(LifecycleVectorEvent @event)
        {
            switch (@event.Type)
            {
                case "attempt_public_operation":
                    _machine.AttemptPublicOperation();
                    break;
                case "load_persisted_session_start":
                    _machine.LoadPersistedSessionStart();
                    break;
                case "load_persisted_session_complete":
                    _machine.LoadPersistedSessionComplete();
                    break;
                case "initialize_start":
                    _machine.InitializeStart();
                    break;
                case "initialize_complete":
                    _machine.InitializeComplete();
                    break;
                case "signal_ready":
                    _machine.SignalReady();
                    break;
                default:
                    throw new InvalidOperationException($"{_vectorId}: Unsupported lifecycle vector event '{@event.Type}'.");
            }
        }

        internal LifecycleStateSnapshot ToSnapshot()
        {
            var snapshot = _machine.CaptureSnapshot();

            return new LifecycleStateSnapshot
            {
                Phase = snapshot.Phase.ToString(),
                ChildCalls = snapshot.ChildCalls,
                PublicAttempts = snapshot.PublicAttempts,
                PublicDenied = snapshot.PublicDenied
            };
        }

        private static LifecyclePhase ParsePhase(string phase, string vectorId)
        {
            return phase switch
            {
                nameof(LifecyclePhase.Configured) => LifecyclePhase.Configured,
                nameof(LifecyclePhase.LoadingSession) => LifecyclePhase.LoadingSession,
                nameof(LifecyclePhase.Initializing) => LifecyclePhase.Initializing,
                nameof(LifecyclePhase.Faulted) => LifecyclePhase.Faulted,
                nameof(LifecyclePhase.Canceled) => LifecyclePhase.Canceled,
                nameof(LifecyclePhase.Ready) => LifecyclePhase.Ready,
                nameof(LifecyclePhase.Disposed) => LifecyclePhase.Disposed,
                _ => throw new InvalidOperationException($"{vectorId}: Unsupported lifecycle phase '{phase}'.")
            };
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
