using System;
using System.Reflection;

namespace OrangeDot.Supabase.Internal;

internal static class GotrueSessionAccessor
{
    internal static void SetCurrentSession(
        global::Supabase.Gotrue.Interfaces.IGotrueClient<global::Supabase.Gotrue.User, global::Supabase.Gotrue.Session> auth,
        global::Supabase.Gotrue.Session session)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(session);

        var property = auth.GetType().GetProperty(
            nameof(global::Supabase.Gotrue.Client.CurrentSession),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? typeof(global::Supabase.Gotrue.Client).GetProperty(
                nameof(global::Supabase.Gotrue.Client.CurrentSession),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        var setter = property?.GetSetMethod(nonPublic: true);

        if (setter is not null)
        {
            setter.Invoke(auth, [session]);
            return;
        }

        var backingField = typeof(global::Supabase.Gotrue.Client).GetField(
            "<CurrentSession>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (backingField is null)
        {
            throw new InvalidOperationException(
                $"Unable to restore a persisted Supabase auth session because {auth.GetType().FullName} does not expose a writable CurrentSession property.");
        }

        backingField.SetValue(auth, session);
    }
}
