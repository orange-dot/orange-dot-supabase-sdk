using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using static Supabase.Postgrest.Constants;

namespace OrangeDot.Supabase.Unity.Samples.AuthAndData
{
public sealed class AuthAndDataSampleController : MonoBehaviour
{
    [Header("Project")]
    public string ProjectUrl = string.Empty;

    public string AnonKey = string.Empty;

    [Header("Auth")]
    public string Email = string.Empty;

    public string Password = string.Empty;

    [Header("Data")]
    public string NewTodoTitle = "Created from Unity";

    [Header("Functions")]
    public string FunctionName = "unity-hello";

    public string FunctionMessage = "Hello from Unity";

    [Header("Storage")]
    public string StorageBucket = "unity-sample";

    public string UploadFileName = "sample-note.txt";

    [TextArea(3, 6)]
    public string UploadText = "Hello from Unity storage";

    public int SignedUrlExpiresInSeconds = 3600;

    private SupabaseUnityClient? _client;
    private List<UnityTodoItem> _items = new();
    private readonly List<string> _storageEntries = new();
    private string _lastFunctionResponse = "(none yet)";
    private string _lastUploadedPath = "(none yet)";
    private string _lastSignedUrl = "(none yet)";
    private string _status = "Configure ProjectUrl and AnonKey, then initialize.";
    private bool _busy;

    private async void Start()
    {
        if (!string.IsNullOrWhiteSpace(ProjectUrl) && !string.IsNullOrWhiteSpace(AnonKey))
        {
            await InitializeAsync();
        }
    }

    private void OnDestroy()
    {
        _client?.Dispose();
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(20, 20, 720, Screen.height - 40), GUI.skin.box);
        GUILayout.Label("Orange Dot Supabase Unity");
        GUILayout.Space(8);

        ProjectUrl = LabeledTextField("Project URL", ProjectUrl);
        AnonKey = LabeledTextField("Anon Key", AnonKey);
        Email = LabeledTextField("Email", Email);
        Password = LabeledPasswordField("Password", Password);
        NewTodoTitle = LabeledTextField("New Todo", NewTodoTitle);
        FunctionName = LabeledTextField("Function Name", FunctionName);
        FunctionMessage = LabeledTextField("Function Message", FunctionMessage);
        StorageBucket = LabeledTextField("Storage Bucket", StorageBucket);
        UploadFileName = LabeledTextField("Upload File Name", UploadFileName);
        UploadText = LabeledTextArea("Upload Text", UploadText, 72f);
        SignedUrlExpiresInSeconds = LabeledIntField("Signed URL Seconds", SignedUrlExpiresInSeconds);

        GUILayout.Space(8);
        GUILayout.Label($"Busy: {_busy}");
        GUILayout.Label($"Authenticated: {_client?.IsAuthenticated == true}");
        GUILayout.Label($"User: {_client?.CurrentUser?.Email ?? _client?.CurrentUser?.Id ?? "(anonymous)"}");
        GUILayout.Label($"Status: {_status}");
        GUILayout.Label($"Last Uploaded Path: {_lastUploadedPath}");

        GUILayout.Space(8);

        using (new EditorLikeDisabledScope(_busy))
        {
            if (GUILayout.Button("Initialize"))
            {
                _ = InitializeAsync();
            }

            if (GUILayout.Button("Sign In"))
            {
                _ = SignInAsync();
            }

            if (GUILayout.Button("Insert Sample Row"))
            {
                _ = InsertTodoAsync();
            }

            if (GUILayout.Button("Load My Rows"))
            {
                _ = LoadTodosAsync();
            }

            if (GUILayout.Button("Invoke Function"))
            {
                _ = InvokeFunctionAsync();
            }

            if (GUILayout.Button("Upload Sample Bytes"))
            {
                _ = UploadSampleBytesAsync();
            }

            if (GUILayout.Button("List Bucket Files"))
            {
                _ = ListBucketFilesAsync();
            }

            if (GUILayout.Button("Create Signed Url"))
            {
                _ = CreateSignedUrlAsync();
            }

            if (GUILayout.Button("Sign Out"))
            {
                _ = SignOutAsync();
            }
        }

        GUILayout.Space(8);
        GUILayout.Label("Rows:");

        foreach (var item in _items)
        {
            GUILayout.Label($"- #{item.Id} {item.Title} ({item.CreatedAt:u})");
        }

        GUILayout.Space(8);
        GUILayout.Label("Function Response:");
        GUILayout.Label(_lastFunctionResponse);

