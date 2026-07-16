using UnityEditor;
using UnityEngine;
using System.Linq;

namespace Kaddumi.UnityTools.SOResetSystem.Editor
{

// This class represents our Presentation layer (UI).
// It is responsible ONLY for displaying data and handling user inputs.
public class SOResetManagerWindow : EditorWindow
{
    private SOResetSettings settings;
    private Vector2 scrollPosition;

    [MenuItem("Tools/Unity Tools/ScriptableObject Reset Manager")]
    public static void ShowWindow()
    {
        GetWindow<SOResetManagerWindow>("SO Resetter");
    }

    private void OnEnable()
    {
        settings = SOResetService.GetSettings();
    }

    private void OnGUI()
    {
        if (settings == null)
        {
            EditorGUILayout.HelpBox("Failed to load or create settings object.", MessageType.Error);
            return;
        }

        GUILayout.Label("SO Reset Manager", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Objects listed below will have 'OnDisable()' called when Play Mode stops.", MessageType.Info);

        DrawDragAndDropArea();

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Add All SOs from Folder (Recursive)", GUILayout.Height(30)))
        {
            AddFromFolderRecursive();
        }

        EditorGUILayout.Space(10);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawTrackedObjectsList();

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(5);
        DrawBottomControls();

        // If the user interacts with any standard GUI element, mark settings as dirty
        if (GUI.changed)
        {
            SaveSettings();
        }
    }

    private void DrawTrackedObjectsList()
    {
        for (int index = 0; index < settings.trackedObjects.Count; index++)
        {
            EditorGUILayout.BeginHorizontal();

            settings.trackedObjects[index] = (ScriptableObject)EditorGUILayout.ObjectField(
                settings.trackedObjects[index],
                typeof(ScriptableObject),
                false);

            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                settings.trackedObjects.RemoveAt(index);
                SaveSettings();
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawBottomControls()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add New Slot"))
        {
            settings.trackedObjects.Add(null);
        }

        if (GUILayout.Button("Clear All", GUILayout.Width(80)))
        {
            if (EditorUtility.DisplayDialog("Clear All?", "Are you sure you want to clear all tracked ScriptableObjects?", "Yes", "No"))
            {
                settings.trackedObjects.Clear();
                SaveSettings();
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawDragAndDropArea()
    {
        Event currentEvent = Event.current;
        Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "\nDRAG & DROP MULTIPLE ASSETS HERE", EditorStyles.helpBox);

        switch (currentEvent.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropArea.Contains(currentEvent.mousePosition))
                {
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (currentEvent.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    ProcessDraggedObjects();
                }
                break;
        }
    }

    private void ProcessDraggedObjects()
    {
        bool hasChanges = false;

        foreach (Object draggedObject in DragAndDrop.objectReferences)
        {
            if (draggedObject is ScriptableObject scriptableObject)
            {
                // Prevent adding our own settings file to the reset list
                if (scriptableObject is SOResetSettings) continue;

                if (!settings.trackedObjects.Contains(scriptableObject))
                {
                    settings.trackedObjects.Add(scriptableObject);
                    hasChanges = true;
                }
            }
        }

        if (hasChanges)
        {
            CleanAndSaveSettings();
        }
    }

    private void AddFromFolderRecursive()
    {
        string path = EditorUtility.OpenFolderPanel("Select Folder to Search for ScriptableObjects", "Assets", "");

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        // Convert system path to project relative path
        if (path.StartsWith(Application.dataPath))
        {
            path = "Assets" + path.Substring(Application.dataPath.Length);
        }
        else
        {
            EditorUtility.DisplayDialog("Invalid Folder", "Please select a folder inside the Project's Assets directory.", "OK");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { path });
        int addedCount = 0;

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            ScriptableObject scriptableObject = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

            if (scriptableObject != null && !settings.trackedObjects.Contains(scriptableObject))
            {
                // Prevent adding our own settings file to the reset list
                if (scriptableObject is SOResetSettings) continue;

                settings.trackedObjects.Add(scriptableObject);
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            CleanAndSaveSettings();
            Debug.Log($"<color=cyan><b>SO Resetter:</b></color> Added {addedCount} new ScriptableObjects from {path}");
        }
    }

    private void CleanAndSaveSettings()
    {
        // Keep the list distinct to avoid duplicate entries
        settings.trackedObjects = settings.trackedObjects.Distinct().ToList();
        SaveSettings();
    }

    private void SaveSettings()
    {
        // Tell Unity this asset has been changed so it gets saved when you save the project
        EditorUtility.SetDirty(settings);
    }
}

}