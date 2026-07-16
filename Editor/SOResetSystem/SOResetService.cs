using UnityEditor;
using UnityEngine;

namespace Kaddumi.UnityTools.SOResetSystem.Editor
{

// This class represents our Logic layer (Service).
// It handles initialization, finding/creating data, and the core resetting process.
[InitializeOnLoad]
public static class SOResetService
{
    private const string defaultSettingsFolder = "Assets/Editor";
    private const string defaultSettingsPath = "Assets/Editor/SOResetSettings.asset";

    static SOResetService()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
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

}