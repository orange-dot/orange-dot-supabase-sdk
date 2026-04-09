# Implementation Plan

Date: 2026-04-05
Duration: 7 days (6 working days + 1 buffer)
Budget: ~5-6 hours focused work per day
Delivery target: polished v0.1 design package and prototype baseline

## Guiding principles

- **Ship at the end of every day.** Each day ends with a green build, passing tests, and at least one externally visible change pushed to the repo.
- **Vertical slices beat horizontal layers.** Prefer "URL derivation module done with tests" over "all interfaces stubbed everywhere."
- **Observability is woven in, not bolted on.** Every component ships with `ILogger<T>` from day one. `ActivitySource` and `IMeterFactory`-backed metrics grow alongside features.
- **The three architectural contrasts are the acceptance criteria.** If any of them is not demonstrably working in the code by end of Day 5, that contrast is at risk and must be rescued before polish begins.
- **Submodules stay untouched.** No local patches to any child module. If a bug blocks progress, it becomes an upstream issue and the orchestration layer works around it.
- **Push to GitHub from Day 1.** Commit cadence is part of the project record.

## Prerequisites (before Day 1)

- [ ] .NET 10 SDK installed and `dotnet --version` verifies
- [ ] Supabase CLI installed (`supabase --version`)
- [ ] GitHub repo created under `orange-dot/<name>`
- [ ] Local `supabase start` reachable at `localhost:54321` for integration tests
- [ ] Community C# SDK checkout available for comparison

## Day 1 — Skeleton + URL derivation

**Morning: repo skeleton**
- `git init`, remote set to `orange-dot/<name>`
- `.gitmodules` added, submodules under `modules/` pinned to the comparison baseline commits
- Solution layout: `src/OrangeDot.Supabase/`, `tests/OrangeDot.Supabase.Tests/`, `samples/Console/`
- `Directory.Build.props` with `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<LangVersion>latest</LangVersion>`
- SDK-style project files use `<TargetFrameworks>net8.0;net10.0</TargetFrameworks>`
- `global.json` pinning .NET 10 SDK
- `.editorconfig` aligned with the existing project conventions
- `.gitignore` for .NET
- GitHub Actions workflow: `build-and-test.yml` (restore, build, test on push to `main` and PRs)

**Afternoon: URL derivation module (Contrast 4)**
- `src/OrangeDot.Supabase/Urls/SupabaseUrls.cs` with pure functions for deriving Auth/REST/Realtime/Storage/Functions URLs from a base Supabase URL
- Uses `UriBuilder`, no regex
- Inject `ILogger<SupabaseUrls>` where derivation happens at runtime
- `tests/OrangeDot.Supabase.Tests/UrlDerivationTests.cs` with `[DataRow]` table-driven tests:
  - `https://abc.supabase.co` variants
  - `https://abc.supabase.in` variants
  - `http://localhost:54321` self-hosted
  - Trailing slashes, port variations, edge cases
- Tests target a minimum of 10 distinct URL input cases

**End of Day 1 deliverable:**
- Repo on GitHub with first commit
- CI green on push
- One module (`SupabaseUrls`) fully implemented, tested, logging-instrumented
- Tag `v0.0.1` to mark first ship

**Exit criteria:** CI is green. Repo is pushed. URL derivation is done and tested. If not all three, Day 1 is not over.

## Day 2 — Auth state observable + error taxonomy

**Morning: auth state types + observable**
- `src/OrangeDot.Supabase/Auth/AuthState.cs` — discriminated union: `AuthState.Anonymous`, `AuthState.Authenticated(version, AccessToken, RefreshToken, ExpiresAt)`, `AuthState.Refreshing(version, pendingRefreshVersion, ...)`, `AuthState.SignedOut(version)`, `AuthState.Faulted(version, pendingRefreshVersion, reason)`
- `src/OrangeDot.Supabase/Auth/IAuthStateObserver.cs` — interface exposing `AuthState Current` plus `IDisposable Subscribe(Action<AuthState> listener)`
- `src/OrangeDot.Supabase/Auth/AuthStateObserver.cs` — homegrown implementation (no `System.Reactive` dependency)
  - Thread-safe subscribe/unsubscribe
  - Current value always readable
  - `Subscribe(...)` immediately replays `Current` to late subscribers
  - Unsubscribe via `IDisposable` returned from `Subscribe(...)`

