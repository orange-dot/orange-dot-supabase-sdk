using System;
using System.IO;
using OrangeDot.Supabase.Unity;
using Supabase.Gotrue;

namespace OrangeDot.Supabase.Unity.Tests;

public sealed class UnitySessionPersistenceTests
{
    [Fact]
    public void SaveLoadAndDestroySession_RoundTripsThroughDisk()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "orange-dot-supabase-unity-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var persistence = new UnitySessionPersistence(tempDirectory);
            var session = new Session
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
                ExpiresIn = 3600,
                User = new User
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Email = "unity@example.com"
                }
            };

            persistence.SaveSession(session);

            var restored = persistence.LoadSession();

            Assert.NotNull(restored);
            Assert.Equal("access-token", restored!.AccessToken);
            Assert.Equal("refresh-token", restored.RefreshToken);
            Assert.Equal("unity@example.com", restored.User!.Email);

            persistence.DestroySession();

            Assert.Null(persistence.LoadSession());
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
