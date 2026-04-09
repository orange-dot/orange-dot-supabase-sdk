using System;
using System.Threading.Tasks;
using GotrueAuthState = global::Supabase.Gotrue.Constants.AuthState;
using IGotrueClient = global::Supabase.Gotrue.Interfaces.IGotrueClient<global::Supabase.Gotrue.User, global::Supabase.Gotrue.Session>;

namespace OrangeDot.Supabase.Internal;

internal sealed class PersistedGotrueClient : global::Supabase.Gotrue.Client, IGotrueClient
{
    private readonly ISupabaseSessionStore _sessionStore;

    internal PersistedGotrueClient(
        global::Supabase.Gotrue.ClientOptions options,
        ISupabaseSessionStore sessionStore)
        : base(options)
    {
        _sessionStore = sessionStore ?? NoOpSupabaseSessionStore.Instance;
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.SignInAnonymously(global::Supabase.Gotrue.SignInAnonymouslyOptions? options)
    {
        var session = await base.SignInAnonymously(options).ConfigureAwait(false);
        await PersistCurrentSessionOrThrowAsync(GotrueAuthState.SignedIn).ConfigureAwait(false);
        return session;
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.RefreshSession()
    {
        var session = await base.RefreshSession().ConfigureAwait(false);
        await PersistCurrentSessionOrThrowAsync(GotrueAuthState.TokenRefreshed).ConfigureAwait(false);
        return session;
    }

    async Task IGotrueClient.SignOut(global::Supabase.Gotrue.Constants.SignOutScope scope)
    {
        await base.SignOut(scope).ConfigureAwait(false);
        await ClearSessionOrThrowAsync(GotrueAuthState.SignedOut).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.User?> IGotrueClient.Update(global::Supabase.Gotrue.UserAttributes attributes)
    {
        var user = await base.Update(attributes).ConfigureAwait(false);
        await PersistCurrentSessionOrThrowAsync(GotrueAuthState.UserUpdated).ConfigureAwait(false);
        return user;
    }

    async Task<global::Supabase.Gotrue.Session> IGotrueClient.SetSession(string accessToken, string refreshToken, bool forceAccessTokenRefresh)
    {
        var session = await base.SetSession(accessToken, refreshToken, forceAccessTokenRefresh).ConfigureAwait(false);
        await PersistCurrentSessionOrThrowAsync(GotrueAuthState.SignedIn).ConfigureAwait(false);
        return session;
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.VerifyOTP(
        string phone,
        string token,
        global::Supabase.Gotrue.Constants.MobileOtpType type)
    {
        var session = await base.VerifyOTP(phone, token, type).ConfigureAwait(false);
        await PersistCurrentSessionOrThrowAsync(GotrueAuthState.SignedIn).ConfigureAwait(false);
        return session;
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.VerifyOTP(
        string email,
        string token,
        global::Supabase.Gotrue.Constants.EmailOtpType type)
    {
        var session = await base.VerifyOTP(email, token, type).ConfigureAwait(false);
        await PersistCurrentSessionOrThrowAsync(GotrueAuthState.SignedIn).ConfigureAwait(false);
        return session;
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.VerifyTokenHash(
        string tokenHash,
        global::Supabase.Gotrue.Constants.EmailOtpType type)
    {
        var session = await base.VerifyTokenHash(tokenHash, type).ConfigureAwait(false);
        await PersistCurrentSessionOrThrowAsync(GotrueAuthState.SignedIn).ConfigureAwait(false);
        return session;
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.SignIn(
        global::Supabase.Gotrue.Constants.SignInType type,
        string identifierOrToken,
        string? password,
        string? scopes)
    {
        var session = await base.SignIn(type, identifierOrToken, password, scopes).ConfigureAwait(false);
        await PersistCurrentSessionOrThrowAsync(GotrueAuthState.SignedIn).ConfigureAwait(false);
        return session;
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.SignIn(string email, string password)
    {
        var session = await base.SignIn(email, password).ConfigureAwait(false);
        await PersistCurrentSessionOrThrowAsync(GotrueAuthState.SignedIn).ConfigureAwait(false);
        return session;
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.SignInWithPassword(string email, string password)
    {
        var session = await base.SignInWithPassword(email, password).ConfigureAwait(false);
        await PersistCurrentSessionOrThrowAsync(GotrueAuthState.SignedIn).ConfigureAwait(false);
        return session;
    }

    private async Task PersistCurrentSessionOrThrowAsync(GotrueAuthState source)
    {
        if (CurrentSession is null)
        {
            return;
        }

        try
        {
            await _sessionStore.PersistAsync(CurrentSession).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw SessionStoreAuthExceptions.Create(source, persist: true, exception);
        }
    }

    private async Task ClearSessionOrThrowAsync(GotrueAuthState source)
    {
        try
        {
            await _sessionStore.ClearAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw SessionStoreAuthExceptions.Create(source, persist: false, exception);
        }
    }
}
