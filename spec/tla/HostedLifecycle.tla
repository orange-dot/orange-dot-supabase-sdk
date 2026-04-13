---- MODULE HostedLifecycle ----
EXTENDS Naturals, TLC

\* Run with TLC before presenting this as a checked lifecycle artifact, e.g.:
\*   java -cp tla2tools.jar tlc2.TLC HostedLifecycle.tla -config HostedLifecycle.cfg

CONSTANT MaxPublicAttempts

Members == {"Auth", "Postgrest", "Realtime", "Storage", "Functions", "Url", "AnonKey", "Urls", "Table"}
StartupPhases == {"Idle", "PreparingClient", "PrePublishWindow", "ReadyPublished", "PublicationSkippedBecauseStopping", "Faulted", "Canceled"}
LifecyclePhases == {"Configured", "LoadingSession", "Initializing", "Ready", "Faulted", "Canceled"}

VARIABLES startupPhase, lifecyclePhase, stopRequested, publicAttempts, publicDenied, childCalls

vars == <<startupPhase, lifecyclePhase, stopRequested, publicAttempts, publicDenied, childCalls>>

Init ==
    /\ startupPhase = "Idle"
    /\ lifecyclePhase = "Configured"
    /\ stopRequested = FALSE
    /\ publicAttempts = 0
    /\ publicDenied = 0
    /\ childCalls = 0

StartRequested ==
    /\ startupPhase \in {"Idle", "PublicationSkippedBecauseStopping", "Faulted", "Canceled"}
    /\ startupPhase' = "PreparingClient"
    /\ lifecyclePhase' =
        IF lifecyclePhase = "Configured"
            THEN "LoadingSession"
            ELSE lifecyclePhase
    /\ stopRequested' = stopRequested
    /\ UNCHANGED <<publicAttempts, publicDenied, childCalls>>

PrePublishWindowEntered ==
    /\ startupPhase = "PreparingClient"
    /\ startupPhase' = "PrePublishWindow"
    /\ lifecyclePhase' =
        IF lifecyclePhase = "LoadingSession"
            THEN "Initializing"
            ELSE lifecyclePhase
    /\ stopRequested' = stopRequested
    /\ UNCHANGED <<publicAttempts, publicDenied, childCalls>>

StopRequested ==
    /\ startupPhase \in StartupPhases
    /\ startupPhase' = startupPhase
    /\ lifecyclePhase' =
        IF lifecyclePhase \in {"Configured", "LoadingSession", "Initializing"}
            THEN "Canceled"
            ELSE lifecyclePhase
    /\ stopRequested' = TRUE
    /\ UNCHANGED <<publicAttempts, publicDenied, childCalls>>

ReadyCompleted ==
    /\ startupPhase = "PrePublishWindow"
    /\ lifecyclePhase = "Initializing"
    /\ ~stopRequested
    /\ startupPhase' = "ReadyPublished"
    /\ lifecyclePhase' = "Ready"
    /\ stopRequested' = stopRequested
    /\ UNCHANGED <<publicAttempts, publicDenied, childCalls>>

StartFaulted ==
    /\ startupPhase \in {"PreparingClient", "PrePublishWindow"}
    /\ lifecyclePhase \in {"LoadingSession", "Initializing"}
    /\ startupPhase' = "Faulted"
    /\ lifecyclePhase' = "Faulted"
    /\ stopRequested' = stopRequested
    /\ UNCHANGED <<publicAttempts, publicDenied, childCalls>>

StartCanceled ==
    /\ startupPhase \in {"PreparingClient", "PrePublishWindow"}
    /\ lifecyclePhase \in {"LoadingSession", "Initializing"}
    /\ startupPhase' = "Canceled"
    /\ lifecyclePhase' = "Canceled"
    /\ stopRequested' = stopRequested
    /\ UNCHANGED <<publicAttempts, publicDenied, childCalls>>

ReadyPublicationSkippedBecauseStopping ==
    /\ startupPhase = "PrePublishWindow"
    /\ lifecyclePhase = "Canceled"
    /\ stopRequested
    /\ startupPhase' = "PublicationSkippedBecauseStopping"
    /\ lifecyclePhase' = lifecyclePhase
    /\ stopRequested' = stopRequested
    /\ UNCHANGED <<publicAttempts, publicDenied, childCalls>>

PublicAccessDenied(m) ==
    /\ m \in Members
    /\ lifecyclePhase # "Ready"
    /\ publicAttempts < MaxPublicAttempts
    /\ publicAttempts' = publicAttempts + 1
    /\ publicDenied' = publicDenied + 1
    /\ UNCHANGED <<startupPhase, lifecyclePhase, stopRequested, childCalls>>

PublicAccessAllowed(m) ==
    /\ m \in Members
    /\ lifecyclePhase = "Ready"
    /\ publicAttempts < MaxPublicAttempts
    /\ childCalls < MaxPublicAttempts
    /\ publicAttempts' = publicAttempts + 1
    /\ childCalls' = childCalls + 1
    /\ UNCHANGED <<startupPhase, lifecyclePhase, stopRequested, publicDenied>>

Next ==
    \/ StartRequested
    \/ PrePublishWindowEntered
    \/ StopRequested
    \/ ReadyCompleted
    \/ StartFaulted
    \/ StartCanceled
    \/ ReadyPublicationSkippedBecauseStopping
    \/ \E m \in Members: PublicAccessDenied(m)
    \/ \E m \in Members: PublicAccessAllowed(m)

TypeOK ==
    /\ startupPhase \in StartupPhases
    /\ lifecyclePhase \in LifecyclePhases
    /\ stopRequested \in BOOLEAN
    /\ publicAttempts \in 0..MaxPublicAttempts
    /\ publicDenied \in 0..MaxPublicAttempts
    /\ childCalls \in 0..MaxPublicAttempts

DeniedNeverExceedsAttempts ==
    publicDenied <= publicAttempts

AllowedNeverExceedsAttempts ==
    childCalls <= publicAttempts

AllowedCallsRequireReady ==
    childCalls > 0 => lifecyclePhase = "Ready"

ReadyImpliesPublished ==
    lifecyclePhase = "Ready" => startupPhase = "ReadyPublished"

PublicationSkipRequiresStopRequested ==
    startupPhase = "PublicationSkippedBecauseStopping" => stopRequested

PublicationSkipKeepsLifecycleCanceled ==
    startupPhase = "PublicationSkippedBecauseStopping" => lifecyclePhase = "Canceled"

CanceledOrFaultedNeverReady ==
    lifecyclePhase \in {"Canceled", "Faulted"} => startupPhase # "ReadyPublished"

Spec ==
    Init
    /\ [][Next]_vars

====
