using System.IO;
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OrangeDot.Supabase.Unity.SmokeHost.Editor
{
    [InitializeOnLoad]
    public static class SmokeHostDemoSceneBootstrap
    {
        private const string DemoScenePath = "Assets/Scenes/LocalSupabaseAuthAndData.unity";
        private const string SessionKey = "OrangeDot.Supabase.Unity.SmokeHost.SceneOpened";
        private const string LocalSupabaseUrl = "http://127.0.0.1:54321";
        private const string LocalAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZS1kZW1vIiwicm9sZSI6ImFub24iLCJleHAiOjE5ODM4MTI5OTZ9.CRXP1A7WOeoJeXxjNni43kdQwgnWNReilDMblYTn_I0";

        static SmokeHostDemoSceneBootstrap()
        {
            EditorApplication.delayCall += OpenDemoSceneOnFirstLoad;
        }

        [MenuItem("Orange Dot/Unity SmokeHost/Rebuild Local Demo Scene")]
        public static void RebuildLocalDemoScene()
        {
            EnsureFolder("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var sampleObject = new GameObject("SupabaseSample");
            var controllerType = Type.GetType(
                "OrangeDot.Supabase.Unity.Samples.AuthAndData.AuthAndDataSampleController, OrangeDot.Supabase.Unity.AuthAndData.Sample");

            if (controllerType is null)
            {
                throw new InvalidOperationException(
                    "Could not find AuthAndDataSampleController. Make sure the sample is synced into Assets/Samples before rebuilding the scene.");
            }

            var controller = sampleObject.AddComponent(controllerType);
            var serialized = new SerializedObject(controller);

            serialized.FindProperty("ProjectUrl")!.stringValue = LocalSupabaseUrl;
            serialized.FindProperty("AnonKey")!.stringValue = LocalAnonKey;
            serialized.FindProperty("Email")!.stringValue = "unity@example.com";
            serialized.FindProperty("Password")!.stringValue = "password123";
            serialized.FindProperty("NewTodoTitle")!.stringValue = "Created from SmokeHost";
            serialized.FindProperty("FunctionName")!.stringValue = "orangedot-integration-smoke";
            serialized.FindProperty("FunctionMessage")!.stringValue = "smoke-host";
            serialized.FindProperty("StorageBucket")!.stringValue = "unity-sample";
            serialized.FindProperty("UploadFileName")!.stringValue = "sample-note.txt";
            serialized.FindProperty("UploadText")!.stringValue = "Hello from Unity storage";
            serialized.FindProperty("SignedUrlExpiresInSeconds")!.intValue = 3600;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, DemoScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(DemoScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void OpenDemoSceneOnFirstLoad()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);

            if (!File.Exists(DemoScenePath))
            {
                return;
            }

            var activeScene = SceneManager.GetActiveScene();

            if (!string.IsNullOrEmpty(activeScene.path))
            {
                return;
            }

            EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var folderName = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent ?? "Assets", folderName);
        }
    }
}
