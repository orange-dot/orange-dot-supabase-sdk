---- MODULE AuthPropagation ----
EXTENDS Naturals, TLC

\* Run with TLC before presenting this as a verification artifact, e.g.:
\*   java -cp tla2tools.jar tlc2.TLC AuthPropagation.tla -config AuthPropagation.cfg

CONSTANT MaxVersion

Bindings == {"Postgrest", "Realtime", "Storage", "Functions"}
AuthStates == {"Anonymous", "Authenticated", "Refreshing", "SignedOut", "Faulted"}

VARIABLES authState, canonicalVersion, pendingRefreshVersion, live, projectedVersion

CurrentProjectionValue ==
    IF authState \in {"Authenticated", "Refreshing"} THEN canonicalVersion ELSE 0

vars == <<authState, canonicalVersion, pendingRefreshVersion, live, projectedVersion>>

Init ==
    /\ authState = "Anonymous"
    /\ canonicalVersion = 0
    /\ pendingRefreshVersion = 0
    /\ live = [b \in Bindings |-> FALSE]
    /\ projectedVersion = [b \in Bindings |-> 0]

StartBinding(b) ==
    /\ b \in Bindings
    /\ ~live[b]
    /\ live' = [live EXCEPT ![b] = TRUE]
    /\ projectedVersion' =
        [projectedVersion EXCEPT ![b] = CurrentProjectionValue]
    /\ UNCHANGED <<authState, canonicalVersion, pendingRefreshVersion>>

StopBinding(b) ==
    /\ b \in Bindings
    /\ live[b]
    /\ live' = [live EXCEPT ![b] = FALSE]
    /\ UNCHANGED <<authState, canonicalVersion, pendingRefreshVersion, projectedVersion>>

SignIn ==
    /\ authState \in {"Anonymous", "SignedOut", "Faulted"}
    /\ canonicalVersion < MaxVersion
    /\ authState' = "Authenticated"
    /\ canonicalVersion' = canonicalVersion + 1
    /\ pendingRefreshVersion' = 0
    /\ UNCHANGED <<live, projectedVersion>>

BeginRefresh ==
    /\ authState = "Authenticated"
    /\ canonicalVersion < MaxVersion
    /\ authState' = "Refreshing"
    /\ canonicalVersion' = canonicalVersion
    /\ pendingRefreshVersion' = canonicalVersion + 1
    /\ UNCHANGED <<live, projectedVersion>>

CompleteRefresh ==
    /\ authState = "Refreshing"
    /\ pendingRefreshVersion > canonicalVersion
    /\ authState' = "Authenticated"
    /\ canonicalVersion' = pendingRefreshVersion
    /\ pendingRefreshVersion' = 0
    /\ UNCHANGED <<live, projectedVersion>>

RefreshFail ==
    /\ authState \in {"Authenticated", "Refreshing"}
    /\ IF authState = "Refreshing"
          THEN pendingRefreshVersion > canonicalVersion
          ELSE pendingRefreshVersion = 0
    /\ authState' = "Faulted"
    /\ canonicalVersion' = canonicalVersion
    /\ pendingRefreshVersion' = 0
    /\ UNCHANGED <<live, projectedVersion>>

IgnoreStaleRefreshResult ==
    /\ authState = "SignedOut"
    /\ authState' = "SignedOut"
    /\ canonicalVersion' = canonicalVersion
    /\ pendingRefreshVersion' = pendingRefreshVersion
    /\ UNCHANGED <<live, projectedVersion>>

ProjectCurrentToBinding(b) ==
    /\ b \in Bindings
    /\ live[b]
    /\ authState \in {"Authenticated", "Refreshing"}
    /\ projectedVersion[b] # canonicalVersion
    /\ projectedVersion' = [projectedVersion EXCEPT ![b] = canonicalVersion]
    /\ UNCHANGED <<authState, canonicalVersion, pendingRefreshVersion, live>>

ClearBindingProjection(b) ==
    /\ b \in Bindings
    /\ live[b]
    /\ authState \in {"Anonymous", "SignedOut", "Faulted"}
    /\ projectedVersion[b] # 0
    /\ projectedVersion' = [projectedVersion EXCEPT ![b] = 0]
    /\ UNCHANGED <<authState, canonicalVersion, pendingRefreshVersion, live>>

SignOut ==
    /\ authState \in {"Anonymous", "Authenticated", "Refreshing", "Faulted"}
    /\ authState' = "SignedOut"
    /\ canonicalVersion' = canonicalVersion
    /\ pendingRefreshVersion' = 0
    /\ live' = live
    /\ projectedVersion' = [b \in Bindings |-> 0]

Next ==
    \/ \E b \in Bindings: StartBinding(b)
    \/ \E b \in Bindings: StopBinding(b)
    \/ SignIn
    \/ BeginRefresh
    \/ CompleteRefresh
    \/ RefreshFail
    \/ IgnoreStaleRefreshResult
    \/ \E b \in Bindings: ProjectCurrentToBinding(b)
    \/ \E b \in Bindings: ClearBindingProjection(b)
    \/ SignOut

TypeOK ==
    /\ authState \in AuthStates
    /\ canonicalVersion \in 0..MaxVersion
    /\ pendingRefreshVersion \in 0..MaxVersion
    /\ live \in [Bindings -> BOOLEAN]
    /\ projectedVersion \in [Bindings -> 0..MaxVersion]

SignedOutClearsBindings ==
    authState = "SignedOut" => \A b \in Bindings: projectedVersion[b] = 0

ProjectedVersionNeverLeads ==
    \A b \in Bindings: projectedVersion[b] <= canonicalVersion

RefreshingUsesFutureVersion ==
    authState = "Refreshing" => pendingRefreshVersion > canonicalVersion

SignedOutHasNoPendingRefresh ==
    authState = "SignedOut" => pendingRefreshVersion = 0

AuthenticatedBindingsSettleOrAuthChanges ==
    [](\A b \in Bindings:
        (authState = "Authenticated" /\ live[b] /\ projectedVersion[b] # canonicalVersion)
            => <>(projectedVersion[b] = canonicalVersion
                  \/ ~live[b]
                  \/ authState # "Authenticated"))

Spec ==
    Init
    /\ [][Next]_vars
    /\ WF_vars(SignIn)
    /\ WF_vars(BeginRefresh)
    /\ WF_vars(CompleteRefresh)
    /\ \A b1 \in Bindings: WF_vars(StartBinding(b1))
    /\ \A b2 \in Bindings: WF_vars(ProjectCurrentToBinding(b2))
    /\ \A b3 \in Bindings: WF_vars(ClearBindingProjection(b3))

====
