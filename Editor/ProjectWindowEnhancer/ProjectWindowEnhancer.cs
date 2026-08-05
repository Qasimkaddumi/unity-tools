using UnityEditor;
using UnityEngine;
using System.IO;

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

            // Content Minimap
            if (settings.enableContentMinimap && isList)
            {
                FolderContentCache.UpdateCacheForFolder(guid, path);
                var contents = FolderContentCache.GetFolderContents(guid);
                if (contents != null && contents.Count > 0)
                {
                    float xOffset = selectionRect.xMax - 18;
                    foreach (var type in contents)
                    {
                        GUIContent typeIcon = EditorGUIUtility.ObjectContent(null, type);
                        if (typeIcon != null && typeIcon.image != null)
                        {
                            Rect miniIconRect = new Rect(xOffset, selectionRect.y + (selectionRect.height - 14) / 2, 14, 14);
                            GUI.DrawTexture(miniIconRect, typeIcon.image);
                            xOffset -= 16;
                        }
                    }
                }
            }

            var config = data.GetConfig(guid);
            if (config != null)
            {
                // Custom color
                if (settings.enableCustomColors && config.color != Color.clear)
                {
                    // Draw with semi-transparency so we don't completely hide the text
                    Color bgColor = config.color;
                    bgColor.a = 0.3f; // Force transparency for background tint
                    
                    Rect bgRect = selectionRect;
                    if (isList)
                    {
                        bgRect.x = 0;
                        bgRect.width = 10000;
                    }
                    EditorGUI.DrawRect(bgRect, bgColor);
                }

                // Custom icon
                if (settings.enableCustomIcons && !string.IsNullOrEmpty(config.iconName))
                {
                    Texture2D icon = EditorGUIUtility.IconContent(config.iconName)?.image as Texture2D;
                    if (icon != null)
                    {
                        Rect iconRect;
                        if (isList)
                        {
                            // In list view, selectionRect.x is the start of the text/icon area
                            iconRect = new Rect(selectionRect.x, selectionRect.y, selectionRect.height, selectionRect.height);
                            // Adjust based on Unity's default icon position
                            iconRect.width = 16;
                            iconRect.height = 16;
                            iconRect.y += (selectionRect.height - 16) / 2;
                        }
                        else
                        {
                            // Grid view
                            float iconSize = Mathf.Min(selectionRect.width, selectionRect.height);
                            iconRect = new Rect(selectionRect.x + (selectionRect.width - iconSize) / 2, selectionRect.y, iconSize, iconSize);
                        }
                        
                        // We need to draw a background patch to cover the default folder icon
                        // For a clean look, we use the default editor background color
                        Color editorBgColor = EditorGUIUtility.isProSkin ? new Color32(56, 56, 56, 255) : new Color32(194, 194, 194, 255);
                        
                        // If it's selected, the background is the selection color
                        if (Selection.Contains(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path)))
                        {
                            editorBgColor = new Color32(44, 93, 135, 255); // Approximate active selection color
                        }

                        // Apply our tint if applicable
                        if (settings.enableCustomColors && config.color != Color.clear)
                        {
                            Color tint = config.color;
                            tint.a = 0.3f;
                            editorBgColor = Color.Lerp(editorBgColor, tint, tint.a);
                        }

                        // Draw patch
                        if (isList)
                        {
                             EditorGUI.DrawRect(new Rect(iconRect.x, iconRect.y, 16, 16), editorBgColor);
                        }
                        else
                        {
                             EditorGUI.DrawRect(iconRect, editorBgColor);
                        }
                       
                        // Draw the custom icon
                        GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                    }
                }
            }
        }
    }
}