**Afternoon: error taxonomy**
- `src/OrangeDot.Supabase/Errors/SupabaseException.cs` — base exception
- Discriminated subtypes: `SupabaseConfigurationException`, `SupabaseAuthException`, `SupabaseRequestException` (with module origin tag)
- Each carries: module name, operation name, correlation id, optional underlying cause
- `SupabaseErrorCode` enum for machine-readable codes

**Tests**
- `AuthStateObserverTests.cs` — subscribe, publish, unsubscribe, multiple concurrent subscribers, late subscribers get current value immediately, dispose is idempotent
- `SupabaseExceptionTests.cs` — exception hierarchy, correlation id round-trip, tag serialization

**End of Day 2 deliverable:**
- Auth state observable fully working, tested for concurrency
- Error hierarchy ready to wrap child-module exceptions at orchestration boundaries
- Both modules have `ILogger<T>` injected and log at key transitions

**Exit criteria:** Observable passes stress test with 100 subscribers + 1000 publish events. Error types round-trip correlation ids.

## Day 3 — Typed lifecycle states + DI extensions

**Morning: lifecycle types (Contrast 1)**
- `src/OrangeDot.Supabase/SupabaseOptions.cs` — standard options class (`{ get; set; }`), configured during startup and treated as immutable after boot
- `src/OrangeDot.Supabase/Lifecycle/ConfiguredClient.cs` — entry state, only `LoadPersistedSessionAsync()` available
- `src/OrangeDot.Supabase/Lifecycle/HydratedClient.cs` — session attempted to load, only `InitializeAsync(ct)` available
- `src/OrangeDot.Supabase/SupabaseClient.cs` — fully booted, exposes child module accessors
- Static entry point: `SupabaseClient.Configure(SupabaseOptions)` returning `ConfiguredClient`
- Each transition consumes its predecessor (no post-transition use of the earlier type)

**Afternoon: DI extensions (Contrast 5, supporting)**
- `src/OrangeDot.Supabase/Extensions/ServiceCollectionExtensions.cs`
- `services.AddSupabaseHosted(opts => { opts.Url = ...; opts.AnonKey = ...; })` registers:
  - `SupabaseOptions` as `IOptions<SupabaseOptions>`
  - `IAuthStateObserver` as singleton
  - `ISupabaseClient` as a readiness-gated singleton shell
  - An internal hosted startup driver that performs `Configure(options) -> LoadPersistedSessionAsync() -> InitializeAsync(ct)` during host start
  - All child module clients and auth-aware bindings as singletons
- Console/manual path stays explicit: `SupabaseClient.Configure(options) -> LoadPersistedSessionAsync() -> InitializeAsync(ct)`
- The DI-resolved client exposes `Task Ready { get; }`; public operations await readiness before first use
- `services.AddSupabaseHosted(...)` registers the hosted startup driver explicitly, not implicitly through constructor side effects

**Tests**
- `LifecycleTransitionTests.cs` — happy path, configuration validation failures, session-load failures, cancellation propagation
- `ServiceRegistrationTests.cs` — `services.AddSupabaseHosted(...)` resolves all expected types, hosted services, and scoping
- `ReadinessGateTests.cs` — DI path blocks public operations until hosted bootstrap completes and `Ready` is signaled
- `LifecycleCompileProofTests.cs` — Roslyn-based negative compilation test proving invalid lifecycle skips do not compile

**End of Day 3 deliverable:**
- Full construction paths work:
  - Console/manual: `SupabaseClient.Configure(options) -> LoadPersistedSessionAsync() -> InitializeAsync(ct)`
  - DI/hosted: `services.AddSupabaseHosted(...)` -> host start -> resolve `ISupabaseClient` -> await `Ready` -> use it
