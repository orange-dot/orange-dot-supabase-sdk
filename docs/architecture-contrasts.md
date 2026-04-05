# Architecture Contrasts

Date: 2026-04-05
Status: Design locked for v0.1
Scope: Orchestration layer only. Child modules (Gotrue, PostgREST, Realtime, Storage, Functions) are consumed untouched.

## Purpose of this document

This document records concrete architectural contrasts against the community orchestration layer. The point is **not** "this style is cleaner." The point is a set of specific decisions that:

1. Point to a real bug, class of bug, or ergonomics gap in the community orchestration layer.
2. Describe a chosen alternative that eliminates the problem **by construction**, not by testing.
3. Can be shown in 10-20 lines of code side-by-side.

This document holds those contrasts, ranked.

## How to read the contrasts

The primary summary is **three contrasts**. Three is enough to demonstrate breadth (lifecycle, events, telemetry are orthogonal axes) without becoming a thesis. Each contrast follows a rigid format:

```
Community pattern:   <what it does today, with file reference>
Concrete problem:    <bug, class of bug, or ergonomics gap it produces>
Chosen alternative:  <pattern used in this SDK>
Why it is better:    <property that eliminates the problem by construction>
```

"By construction" matters: the goal is to show that the bug **cannot exist** in the alternative, not that the alternative is tested against it.

---

## Contrast 1 — State lifecycle: boolean flags → typed states

**Community pattern.**
`Supabase/Client.cs` performs work in two places: the constructor (when URL+anon-key form is used) and `InitializeAsync()`. Whether `Auth.LoadSession()` has been called before `RetrieveSessionAsync()` is not visible in the type system — it depends on whether the caller followed the ordering documented in prose.

**Concrete problem.**
`InitializeAsync()` does not call `Auth.LoadSession()` before `RetrieveSessionAsync()`, despite documentation implying that it restores persisted sessions. Callers who follow the docs discover their persisted session is not restored. This is a **wrong-order bug** of a class the type system did not prevent.

**Chosen alternative.**
Model the lifecycle as types. Each phase returns a different type; each type exposes only the methods legal in that phase.

```csharp
// Community: one type, order matters, not enforced
var client = new Supabase.Client(url, key);
await client.InitializeAsync();       // must run first
await client.Auth.LoadSession();      // sometimes forgotten
// client.From<Todo>(...) is callable even if init was skipped

// Orange-dot: typed transitions
var configured = SupabaseClient
    .Configure(new SupabaseOptions { Url = url, AnonKey = key });

var hydrated = await configured
    .LoadPersistedSessionAsync();     // only exists on ConfiguredClient

var client = await hydrated
    .InitializeAsync(cancellationToken); // returns SupabaseClient

// client.Postgrest.From<Todo>() — available only on SupabaseClient
```

Internally this is a small handful of types (`ConfiguredClient`, `HydratedClient`, `SupabaseClient`) that carry forward the options and gradually accumulate state. Each transition consumes its predecessor.

**Why it is better.**
The wrong-order bug from the community version is a compile error in this shape. There is no test for it because it cannot be written.

**Cost.**
Slightly more verbose construction. Mitigated by a fluent builder for the common path.

---

## Contrast 2 — Auth state propagation: callback delegates → observable stream

**Community pattern.**
The top-level `Client` owns auth state. Each child client (`Postgrest`, `Realtime`, `Storage`, `Functions`) holds a `GetHeaders` delegate that the top-level client sets at construction. When auth state changes (sign-in, sign-out, token refresh), the top-level client handles the event and pushes the new token into each child's delegate by re-setting it. Realtime is pushed separately because it does not re-read headers per message.

**Concrete problem.**
This is an **imperative fan-out** pattern. The orchestrator must know about every child client that needs a token and must individually push updates into each. Adding a new child (or adding a new subsystem that needs the token — e.g., a metrics exporter, an outgoing webhook signer) means modifying the orchestrator. This violates open/closed at the architectural level.

Secondary problem: the pattern is hard to test. Mocking "the orchestrator pushes into these four delegates" requires threading all four child clients through the test fixture.

**Chosen alternative.**
Expose auth state through a lightweight homegrown observer. The orchestrator publishes auth state changes once. Anything that needs to react — child clients, custom subscribers, observability — subscribes. The orchestrator does not know who listens. New subscribers immediately receive `Current`, so late-starting hosted services do not miss the latest auth state.

