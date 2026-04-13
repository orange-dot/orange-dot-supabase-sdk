using System;
using System.Collections.Generic;

namespace OrangeDot.Supabase.Internal;

internal enum AuthModelActionKind
{
    StartBinding,
    SignIn,
    BeginRefresh,
    CompleteRefresh,
    RefreshFail,
    IgnoreStaleRefreshResult,
    ProjectCurrentToBinding,
    ClearBindingProjection,
    SignOut
}

internal sealed record AuthModelAction(AuthModelActionKind Kind, string? BindingName = null);

internal sealed class AuthTraceToModelActionTranslator
{
    private static readonly string[] HeaderBindings = ["Postgrest", "Storage", "Functions"];
    private static readonly string[] RealtimeBindings = ["Realtime"];

    private readonly Dictionary<string, bool> _liveBindings = new(StringComparer.Ordinal)
    {
        ["Postgrest"] = false,
        ["Realtime"] = false,
        ["Storage"] = false,
        ["Functions"] = false
    };

    private readonly Dictionary<string, long> _projectedVersions = new(StringComparer.Ordinal)
    {
        ["Postgrest"] = 0,
        ["Realtime"] = 0,
        ["Storage"] = 0,
        ["Functions"] = 0
    };

    private string _authState = "Anonymous";
    private long _canonicalVersion;
    private long _pendingRefreshVersion;

    internal IReadOnlyList<AuthModelAction> Translate(IEnumerable<RuntimeTraceEvent> traceEvents)
    {
        ArgumentNullException.ThrowIfNull(traceEvents);
        Reset();

        var actions = new List<AuthModelAction>();

        foreach (var traceEvent in traceEvents)
        {
            switch (traceEvent)
            {
                case AuthTraceEvent authTrace:
                    TranslateAuthTrace(authTrace, actions);
                    break;
                case BindingProjectionTraceEvent bindingTrace:
                    TranslateBindingTrace(bindingTrace, actions);
                    break;
            }
        }

        return actions.ToArray();
    }

    private void Reset()
    {
        _authState = "Anonymous";
        _canonicalVersion = 0;
        _pendingRefreshVersion = 0;

        foreach (var bindingName in _liveBindings.Keys)
        {
            _liveBindings[bindingName] = false;
            _projectedVersions[bindingName] = 0;
        }
    }

    private void TranslateAuthTrace(AuthTraceEvent authTrace, List<AuthModelAction> actions)
    {
        switch (authTrace.Kind)
        {
            case AuthTraceKind.InitialSessionPublished:
            case AuthTraceKind.SignedInPublished:
                actions.Add(new AuthModelAction(AuthModelActionKind.SignIn));
                UpdateAuthSnapshot(authTrace);
                return;
            case AuthTraceKind.RefreshBeginPublished:
                actions.Add(new AuthModelAction(AuthModelActionKind.BeginRefresh));
                UpdateAuthSnapshot(authTrace);
                return;
            case AuthTraceKind.RefreshCompletedPublished:
                actions.Add(new AuthModelAction(AuthModelActionKind.CompleteRefresh));
                UpdateAuthSnapshot(authTrace);
                return;
            case AuthTraceKind.RefreshFailedPublished:
                actions.Add(new AuthModelAction(AuthModelActionKind.RefreshFail));
                UpdateAuthSnapshot(authTrace);
                return;
            case AuthTraceKind.SignedOutPublished:
                actions.Add(new AuthModelAction(AuthModelActionKind.SignOut));
                UpdateAuthSnapshot(authTrace);

                foreach (var bindingName in _projectedVersions.Keys)
                {
                    if (_liveBindings[bindingName])
                    {
                        _projectedVersions[bindingName] = 0;
                    }
                }

                return;
            case AuthTraceKind.StaleRefreshIgnored:
                actions.Add(new AuthModelAction(AuthModelActionKind.IgnoreStaleRefreshResult));
                UpdateAuthSnapshot(authTrace);
                return;
            case AuthTraceKind.UserUpdatedPublished:
            case AuthTraceKind.FaultedPublished:
            case AuthTraceKind.MfaChallengeVerifiedPublished:
                throw new InvalidOperationException(
                    $"Auth trace kind '{authTrace.Kind}' is not yet mapped to a TLA action.");
            default:
                throw new ArgumentOutOfRangeException(nameof(authTrace), authTrace.Kind, "Unknown auth trace kind.");
        }
    }

    private void TranslateBindingTrace(BindingProjectionTraceEvent bindingTrace, List<AuthModelAction> actions)
    {
        var bindingNames = bindingTrace.Target switch
        {
            BindingTarget.Header => HeaderBindings,
            BindingTarget.Realtime => RealtimeBindings,
            _ => throw new ArgumentOutOfRangeException(nameof(bindingTrace), bindingTrace.Target, "Unknown binding target.")
        };

        ValidateProjection(bindingTrace);

        foreach (var bindingName in bindingNames)
        {
            if (!_liveBindings[bindingName])
            {
                actions.Add(new AuthModelAction(AuthModelActionKind.StartBinding, bindingName));
                _liveBindings[bindingName] = true;
                _projectedVersions[bindingName] = bindingTrace.ProjectedVersion;
                continue;
            }

            if (bindingTrace.Action == BindingProjectionAction.Applied)
            {
                if (_projectedVersions[bindingName] != bindingTrace.ProjectedVersion)
                {
                    actions.Add(new AuthModelAction(AuthModelActionKind.ProjectCurrentToBinding, bindingName));
                    _projectedVersions[bindingName] = bindingTrace.ProjectedVersion;
                }

                continue;
            }

            if (_projectedVersions[bindingName] != 0)
            {
                actions.Add(new AuthModelAction(AuthModelActionKind.ClearBindingProjection, bindingName));
                _projectedVersions[bindingName] = 0;
            }
        }
    }

    private void UpdateAuthSnapshot(AuthTraceEvent authTrace)
    {
        _authState = authTrace.State;
        _canonicalVersion = authTrace.CanonicalVersion;
        _pendingRefreshVersion = authTrace.PendingRefreshVersion;
    }

    private void ValidateProjection(BindingProjectionTraceEvent bindingTrace)
    {
        var expectedProjectionVersion = _authState is "Authenticated" or "Refreshing"
            ? _canonicalVersion
            : 0;

        if (bindingTrace.ProjectedVersion != expectedProjectionVersion)
        {
            throw new InvalidOperationException(
                $"Binding projection version {bindingTrace.ProjectedVersion} does not match auth projection version {expectedProjectionVersion}.");
        }

        if (bindingTrace.Action == BindingProjectionAction.Cleared && bindingTrace.ProjectedVersion != 0)
        {
            throw new InvalidOperationException("Cleared binding projections must reset to version 0.");
        }

        if (bindingTrace.Action == BindingProjectionAction.Applied && bindingTrace.ProjectedVersion == 0)
        {
            throw new InvalidOperationException("Applied binding projections must carry a non-zero version.");
        }

        if (bindingTrace.PendingRefreshVersion != _pendingRefreshVersion)
        {
            throw new InvalidOperationException(
                $"Binding pending refresh version {bindingTrace.PendingRefreshVersion} does not match auth pending refresh version {_pendingRefreshVersion}.");
        }
    }
}