- Lifecycle types enforce correct ordering at compile time
- Roslyn negative-compilation coverage proves that trying to skip a lifecycle step fails at compile time

**Exit criteria:** An ASP.NET minimal API can register `ISupabaseClient`, start the host, and observe `Ready` completion. A console app can walk the typed lifecycle manually. Both paths green.

## Day 4 — Child-client bindings + observability wiring

**Morning: child-client auth subscribers (Contrast 2)**
- `src/OrangeDot.Supabase/Internal/RealtimeTokenBinding.cs` — owned subscriber that reacts to `IAuthStateObserver` updates and pushes token/channel resets into the realtime client
- `src/OrangeDot.Supabase/Internal/PostgrestHeaderBinding.cs` — subscribes, updates Postgrest client's `GetHeaders` delegate
- `src/OrangeDot.Supabase/Internal/StorageHeaderBinding.cs` — same pattern for Storage
- `src/OrangeDot.Supabase/Internal/FunctionsHeaderBinding.cs` — same pattern for Functions
- Each binding is a self-contained class. Orchestrator has no references to them.

**Afternoon: observability deep wiring (Contrast 3)**
- `src/OrangeDot.Supabase/Observability/SupabaseTelemetry.cs` — static `ActivitySource` named `"Supabase.Client"` with version tag
- `src/OrangeDot.Supabase/Observability/SupabaseMetrics.cs` — counters and histograms created from `IMeterFactory.Create("Supabase.Client", "0.1.0")`:
  - `supabase.requests.total` (counter, tagged by `module` and `outcome`)
  - `supabase.requests.duration` (histogram, tagged by `module`)
  - `supabase.auth.token_refresh.total` (counter, tagged by `outcome`)
- Activities emitted around each orchestration-level operation
- Log statements at `LogLevel.Information` on auth transitions, at `LogLevel.Debug` on every child-binding publish

**Tests**
- `ChildBindingTests.cs` — publish an `AuthState.Authenticated`, assert each child received the token (using mock child clients)
- `TelemetryTests.cs` — use `ActivityListener` to capture emitted activities, assert tags and durations
- `MeterTests.cs` — use `IMeterFactory` + `MeterListener` to capture metric values, assert counter increments

**End of Day 4 deliverable:**
- Auth state propagation fully working via replaying auth observer + bindings, zero manual orchestrator push
- Full observability stack emitting activities, metrics, and structured logs
- All three primary contrasts are now demonstrable in code

**Exit criteria:** `MeterListener` captures at least one counter and one histogram value during a test. `ActivityListener` captures an activity with correct tags.

## Day 5 — Supabase table wrapper + stateless client + first sample

**Morning: supabase table wrapper**
- `src/OrangeDot.Supabase/SupabaseTable.cs` — wraps `Postgrest.Table<T>`
- Adds realtime `.On(...)` subscription method tied to the orchestrator's `RealtimeClient`
- Delegates all other methods to the wrapped Postgrest table

**Afternoon: stateless client + console sample**
- `src/OrangeDot.Supabase.Stateless/SupabaseStateless.cs` — static convenience methods for one-shot operations without DI
- `samples/Console/Program.cs` — end-to-end demo:
  - Configure with local Supabase URL + anon key
  - Sign in anonymously or with email
  - Query a table
  - Subscribe to a realtime channel, print changes
  - Sign out
- `samples/Console/README.md` with run instructions

**Tests**
- `SupabaseTableTests.cs` — verifies table wrapper delegates correctly and realtime subscription fires
- `StatelessTests.cs` — verifies one-shot ops work without DI

**End of Day 5 deliverable:**
- Usable SDK end-to-end
- Runnable console sample that talks to local Supabase
- All three contrasts visible to anyone running the sample (structured logs, activities in console, typed lifecycle enforced)

**Exit criteria:** Console sample runs against `supabase start` local stack and prints realtime events.

## Day 6 — Integration tests + AspNetCore sample + README polish

