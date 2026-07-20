using Kaddumi.UnityTools.ToolManager.Editor;
using UnityEditor;
using UnityEngine;

namespace Kaddumi.UnityTools.SOResetSystem.Editor
{

// This class represents our Logic layer (Service).
// It handles initialization, finding/creating data, and the core resetting process.
//
// The auto-reset behaviour is switched on/off through the Tool Manager via SOResetToolModule; the
// service itself no longer subscribes on load. The settings asset and manager window are unaffected.
public static class SOResetService
{
    private const string defaultSettingsFolder = "Assets/Editor";
    private const string defaultSettingsPath = "Assets/Editor/SOResetSettings.asset";

    /// <summary> Start listening for the return to Edit Mode. Idempotent. </summary>
    public static void Subscribe()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    /// <summary> Stop listening; tracked objects are no longer reset on Play Mode exit. </summary>
    public static void Unsubscribe()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            ResetAllTrackedObjects();
        }
    }

    public static SOResetSettings GetSettings()
    {
        string[] guids = AssetDatabase.FindAssets("t:SOResetSettings");

        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<SOResetSettings>(path);
        }

        return CreateSettings();
    }

    private static SOResetSettings CreateSettings()
    {
        SOResetSettings settings = ScriptableObject.CreateInstance<SOResetSettings>();

        if (!AssetDatabase.IsValidFolder(defaultSettingsFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Editor");
        }

        AssetDatabase.CreateAsset(settings, defaultSettingsPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"<color=cyan><b>SO Resetter:</b></color> Created new settings file at {defaultSettingsPath}");

        return settings;
    }

    private static void ResetAllTrackedObjects()
    {
        SOResetSettings settings = GetSettings();

        if (settings == null || settings.trackedObjects == null)
        {
            return;
        }

        int resetCount = 0;

        foreach (ScriptableObject scriptableObject in settings.trackedObjects)
        {
            if (scriptableObject != null)
            {
                ResetObject(scriptableObject);
                resetCount++;
            }
        }

        if (resetCount > 0)
        {
            Debug.Log($"<color=cyan><b>SO Resetter:</b></color> Successfully reset {resetCount} ScriptableObjects.");
        }
    }

    private static void ResetObject(ScriptableObject scriptableObject)
    {
        var onDisableMethod = scriptableObject.GetType().GetMethod(
            "OnDisable",
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        onDisableMethod?.Invoke(scriptableObject, null);
        EditorUtility.SetDirty(scriptableObject);
    }
}

/// <summary>
/// Exposes <see cref="SOResetService"/>'s auto-reset behaviour to the Tool Manager so it can be
/// turned on and off. When active, tracked ScriptableObjects are reset on returning to Edit Mode.
/// The manager window (Tools ▸ Unity Tools ▸ ScriptableObject Reset Manager) and its settings asset
/// remain available regardless of this toggle.
/// </summary>
public sealed class SOResetToolModule : IEditorToolModule
{
    public string Id => "SOResetService";
    public string DisplayName => "ScriptableObject Auto-Reset";
    public string Description =>
        "Resets tracked ScriptableObjects (calls their OnDisable) when Play Mode stops, so play-mode " +
        "state doesn't leak into serialized assets. Edit the tracked list in the SO Reset Manager window.";
    public string Category => "Play Mode";
    public bool DefaultEnabled => true;

    public void OnActivated() => SOResetService.Subscribe();
    public void OnDeactivated() => SOResetService.Unsubscribe();
}

}