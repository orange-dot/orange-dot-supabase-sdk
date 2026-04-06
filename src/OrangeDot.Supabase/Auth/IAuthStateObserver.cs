using System;

namespace OrangeDot.Supabase.Auth;

public interface IAuthStateObserver
{
    AuthState Current { get; }

    IDisposable Subscribe(Action<AuthState> listener);
}