```csharp
public interface IAuthStateObserver
{
    AuthState Current { get; }
    IDisposable Subscribe(Action<AuthState> listener);
}

public abstract record AuthState
{
    public sealed record Anonymous                          : AuthState;
    public sealed record Authenticated(string AccessToken,
                                       string RefreshToken,
                                       DateTimeOffset ExpiresAt) : AuthState;
    public sealed record SignedOut                          : AuthState;
}

// Child-binding subscribers live in their own classes, not in the orchestrator
internal sealed class RealtimeTokenBinding : IHostedService
{
    private readonly IAuthStateObserver _auth;
    private readonly IRealtimeClient _realtime;
    private IDisposable? _subscription;

    public RealtimeTokenBinding(IAuthStateObserver auth, IRealtimeClient realtime)
    {
        _auth = auth;
        _realtime = realtime;
    }

    public Task StartAsync(CancellationToken ct)
    {
        _subscription = _auth.Subscribe(state =>
        {
            if (state is AuthState.Authenticated authenticated)
                _realtime.SetAccessToken(authenticated.AccessToken);
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _subscription?.Dispose();
        _subscription = null;
        return Task.CompletedTask;
    }
}
```

**Why it is better.**
Adding a new auth-aware subsystem is a new `IHostedService` registration, **zero orchestrator changes**. Inverts control: children pull, they do not wait to be pushed into. Replay-on-subscribe means hosted bindings can start after the orchestrator and still receive the current token immediately. Testing a child's auth reaction requires nothing more than producing one event onto the observer.

**Cost.**
One small homegrown abstraction plus a few lines of thread-safe subscription code. No `System.Reactive` dependency.

---

## Contrast 3 — Observability: custom `Debugger` singleton → standard .NET telemetry

**Community pattern.**
A `Debugger.Instance` singleton sits at module boundaries with custom delegate handlers (`IPostgrestDebugger.DebugEventHandler`, etc.). Each module has its own. There is no `ILogger` integration, no `ActivitySource` for distributed tracing, and no metrics emission.

**Concrete problem.**
Production .NET deployments integrate with Application Insights, OpenTelemetry, Serilog, Seq, or vendor-specific APM. The standard expectation is that a library emits via `Microsoft.Extensions.Logging.ILogger<T>`, `System.Diagnostics.ActivitySource`, and `System.Diagnostics.Metrics.IMeterFactory`. A custom debugger singleton is **siloed** from all of that. The first question a customer asks in a production incident — *"why is this SDK slow, where is the latency coming from?"* — has no ready answer today.

**Chosen alternative.**
First-class .NET observability. `ILogger<T>` injected everywhere. A single static `ActivitySource` named `Supabase.Client` producing `Activity` spans for each request. Metrics come from a host-owned `IMeterFactory`, producing counters and histograms for operations, token refreshes, realtime reconnects, etc.

```csharp
public sealed class SupabaseClient : ISupabaseClient
{
    private readonly ILogger<SupabaseClient> _logger;
    private static readonly ActivitySource Activity = new("Supabase.Client", "0.1.0");
    private readonly Meter _meter;
    private readonly Counter<long> _requestCount;
    private readonly Histogram<double> _requestDuration;

    public SupabaseClient(ILogger<SupabaseClient> logger, IMeterFactory meterFactory)
    {
        _logger = logger;
        _meter = meterFactory.Create("Supabase.Client", "0.1.0");
        _requestCount = _meter.CreateCounter<long>("supabase.requests.total");
        _requestDuration = _meter.CreateHistogram<double>("supabase.requests.duration");
    }

    internal async Task<T> ExecuteAsync<T>(string module, Func<Task<T>> operation,
                                           CancellationToken ct)
    {
        using var activity = Activity.StartActivity("supabase.operation");
        activity?.SetTag("supabase.module", module);

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await operation();
            _requestCount.Add(1, new KeyValuePair<string, object?>("module", module),
                                 new KeyValuePair<string, object?>("outcome", "success"));
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _requestCount.Add(1, new KeyValuePair<string, object?>("module", module),
                                 new KeyValuePair<string, object?>("outcome", "error"));
            _logger.LogError(ex, "Supabase {Module} operation failed", module);
            throw;
        }
        finally
        {
            _requestDuration.Record(sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("module", module));
        }
    }
}
```

