using Kaddumi.UnityTools.ToolManager.Editor;
using UnityEditor;
using UnityEngine;

namespace Kaddumi.UnityTools.ProjectIcons.Editor
{
    /// <summary>
    /// In the Project window's one-column (list) layout, draws a Prefab's actual preview
    /// over the generic prefab icon.
    ///
    /// Registered as an <see cref="IEditorToolModule"/> so it can be switched on/off from the
    /// Tool Manager window (Tools > Unity Tools > Tool Manager). The registry owns its lifecycle.
    /// </summary>
    public sealed class PrefabPreviewToolModule : IEditorToolModule
    {
        public string Id => "PrefabPreviewProjectIcons";
        public string DisplayName => "Prefab Icon Preview";
        public string Description =>
            "Draws the Prefab's actual preview over the generic icon in the Project window's list view.";
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

            // Ensure this GameObject is actually a prefab asset, not a model or something else
            PrefabAssetType assetType = PrefabUtility.GetPrefabAssetType(go);
            if (assetType != PrefabAssetType.Regular && assetType != PrefabAssetType.Variant)
                return;

            // Check if the prefab or any of its children have a model (MeshRenderer or SkinnedMeshRenderer)
            bool hasModel = false;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r is MeshRenderer || r is SkinnedMeshRenderer)
                {
                    hasModel = true;
                    break;
                }
            }

            // If there's no model, we just let Unity show the default prefab icon.
            if (!hasModel)
                return;

            Texture2D preview = AssetPreview.GetAssetPreview(go);
            if (preview == null)
            {
                // Previews are generated asynchronously; nudge a repaint until this one is ready.
                if (AssetPreview.IsLoadingAssetPreview(go.GetInstanceID()))
                    EditorApplication.RepaintProjectWindow();
                return;
            }

            float size = rect.height;
            bool isSelected = IsSelected(instanceId);
            
            // If the user is renaming this prefab, Unity spawns a TextField.
            // We shouldn't paint over the row background in that case, otherwise we hide the TextField.
            bool isRenaming = EditorGUIUtility.editingTextField && isSelected;

            var previewRect = new Rect(rect.x + size, rect.y, size, size);

            if (!isRenaming)
            {
                // Draw background over the rest of the row to hide the original text
                var bgRect = new Rect(rect.x + size, rect.y, rect.width - size, rect.height);
                EditorGUI.DrawRect(bgRect, GetRowBackgroundColor(isSelected));
            }
            else
            {
                // If renaming, just draw background behind the preview icon so it's readable
                EditorGUI.DrawRect(previewRect, GetRowBackgroundColor(isSelected));
            }

            // Draw our preview
            GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);

            if (!isRenaming)
            {
                // Redraw the text shifted to the right
                var textRect = new Rect(rect.x + size * 2 + 2f, rect.y, rect.width - size * 2, rect.height);
                
                GUIStyle labelStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft
                };
                
                if (isSelected)
                {
                    labelStyle.normal.textColor = Color.white;
                }
                
                GUI.Label(textRect, go.name, labelStyle);
            }
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