        GUILayout.Space(8);
        GUILayout.Label("Storage Files:");

        foreach (var entry in _storageEntries)
        {
            GUILayout.Label($"- {entry}");
        }

        GUILayout.Space(8);
        GUILayout.Label("Signed URL:");
        GUILayout.Label(_lastSignedUrl);

        GUILayout.EndArea();
    }

    private async Task InitializeAsync()
    {
        if (_client is not null)
        {
            _status = "Client is already initialized.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ProjectUrl) || string.IsNullOrWhiteSpace(AnonKey))
        {
            _status = "ProjectUrl and AnonKey are required.";
            return;
        }

        _busy = true;

        try
        {
            _client = new SupabaseUnityClient(
                new SupabaseUnityOptions
                {
                    ProjectUrl = ProjectUrl,
                    AnonKey = AnonKey,
                    RefreshSessionOnInitialize = true
                },
                new UnitySessionPersistence(Application.persistentDataPath));

            var session = await _client.InitializeAsync();
            _status = session is null
                ? "Initialized anonymously."
                : $"Restored session for {_client.CurrentUser?.Email ?? _client.CurrentUser?.Id}.";
        }
        catch (Exception ex)
        {
            _status = ex.Message;
            Debug.LogException(ex);
            _client?.Dispose();
            _client = null;
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task SignInAsync()
    {
        if (!EnsureClient())
        {
            return;
        }

        _busy = true;

        try
        {
            var session = await _client!.SignInWithPasswordAsync(Email, Password);
            _status = session is null
                ? "No session returned."
                : $"Signed in as {_client.CurrentUser?.Email ?? _client.CurrentUser?.Id}.";
        }
        catch (Exception ex)
        {
            _status = ex.Message;
            Debug.LogException(ex);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task InsertTodoAsync()
    {
        if (!EnsureAuthenticated())
        {
            return;
        }

        _busy = true;

        try
        {
            await _client!.Table<UnityTodoItem>().Insert(new UnityTodoItem
            {
                OwnerId = _client.CurrentUser!.Id,
                Title = string.IsNullOrWhiteSpace(NewTodoTitle)
                    ? $"Unity sample {DateTime.UtcNow:u}"
                    : NewTodoTitle
            });

            _status = "Inserted a new row.";
            await LoadTodosAsync();
        }
        catch (Exception ex)
        {
            _status = ex.Message;
            Debug.LogException(ex);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task LoadTodosAsync()
    {
        if (!EnsureAuthenticated())
        {
            return;
        }

        _busy = true;

        try
        {
            var response = await _client!
                .Table<UnityTodoItem>()
                .Filter("owner_id", Operator.Equals, _client.CurrentUser!.Id)
                .Order(item => item.CreatedAt, Ordering.Descending)
                .Get();

            _items = response.Models;
            _status = $"Loaded {_items.Count} rows.";
        }
        catch (Exception ex)
        {
            _status = ex.Message;
            Debug.LogException(ex);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task SignOutAsync()
    {
        if (!EnsureClient())
        {
            return;
        }

        _busy = true;

        try
        {
            await _client!.SignOutAsync();
            ResetTransientState();
            _status = "Signed out.";
        }
        catch (Exception ex)
        {
            _status = ex.Message;
            Debug.LogException(ex);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task UploadSampleBytesAsync()
    {
        if (!EnsureAuthenticated())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(StorageBucket))
        {
            _status = "StorageBucket is required.";
            return;
        }

        _busy = true;

        try
        {
            var objectPath = BuildStorageObjectPath(_client!.CurrentUser!.Id, UploadFileName);
            await _client.UploadTextAsync(StorageBucket, objectPath, UploadText);
            _lastUploadedPath = objectPath;
            _status = $"Uploaded '{objectPath}' to bucket '{StorageBucket}'.";
            await ListBucketFilesCoreAsync();
        }
        catch (Exception ex)
        {
            _status = ex.Message;
            Debug.LogException(ex);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task ListBucketFilesAsync()
    {
        if (!EnsureAuthenticated())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(StorageBucket))
        {
            _status = "StorageBucket is required.";
            return;
        }

        _busy = true;

        try
        {
            await ListBucketFilesCoreAsync();
        }
        catch (Exception ex)
        {
            _status = ex.Message;
            Debug.LogException(ex);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task CreateSignedUrlAsync()
    {
        if (!EnsureAuthenticated())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(StorageBucket))
        {
            _status = "StorageBucket is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_lastUploadedPath) || _lastUploadedPath == "(none yet)")
        {
            _status = "Upload a sample file first.";
            return;
        }

        if (SignedUrlExpiresInSeconds <= 0)
        {
            _status = "SignedUrlExpiresInSeconds must be greater than zero.";
            return;
        }

        _busy = true;

        try
        {
            _lastSignedUrl = await _client!.CreateSignedUrlAsync(
                StorageBucket,
                _lastUploadedPath,
                SignedUrlExpiresInSeconds);

            _status = $"Created a signed URL for '{_lastUploadedPath}'.";
        }
        catch (Exception ex)
        {
            _status = ex.Message;
            Debug.LogException(ex);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task InvokeFunctionAsync()
    {
        if (!EnsureAuthenticated())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(FunctionName))
        {
            _status = "FunctionName is required.";
            return;
        }

        _busy = true;

        try
        {
            var response = await _client!.InvokeFunctionAsync(
                FunctionName,
                new Dictionary<string, object>
                {
                    ["source"] = string.IsNullOrWhiteSpace(FunctionMessage)
                        ? "unity-auth-and-data-sample"
                        : FunctionMessage,
                    ["message"] = string.IsNullOrWhiteSpace(FunctionMessage)
                        ? "Hello from Unity"
                        : FunctionMessage,
                    ["userId"] = _client.CurrentUser!.Id,
                    ["email"] = _client.CurrentUser!.Email ?? string.Empty
                });

            _lastFunctionResponse = response;
            _status = $"Invoked function '{FunctionName}'.";
        }
        catch (Exception ex)
        {
            _status = ex.Message;
            Debug.LogException(ex);
        }
        finally
        {
            _busy = false;
        }
    }

    private bool EnsureClient()
    {
        if (_client is not null)
        {
            return true;
        }

        _status = "Initialize the client first.";
        return false;
    }

    private bool EnsureAuthenticated()
    {
        if (!EnsureClient())
        {
            return false;
        }

        if (_client!.CurrentUser is not null)
        {
            return true;
        }

        _status = "Sign in first.";
        return false;
    }

    private static string LabeledTextField(string label, string value)
    {
        GUILayout.Label(label);
        return GUILayout.TextField(value ?? string.Empty);
    }

    private static string LabeledPasswordField(string label, string value)
    {
        GUILayout.Label(label);
        return GUILayout.PasswordField(value ?? string.Empty, '*');
    }

    private static string LabeledTextArea(string label, string value, float height)
    {
        GUILayout.Label(label);
        return GUILayout.TextArea(value ?? string.Empty, GUILayout.MinHeight(height));
    }

    private static int LabeledIntField(string label, int value)
    {
        GUILayout.Label(label);

        if (int.TryParse(GUILayout.TextField(value.ToString()), out var parsed))
        {
            return parsed;
        }

        return value;
    }

    private static string BuildStorageObjectPath(string userId, string fileName)
    {
        var normalizedName = string.IsNullOrWhiteSpace(fileName)
            ? "sample-note.txt"
            : fileName.Trim().Replace(' ', '-');

        if (!normalizedName.Contains('.'))
        {
            normalizedName += ".txt";
        }

        return $"{userId}/{normalizedName}";
    }

    private async Task ListBucketFilesCoreAsync()
    {
        var prefix = _client!.CurrentUser!.Id;
        var files = await _client.ListFilesAsync(StorageBucket, prefix);

        _storageEntries.Clear();

        foreach (var file in files)
        {
            var displayName = file.Name ?? "(unnamed)";
            var timestamp = file.UpdatedAt ?? file.CreatedAt;
            var suffix = timestamp.HasValue ? $" ({timestamp.Value:u})" : string.Empty;
            _storageEntries.Add($"{displayName}{suffix}");
        }

        _status = $"Loaded {_storageEntries.Count} storage object(s) from '{StorageBucket}'.";
    }

    private void ResetTransientState()
    {
        _items.Clear();
        _storageEntries.Clear();
        _lastFunctionResponse = "(none yet)";
        _lastUploadedPath = "(none yet)";
        _lastSignedUrl = "(none yet)";
    }

    private readonly struct EditorLikeDisabledScope : IDisposable
    {
        private readonly bool _previousState;

        public EditorLikeDisabledScope(bool disabled)
        {
            _previousState = GUI.enabled;
            GUI.enabled = !disabled;
        }

        public void Dispose()
        {
            GUI.enabled = _previousState;
        }
    }
}
}
