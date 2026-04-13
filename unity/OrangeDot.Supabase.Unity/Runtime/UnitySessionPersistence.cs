using System;
using System.IO;
using Newtonsoft.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace OrangeDot.Supabase.Unity
{
public sealed class UnitySessionPersistence : IGotrueSessionPersistence<Session>
{
    public const string DefaultFileName = "orange-dot.supabase.session.json";

    private readonly string _storageDirectory;
    private readonly string _fileName;

    public UnitySessionPersistence(string storageDirectory, string fileName = DefaultFileName)
    {
        if (string.IsNullOrWhiteSpace(storageDirectory))
        {
            throw new ArgumentException("Storage directory is required.", nameof(storageDirectory));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.", nameof(fileName));
        }

        _storageDirectory = storageDirectory;
        _fileName = fileName;
    }

    public string FilePath => Path.Combine(_storageDirectory, _fileName);

    public void SaveSession(Session session)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        Directory.CreateDirectory(_storageDirectory);
        File.WriteAllText(FilePath, JsonConvert.SerializeObject(session));
    }

    public void DestroySession()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
    }

    public Session? LoadSession()
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }

        var sessionJson = File.ReadAllText(FilePath);

        if (string.IsNullOrWhiteSpace(sessionJson))
        {
            return null;
        }

        return JsonConvert.DeserializeObject<Session>(sessionJson);
    }
}
}
