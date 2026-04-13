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

    private SupabaseUnityClient? _client;
    private List<UnityTodoItem> _items = new();
    private string _lastFunctionResponse = "(none yet)";
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

        GUILayout.Space(8);
        GUILayout.Label($"Busy: {_busy}");
        GUILayout.Label($"Authenticated: {_client?.IsAuthenticated == true}");
        GUILayout.Label($"User: {_client?.CurrentUser?.Email ?? _client?.CurrentUser?.Id ?? "(anonymous)"}");
        GUILayout.Label($"Status: {_status}");

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
                .Where(item => item.OwnerId == _client.CurrentUser!.Id)
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
            _items.Clear();
            _lastFunctionResponse = "(none yet)";
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
