using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

namespace Kaddumi.UnityTools.ProjectEnhancer
{
    [InitializeOnLoad]
    public static class ProjectWindowEnhancer
    {
        static ProjectWindowEnhancer()
        {
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            if (Event.current.type != EventType.Repaint) return;

            var settings = ProjectWindowEnhancerSettings.instance;
            var data = ProjectFolderData.instance;

            if (settings == null || data == null) return;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return;

            bool isFolder = AssetDatabase.IsValidFolder(path);
            bool isList = selectionRect.height <= 20;

            if (!isFolder) return;

            var config = data.GetConfig(guid);
            string customIconName = config != null ? config.iconName : null;
            
            // Automatic Icons
            if (settings.enableAutomaticIcons && string.IsNullOrEmpty(customIconName))
            {
                FolderContentCache.UpdateCacheForFolder(guid, path);
                var contents = FolderContentCache.GetFolderContents(guid);
                if (contents != null && contents.Count == 1)
                {
                    var type = contents.First();
                    if (type == typeof(UnityEditor.MonoScript)) customIconName = "cs Script Icon";
                    else if (type == typeof(Material)) customIconName = "Material Icon";
                    else if (type == typeof(GameObject)) customIconName = "Prefab Icon";
                    else if (type == typeof(Texture2D)) customIconName = "Texture Icon";
                    else if (type == typeof(AudioClip)) customIconName = "AudioClip Icon";
                    else if (type == typeof(AnimationClip)) customIconName = "AnimationClip Icon";
                    else if (type == typeof(SceneAsset)) customIconName = "SceneAsset Icon";
                }
            }

            bool hasCustomColor = settings.enableCustomColors && config != null && config.color != Color.clear;
            bool hasCustomIcon = settings.enableCustomIcons && !string.IsNullOrEmpty(customIconName);

            if (hasCustomColor || hasCustomIcon || settings.enableMinimalMode)
            {
                Rect iconRect;
                if (isList)
                {
                    iconRect = new Rect(selectionRect.x, selectionRect.y, selectionRect.height, selectionRect.height);
                    iconRect.width = 16;
                    iconRect.height = 16;
                    iconRect.y += (selectionRect.height - 16) / 2;
                }
                else
                {
                    float iconSize = Mathf.Min(selectionRect.width, selectionRect.height);
                    iconRect = new Rect(selectionRect.x + (selectionRect.width - iconSize) / 2, selectionRect.y, iconSize, iconSize);
                }
                
                Color editorBgColor = EditorGUIUtility.isProSkin ? new Color32(56, 56, 56, 255) : new Color32(194, 194, 194, 255);
                
                if (Selection.Contains(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path)))
                {
                    editorBgColor = new Color32(44, 93, 135, 255);
                }

                // Hide default icon
                if (isList)
                {
                    EditorGUI.DrawRect(new Rect(iconRect.x, iconRect.y, 16, 16), editorBgColor);
                }
                else
                {
                    EditorGUI.DrawRect(iconRect, editorBgColor);
                }

                if (settings.enableMinimalMode && isList)
                {
                    // Draw tiny dot to indicate it's a folder
                    Color dotColor = hasCustomColor ? config.color : new Color(0.5f, 0.5f, 0.5f, 0.5f);
                    Rect dotRect = new Rect(iconRect.x + 6, iconRect.y + 6, 4, 4);
                    EditorGUI.DrawRect(dotRect, dotColor);
                }
                else
                {
                    // Draw folder icon tinted with custom color
                    Texture2D baseFolderIcon = EditorGUIUtility.IconContent("Folder Icon").image as Texture2D;
                    if (baseFolderIcon != null)
                    {
                        Color prevColor = GUI.color;
                        if (hasCustomColor) GUI.color = config.color;
                        GUI.DrawTexture(iconRect, baseFolderIcon, ScaleMode.ScaleToFit);
                        GUI.color = prevColor;
                    }
                    
                    // Draw small overlay icon on bottom-left
                    if (hasCustomIcon)
                    {
                        Texture2D overlayIcon = EditorGUIUtility.IconContent(customIconName)?.image as Texture2D;
                        if (overlayIcon != null)
                        {
                            Rect overlayRect;
                            if (isList)
                            {
                                overlayRect = new Rect(iconRect.x - 2, iconRect.y + 6, 10, 10);
                            }
                            else
                            {
                                float overlaySize = iconRect.width * 0.5f;
                                overlayRect = new Rect(iconRect.x, iconRect.y + iconRect.height - overlaySize, overlaySize, overlaySize);
                            }
                            GUI.DrawTexture(overlayRect, overlayIcon, ScaleMode.ScaleToFit);
                        }
                    }
                }
            }
        }
    }
}