**Morning: integration tests**
- `tests/OrangeDot.Supabase.IntegrationTests/` project (skipped when `supabase start` not available)
- `SignInFlowTests.cs` — email sign-in, session load, sign-out, token refresh
- `PostgrestCrudTests.cs` — insert, select, update, delete roundtrip
- `RealtimeSubscriptionTests.cs` — insert a row, receive a `postgres_changes` event through subscription
- Tests use a dedicated test schema, cleaned between runs

**Afternoon: AspNetCore sample + README**
- `samples/AspNetCore/` — minimal web API with `services.AddSupabaseHosted(...)`, one endpoint that queries a table, middleware that attaches auth
- `README.md` polish:
  - Short project overview (3 paragraphs)
  - The three contrasts with code snippets
  - Quick start (console + DI)
  - Link to `docs/architecture-contrasts.md`, `docs/decision-log.md`
  - Badge row (CI status, .NET version)
- Root `README.md` of the repo should be readable in under 3 minutes and leave the reader knowing: what this is, the three contrasts, how to run the sample

**End of Day 6 deliverable:**
- Full SDK with unit + integration tests
- Two runnable samples
- Polished README that can be shared as the public project overview

**Exit criteria:** README passes the "cold reader" test — someone who knows nothing about the project understands the three contrasts in one read.

## Day 7 — Buffer / polish / release prep

**Use this day for:**
- Anything that slipped
- Extra test coverage on weak spots
- README refinements after cold-reader feedback
- `architecture-contrasts.md` cross-links from README
- Final public-doc scrub for scope, tone, and stale internal references
- Final commit + version tag `v0.1.0`

**Do NOT use this day for:**
- Adding new features
- Expanding scope
- Refactoring working code for aesthetics

## Slippage plan / cut list

If behind schedule, cut in this order:

1. **First to cut:** AspNetCore sample (Day 6 afternoon). Keep console sample only.
2. **Second to cut:** Integration tests (Day 6 morning). Unit tests + table-driven tests cover most of the signal.
3. **Third to cut:** `MeterListener` and `ActivityListener` verification tests (Day 4). Observability still ships, just less proof of it.
4. **Fourth to cut:** Stateless client (Day 5 afternoon). DI-only path is fine.
5. **Last resort:** Reduce error taxonomy to a single `SupabaseException` type.

**Do not cut:** the three primary contrasts (lifecycle, auth observable, observability). If any of those is at risk by Day 4, stop feature work and rescue them.

## Traps to avoid

- Implementing retry / circuit-breaking. Polly territory, out of scope.
- Adding response caching. Not an orchestration concern.
- Fixing bugs found in child modules. Those go to upstream issues + separate PRs.
- Supporting targets outside `net8.0` and `net10.0`.
- Trying to close `supabase-js` feature-parity gaps inside this repo. That is a separate audit.
- Benchmarks. Nice-to-have at best. Skip unless Day 7 has nothing else.
- Swapping `Newtonsoft.Json` for `System.Text.Json` inside child modules. The orchestration layer can prefer STJ but does not force child modules to change.

## Daily rhythm

- **Morning (2-3 hours):** Hard thinking work — new module, architecture decision, design trade-off.
- **Afternoon (2-3 hours):** Test coverage, polish, documentation.
- **End-of-day (15 min):** Update `README.md` status checklist. Commit. Push. Write tomorrow's first task as a note.

## Success criteria (measured on Day 7 evening)

- [ ] Public GitHub repo under `orange-dot/<name>` with 7+ meaningful commits
- [ ] CI green on every commit to `main`
- [ ] Three architectural contrasts each demonstrable in code + tests
- [ ] README readable and overview-ready
- [ ] At least one runnable sample
- [ ] Zero patches to any submodule child
- [ ] Tag `v0.1.0` on final commit
- [ ] Public documentation scrubbed for internal-only framing

## What comes after v0.1

Out of scope for this window but plausible follow-ups:
- Second pass on contrast 5 (DI construction) and contrast 6 (error taxonomy) if they want to become primary contrasts.
- Benchmarks comparing auth-state propagation latency (observable vs delegate push).
- Documentation site under `orange-dot.github.io/<name>`.
- NuGet packaging (only if a decision is made to publish publicly, which is not a v0.1 goal).