**Why it is better.**
Customers drop this SDK into their existing observability stack and it **just appears**, with traces, metrics, and structured logs. No custom integration code is needed. This is the line between *"it works"* and *"it is production-ready for enterprise .NET customers"* — and that line matters to Supabase's target audience for the C# SDK.

**Cost.**
Slightly more code inside operation wrappers. Dependencies on `Microsoft.Extensions.Logging.Abstractions` and `System.Diagnostics.DiagnosticSource` — both already on the dependency graph of any real .NET app.

---

## Secondary contrasts (shipped if time allows, not in the primary summary)

These are real contrasts with concrete backing, but were left out of the top-three to keep the summary focused.

### Contrast 4 — URL derivation: regex → UriBuilder

**Community pattern.** Regex matching on hosted Supabase URLs.
**Problem.** `StatelessClient.Functions()` uses `@"/(supabase\.co)|(supabase\.in)/"` with literal `/` delimiters, which does not match real hosted URLs. The stateful `Client` has a correct but **different** regex. Two copies of the same logic, drifted.
**Alternative.** Structured `UriBuilder` manipulation + table-driven tests.
**Why better.** No possibility of regex drift between two call sites. Bug from community version cannot be written in this shape. Every hosted/self-hosted variant is a `DataRow` in a unit test.
**Why secondary.** Too narrow to carry architectural signal on its own — strongest as supporting evidence in discussion.

### Contrast 5 — DI construction: two-constructor runtime dance → explicit entry point + DI extension

**Community pattern.** `Client` has two constructors: one that accepts pre-built child clients (DI scenario), one that accepts URL+key and constructs children internally.
**Problem.** Runtime branching. If a caller uses the DI constructor without supplying all children, behavior is silent or exceptional at unpredictable times.
**Alternative.** Single explicit lifecycle entry point `SupabaseClient.Configure(options)`, plus `IServiceCollection.AddSupabase(...)` for host integration. No direct constructor access.
**Why better.** Uniform construction semantics. DI-native by default.
**Why secondary.** Partially addressed by the community version today. Not a strong contrast to carry independently.

### Contrast 6 — Error taxonomy: per-module exception types → unified hierarchy

**Community pattern.** Each module throws its own exception type (`PostgrestException`, `GotrueException`, `StorageException`, etc.) with different shapes and no common ancestor.
**Problem.** No uniform catch surface. Cross-cutting error handling (retry, reauth on expiry, dead-letter) requires per-module knowledge.
**Alternative.** Orchestration-level `SupabaseException` base with discriminated subtypes and a shared error context (module name, operation, correlation id).
**Why better.** Enables uniform error policy at the orchestration boundary.
**Why secondary.** Touches the child-module error surface, which is out of scope for this study. Could leak scope.

---

## Condensed summary

This is the shortest complete summary of the repository's design direction.

**Slide 1 — Lifecycle**
> Community: `InitializeAsync()` can skip `Auth.LoadSession()`. Ordering is not enforced by types, causing a known persisted-session-lost bug.
> Orange-dot: typed state transitions. Skipping a step is a compile error.

**Slide 2 — Auth propagation**
> Community: orchestrator pushes tokens into each child via delegate callbacks. Adding a new auth-aware subsystem requires an orchestrator change.
> Orange-dot: auth state is a replaying `IAuthStateObserver`. Subscribers register themselves. Zero orchestrator change to add a new consumer.

**Slide 3 — Observability**
> Community: custom `Debugger` singleton with no framework integration.
> Orange-dot: `ILogger<T>`, `ActivitySource`, `IMeterFactory` — drops into any .NET observability stack as-is.

---

## Principles behind the selection

- Every primary contrast cites a **concrete, file-located community issue**. Aesthetic preference alone is not enough.
- Every primary contrast is **orthogonal** to the others. Lifecycle, events, and telemetry are three different axes; together they cover breadth.
- Every primary contrast is **implementable in the 5-7 day budget** for this SDK. Nothing is aspirational.
- No contrast requires modifying child modules. All are strictly at the orchestration boundary.
