using Kaddumi.UnityTools.ToolManager.Editor;
using UnityEditor;
using UnityEngine;

namespace Kaddumi.UnityTools.ProjectIcons.Editor
{
    /// <summary>
    /// In the Project window's one-column (list) layout, draws a 3D model's actual preview
    /// over the generic model icon.
    ///
    /// Registered as an <see cref="IEditorToolModule"/> so it can be switched on/off from the
    /// Tool Manager window (Tools > Unity Tools > Tool Manager). The registry owns its lifecycle.
    /// </summary>
    public sealed class ModelPreviewToolModule : IEditorToolModule
    {
        public string Id => "ModelProjectIcons";
        public string DisplayName => "Model Icon Preview";
        public string Description =>
            "Draws the 3D model's preview over the generic icon in the Project window's list view.";
        public string Category => "Project Window";
        public bool DefaultEnabled => true;

        // One-column rows are a single ~16px line; the grid layout uses much taller rects. This is
        // how we restrict the override to the list view only.
        private const float MaxListRowHeight = 20f;

        public void OnActivated()
        {
            EditorApplication.projectWindowItemInstanceOnGUI -= OnProjectWindowItem;
            EditorApplication.projectWindowItemInstanceOnGUI += OnProjectWindowItem;
            EditorApplication.RepaintProjectWindow();
        }

        public void OnDeactivated()
        {
            EditorApplication.projectWindowItemInstanceOnGUI -= OnProjectWindowItem;
            EditorApplication.RepaintProjectWindow();
        }

        private static void OnProjectWindowItem(int instanceId, Rect rect)
        {
            // Grid / two-column layout draws its own previews already.
            if (rect.height > MaxListRowHeight)
                return;

            var go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (go == null)
                return;

            // Ensure this GameObject is actually a model asset (not a prefab, etc.)
            string path = AssetDatabase.GetAssetPath(go);
            if (string.IsNullOrEmpty(path))
                return;

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                return;

            Texture2D preview = AssetPreview.GetAssetPreview(go);
            if (preview == null)
            {
                // Previews are generated asynchronously; nudge a repaint until this one is ready.
                if (AssetPreview.IsLoadingAssetPreview(go.GetInstanceID()))
                    EditorApplication.RepaintProjectWindow();
                return;
            }

            // The built-in icon is a square at the left edge of the row, sized to the row height.
            float size = rect.height;
            var iconRect = new Rect(rect.x, rect.y, size, size);

            // Paint over Unity's generic icon first. Match the row background (selection included) so it blends in.
            EditorGUI.DrawRect(iconRect, GetRowBackgroundColor(IsSelected(instanceId)));

            // ScaleToFit preserves the aspect ratio.
            GUI.DrawTexture(iconRect, preview, ScaleMode.ScaleToFit);
        }

        private static bool IsSelected(int instanceId)
        {
            int[] ids = Selection.instanceIDs;
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == instanceId)
                    return true;
            }
            return false;
        }

        private static Color GetRowBackgroundColor(bool selected)
        {
            if (selected)
            {
                return EditorGUIUtility.isProSkin
                    ? new Color(0.172f, 0.364f, 0.529f)
                    : new Color(0.227f, 0.447f, 0.690f);
            }

            return EditorGUIUtility.isProSkin
                ? new Color(0.219f, 0.219f, 0.219f)
                : new Color(0.760f, 0.760f, 0.760f);
        }
    }
}
