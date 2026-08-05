using UnityEngine;
using UnityEditor;

namespace Kaddumi.UnityTools.ProjectEnhancer
{
    public class FolderCustomizationPopup : EditorWindow
    {
        private string targetGuid;
        private FolderConfig config;

        private Color selectedColor = Color.clear;
        private string selectedIconName = "";

        // Built-in icon names for folders
        private readonly string[] builtinIcons = new string[]
        {
            "", // None
            "Folder Icon", // Default
            "FolderFavorite Icon",
            "FolderOpened Icon",
            "sv_label_0",
            "sv_label_1",
            "sv_label_2",
            "sv_label_3",
            "sv_label_4",
            "sv_label_5",
            "sv_label_6",
            "sv_label_7",
            "cs Script Icon",
            "Material Icon",
            "Prefab Icon",
            "SceneAsset Icon",
            "SettingsIcon"
        };

        [MenuItem("Assets/Customize Folder", true)]
        private static bool ValidateCustomizeFolder()
        {
            if (Selection.activeObject == null) return false;
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return AssetDatabase.IsValidFolder(path);
        }

        [MenuItem("Assets/Customize Folder", false, 20)]
        private static void ShowWindow()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            string guid = AssetDatabase.AssetPathToGUID(path);

            var window = GetWindow<FolderCustomizationPopup>(true, "Customize Folder", true);
            window.minSize = new Vector2(250, 320);
            window.maxSize = new Vector2(250, 320);
            window.Init(guid);
        }

        public void Init(string guid)
        {
            targetGuid = guid;
            config = ProjectFolderData.instance.GetConfig(guid);
            
            if (config != null)
            {
                selectedColor = config.color;
                selectedIconName = config.iconName;
            }
            else
            {
                selectedColor = Color.clear;
                selectedIconName = "";
            }
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(targetGuid))
            {
                Close();
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(targetGuid);
            GUILayout.Label("Customizing: " + System.IO.Path.GetFileName(path), EditorStyles.boldLabel);

            EditorGUILayout.Space();

            selectedColor = EditorGUILayout.ColorField("Highlight Color", selectedColor);
            if (GUILayout.Button("Clear Color"))
            {
                selectedColor = Color.clear;
            }

            EditorGUILayout.Space();
            GUILayout.Label("Select Icon", EditorStyles.boldLabel);

            // Draw a grid of icons
            int iconsPerRow = 5;
            float iconSize = 40;
            
            GUILayout.BeginVertical();
            for (int i = 0; i < builtinIcons.Length; i += iconsPerRow)
            {
                GUILayout.BeginHorizontal();
                for (int j = 0; j < iconsPerRow; j++)
                {
                    int index = i + j;
                    if (index >= builtinIcons.Length) break;

                    string iconName = builtinIcons[index];
                    GUIContent iconContent = string.IsNullOrEmpty(iconName) ? new GUIContent("X") : EditorGUIUtility.IconContent(iconName);
                    
                    if (iconContent == null || iconContent.image == null && !string.IsNullOrEmpty(iconName))
                    {
                         iconContent = new GUIContent(iconName); // Fallback
                    }

                    bool isSelected = selectedIconName == iconName;
                    GUI.backgroundColor = isSelected ? Color.cyan : Color.white;

                    if (GUILayout.Button(iconContent, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
                    {
                        selectedIconName = iconName;
                    }
                    GUI.backgroundColor = Color.white;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            EditorGUILayout.Space();
            
            selectedIconName = EditorGUILayout.TextField("Custom Icon Name", selectedIconName);

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save"))
            {
                ProjectFolderData.instance.SetConfig(targetGuid, selectedColor, selectedIconName);
                EditorApplication.RepaintProjectWindow();
                Close();
            }
            if (GUILayout.Button("Cancel"))
            {
                Close();
            }
            GUILayout.EndHorizontal();
            
            if (GUILayout.Button("Reset to Default"))
            {
                ProjectFolderData.instance.ClearConfig(targetGuid);
                EditorApplication.RepaintProjectWindow();
                Close();
            }
        }
    }
}
