using System;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using OrangeDot.Supabase.Auth;
using OrangeDot.Supabase.Internal;

namespace OrangeDot.Supabase.Tests.Auth;

public sealed class AuthBindingsTests
{
    [Fact]
    public void Header_binding_replays_current_authenticated_state_and_clears_on_signed_out()
    {
        var observer = new AuthStateObserver();
        observer.Publish(new AuthState.Authenticated(
            "access-token",
            "refresh-token",
            DateTimeOffset.Parse("2026-04-07T10:00:00Z")));

        var dynamicHeaders = new DynamicAuthHeaders("anon-key");
        _ = new HeaderAuthBinding(observer, dynamicHeaders, NullLogger<HeaderAuthBinding>.Instance);

        Assert.Equal("anon-key", dynamicHeaders.Build()["apikey"]);
        Assert.Equal("Bearer access-token", dynamicHeaders.Build()["Authorization"]);

        observer.Publish(new AuthState.SignedOut());

        Assert.Equal("anon-key", dynamicHeaders.Build()["apikey"]);
        Assert.DoesNotContain("Authorization", dynamicHeaders.Build().Keys);
    }

    [Fact]
    public void Realtime_binding_replays_current_authenticated_state_and_removes_channels_on_signed_out()
    {
        var observer = new AuthStateObserver();
        observer.Publish(new AuthState.Authenticated(
            "access-token",
            "refresh-token",
            DateTimeOffset.Parse("2026-04-07T10:00:00Z")));

        var realtime = new global::Supabase.Realtime.Client("wss://abc.supabase.co/realtime/v1");
        SetSocket(realtime, new global::Supabase.Realtime.RealtimeSocket(
            "wss://abc.supabase.co/realtime/v1",
            new global::Supabase.Realtime.ClientOptions()));

        _ = new RealtimeTokenBinding(observer, realtime, NullLogger<RealtimeTokenBinding>.Instance);

        Assert.Equal("access-token", ReadPrivateStringMember(realtime, "AccessToken"));

        var firstChannel = realtime.Channel("first");
        var secondChannel = realtime.Channel("second");
        observer.Publish(new AuthState.Authenticated(
            "updated-token",
            "refresh-token",
            DateTimeOffset.Parse("2026-04-07T10:30:00Z")));

        Assert.Equal("updated-token", firstChannel.Options.Parameters!["user_token"]);
        Assert.Equal("updated-token", secondChannel.Options.Parameters!["user_token"]);

        observer.Publish(new AuthState.SignedOut());

        Assert.Empty(realtime.Subscriptions);
    }

    private static void SetSocket(global::Supabase.Realtime.Client client, global::Supabase.Realtime.RealtimeSocket socket)
    {
        var field = typeof(global::Supabase.Realtime.Client).GetField(
            "<Socket>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        field!.SetValue(client, socket);
    }

    private static string ReadPrivateStringMember(object instance, string memberName)
    {
        var property = instance.GetType().GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (property is not null)
        {
            return Assert.IsType<string>(property.GetValue(instance));
        }

        var field = instance.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        return Assert.IsType<string>(field!.GetValue(instance));
    }
}
