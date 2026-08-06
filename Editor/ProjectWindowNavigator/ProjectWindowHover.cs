using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

namespace Kaddumi.UnityTools.ProjectEnhancer
{
    public static class ProjectWindowHover
    {
        public static void Enable()
        {
            EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
        }

        public static void Disable()
        {
            EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            if (Event.current.type != EventType.Repaint) return;

            var data = ProjectBookmarkData.instance;

            if (data == null) return;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return;

            bool isFolder = AssetDatabase.IsValidFolder(path);
            bool isList = selectionRect.height <= 20;

            if (!isFolder) return;

            // Two-line names / Hover expansion for long names
            // Unity truncates long names natively. We can draw our own label if the mouse is hovering,
            // or just draw a word-wrapped label over the original.
            if (Event.current.type == EventType.Repaint)
            {
                string assetName = System.IO.Path.GetFileNameWithoutExtension(path);
                
                // Only do this if the name is likely truncated (rough heuristic: length > 12 chars in list view, or always in grid view if long)
                if (assetName.Length > 15)
                {
                    if (isList)
                    {
                        // In list view, if hovered, expand the text slightly over the next row with a background so it's readable
                        if (selectionRect.Contains(Event.current.mousePosition))
                        {
                            GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
                            float textWidth = labelStyle.CalcSize(new GUIContent(assetName)).x;
                            if (textWidth > selectionRect.width - 20) // If it's likely truncated
                            {
                                Rect expandedRect = new Rect(selectionRect.x + 18, selectionRect.y, textWidth + 4, selectionRect.height);
                                Color editorBgColor = EditorGUIUtility.isProSkin ? new Color32(56, 56, 56, 255) : new Color32(194, 194, 194, 255);
                                EditorGUI.DrawRect(expandedRect, editorBgColor);
                                GUI.Label(expandedRect, assetName, labelStyle);
                            }
                        }
                    }
                }
            }
        }
    }
}
