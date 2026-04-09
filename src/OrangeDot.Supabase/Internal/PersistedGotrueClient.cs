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

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.SignUp(
        global::Supabase.Gotrue.Constants.SignUpType type,
        string identifier,
        string password,
        global::Supabase.Gotrue.SignUpOptions? options)
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.SignUp(type, identifier, password, options),
            GotrueAuthState.SignedIn).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.SignUp(
        string email,
        string password,
        global::Supabase.Gotrue.SignUpOptions? options)
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.SignUp(email, password, options),
            GotrueAuthState.SignedIn).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.SignInAnonymously(global::Supabase.Gotrue.SignInAnonymouslyOptions? options)
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.SignInAnonymously(options),
            GotrueAuthState.SignedIn).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.RefreshSession()
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.RefreshSession(),
            GotrueAuthState.TokenRefreshed).ConfigureAwait(false);
    }

    async Task IGotrueClient.RefreshToken()
    {
        await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.RefreshToken(),
            GotrueAuthState.TokenRefreshed).ConfigureAwait(false);
    }

    async Task IGotrueClient.SignOut(global::Supabase.Gotrue.Constants.SignOutScope scope)
    {
        await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.SignOut(scope),
            GotrueAuthState.SignedOut).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.User?> IGotrueClient.Update(global::Supabase.Gotrue.UserAttributes attributes)
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.Update(attributes),
            GotrueAuthState.UserUpdated).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.Session> IGotrueClient.SetSession(string accessToken, string refreshToken, bool forceAccessTokenRefresh)
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.SetSession(accessToken, refreshToken, forceAccessTokenRefresh),
            forceAccessTokenRefresh ? GotrueAuthState.TokenRefreshed : GotrueAuthState.SignedIn).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.VerifyOTP(
        string phone,
        string token,
        global::Supabase.Gotrue.Constants.MobileOtpType type)
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.VerifyOTP(phone, token, type),
            GotrueAuthState.SignedIn).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.VerifyOTP(
        string email,
        string token,
        global::Supabase.Gotrue.Constants.EmailOtpType type)
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.VerifyOTP(email, token, type),
            GotrueAuthState.SignedIn).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.VerifyTokenHash(
        string tokenHash,
        global::Supabase.Gotrue.Constants.EmailOtpType type)
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.VerifyTokenHash(tokenHash, type),
            GotrueAuthState.SignedIn).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.SignIn(
        global::Supabase.Gotrue.Constants.SignInType type,
        string identifierOrToken,
        string? password,
        string? scopes)
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.SignIn(type, identifierOrToken, password, scopes),
            type == global::Supabase.Gotrue.Constants.SignInType.RefreshToken
                ? GotrueAuthState.TokenRefreshed
                : GotrueAuthState.SignedIn).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.SignIn(string email, string password)
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.SignIn(email, password),
            GotrueAuthState.SignedIn).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.SignInWithPassword(string email, string password)
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.SignInWithPassword(email, password),
            GotrueAuthState.SignedIn).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.SignInWithIdToken(
        global::Supabase.Gotrue.Constants.Provider provider,
        string idToken,
        string? accessToken,
        string? nonce,
        string? captchaToken)
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.SignInWithIdToken(provider, idToken, accessToken, nonce, captchaToken),
            GotrueAuthState.SignedIn).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.PasswordlessSignInState> IGotrueClient.SignInWithOtp(
        global::Supabase.Gotrue.SignInWithPasswordlessEmailOptions options)
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.SignInWithOtp(options),
            GotrueAuthState.SignedOut).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.PasswordlessSignInState> IGotrueClient.SignInWithOtp(
        global::Supabase.Gotrue.SignInWithPasswordlessPhoneOptions options)
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.SignInWithOtp(options),
            GotrueAuthState.SignedOut).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.ProviderAuthState> IGotrueClient.SignIn(
        global::Supabase.Gotrue.Constants.Provider provider,
        global::Supabase.Gotrue.SignInOptions? options)
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.SignIn(provider, options),
            GotrueAuthState.SignedOut).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.SSOResponse?> IGotrueClient.SignInWithSSO(
        Guid providerId,
        global::Supabase.Gotrue.SignInWithSSOOptions? options)
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.SignInWithSSO(providerId, options),
            GotrueAuthState.SignedOut).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.SSOResponse?> IGotrueClient.SignInWithSSO(
        string domain,
        global::Supabase.Gotrue.SignInWithSSOOptions? options)
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.SignInWithSSO(domain, options),
            GotrueAuthState.SignedOut).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.GetSessionFromUrl(Uri uri, bool storeSession)
    {
        if (!storeSession)
        {
            return await base.GetSessionFromUrl(uri, storeSession).ConfigureAwait(false);
        }

        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.GetSessionFromUrl(uri, storeSession),
            GotrueAuthState.SignedIn).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.RetrieveSessionAsync()
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.RetrieveSessionAsync(),
            GotrueAuthState.TokenRefreshed).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.ExchangeCodeForSession(string codeVerifier, string authCode)
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.ExchangeCodeForSession(codeVerifier, authCode),
            GotrueAuthState.SignedIn).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.Verify(global::Supabase.Gotrue.Mfa.MfaVerifyParams mfaVerifyParams)
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.Verify(mfaVerifyParams),
            GotrueAuthState.MfaChallengeVerified).ConfigureAwait(false);
    }

    async Task<global::Supabase.Gotrue.Session?> IGotrueClient.ChallengeAndVerify(global::Supabase.Gotrue.Mfa.MfaChallengeAndVerifyParams mfaChallengeAndVerifyParams)
    {
        return await ExecuteWithSynchronizedSessionStoreAsync(
            () => base.ChallengeAndVerify(mfaChallengeAndVerifyParams),
            GotrueAuthState.MfaChallengeVerified).ConfigureAwait(false);
    }

    private async Task<T> ExecuteWithSynchronizedSessionStoreAsync<T>(
        Func<Task<T>> operation,
        GotrueAuthState source)
    {
        using var _ = SessionStoreSyncContext.SuppressBridgePersistence();

        try
        {
            var result = await operation().ConfigureAwait(false);
            await SyncCurrentSessionOrThrowAsync(source).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await TrySyncCurrentSessionAsync(source).ConfigureAwait(false);
            throw;
        }
    }

    private async Task ExecuteWithSynchronizedSessionStoreAsync(
        Func<Task> operation,
        GotrueAuthState source)
    {
        using var _ = SessionStoreSyncContext.SuppressBridgePersistence();

        try
        {
            await operation().ConfigureAwait(false);
            await SyncCurrentSessionOrThrowAsync(source).ConfigureAwait(false);
        }
        catch
        {
            await TrySyncCurrentSessionAsync(source).ConfigureAwait(false);
            throw;
        }
    }

    private Task SyncCurrentSessionOrThrowAsync(GotrueAuthState source)
    {
        return CurrentSession is null
            ? ClearSessionOrThrowAsync(source)
            : PersistCurrentSessionOrThrowAsync(source);
    }

    private async Task TrySyncCurrentSessionAsync(GotrueAuthState source)
    {
        try
        {
            await SyncCurrentSessionOrThrowAsync(source).ConfigureAwait(false);
        }
        catch
        {
            // Preserve the original auth failure if session-store convergence also fails.
        }
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
