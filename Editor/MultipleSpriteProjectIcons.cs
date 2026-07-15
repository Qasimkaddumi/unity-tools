using UnityEditor;
using UnityEngine;

namespace KaleemEditor
{
    /// <summary>
    /// In the Project window's one-column (list) layout, draws each individual sprite's own sliced
    /// preview over the generic icon. This targets the Sprite sub-assets you see when you expand a
    /// texture whose Sprite Mode is "Multiple" (as well as any standalone Sprite row), so you see the
    /// actual items rather than a generic icon or the whole packed atlas.
    ///
    /// The grid / two-column layout already renders full previews, so it is left untouched.
    /// </summary>
    [InitializeOnLoad]
    public static class MultipleSpriteProjectIcons
    {
        private const string MenuPath = "Tools/Project Icons/Preview Individual Sprites";
        private const string PrefKey = "Kaleem.MultipleSpriteProjectIcons.Enabled";

        // One-column rows are a single ~16px line; the grid layout uses much taller rects. This is
        // how we restrict the override to the list view only.
        private const float MaxListRowHeight = 20f;

        private static bool _enabled;

        static MultipleSpriteProjectIcons()
        {
            _enabled = EditorPrefs.GetBool(PrefKey, true);
            if (_enabled)
                Enable();
        }

        private static void Enable()
        {
            EditorApplication.projectWindowItemInstanceOnGUI -= OnProjectWindowItem;
            EditorApplication.projectWindowItemInstanceOnGUI += OnProjectWindowItem;
            HoverPreviewController.SetActive(true);
        }

        private static void Disable()
        {
            EditorApplication.projectWindowItemInstanceOnGUI -= OnProjectWindowItem;
            HoverPreviewController.SetActive(false);
        }

        private static void OnProjectWindowItem(int instanceId, Rect rect)
        {
            // Grid / two-column layout draws its own previews already.
            if (rect.height > MaxListRowHeight)
                return;

            if (EditorUtility.InstanceIDToObject(instanceId) is not Sprite sprite)
                return;

            // Report hover so the floating enlarged preview can follow the cursor. Anchored to the
            // right edge of the row, vertically centred on it.
            if (Event.current.type == EventType.Repaint && rect.Contains(Event.current.mousePosition))
            {
                Vector2 anchor = GUIUtility.GUIToScreenPoint(new Vector2(rect.xMax, rect.center.y));
                HoverPreviewController.ReportHover(instanceId, sprite, anchor);
            }

            Texture2D preview = AssetPreview.GetAssetPreview(sprite);
            if (preview == null)
            {
                // Previews are generated asynchronously; nudge a repaint until this one is ready.
                if (AssetPreview.IsLoadingAssetPreview(sprite.GetInstanceID()))
                    EditorApplication.RepaintProjectWindow();
                return;
            }

            // The built-in icon is a square at the left edge of the row, sized to the row height.
            float size = rect.height;
            var iconRect = new Rect(rect.x, rect.y, size, size);

            // Paint over Unity's generic icon first so transparent / small sprites don't show a
            // confusing double image. Match the row background (selection included) so it blends in.
            EditorGUI.DrawRect(iconRect, GetRowBackgroundColor(IsSelected(instanceId)));

            // ScaleToFit preserves the sprite's aspect ratio (it letterboxes; it never stretches).
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

        [MenuItem(MenuPath)]
        private static void ToggleEnabled()
        {
            _enabled = !_enabled;
            EditorPrefs.SetBool(PrefKey, _enabled);
            if (_enabled)
                Enable();
            else
                Disable();
            EditorApplication.RepaintProjectWindow();
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool ToggleEnabledValidate()
        {
            Menu.SetChecked(MenuPath, _enabled);
            return true;
        }
    }

    /// <summary>
    /// Drives a floating, borderless preview window that follows the cursor while it hovers a sprite
    /// row in the Project window's list layout. A borderless popup is used (instead of drawing in the
    /// row callback) so the enlarged preview is not clipped by neighbouring rows.
    /// </summary>
    internal static class HoverPreviewController
    {
        // Hover must persist across a short delay before the popup appears, and the popup closes once
        // no row has reported hover for this long (the row callback keeps refreshing it).
        private const double ShowDelay = 0.35;
        private const double StaleThreshold = 0.15;

        private static bool _active;
        private static int _hoverInstanceId;
        private static Sprite _hoverSprite;
        private static Vector2 _hoverAnchor;
        private static double _hoverStart;
        private static double _lastSeen;
        private static HoverPreviewPopup _popup;

        public static void SetActive(bool active)
        {
            if (_active == active)
                return;

            _active = active;
            EditorApplication.update -= OnUpdate;
            if (active)
                EditorApplication.update += OnUpdate;
            else
                ClearHover();
        }

        public static void ReportHover(int instanceId, Sprite sprite, Vector2 anchorScreen)
        {
            if (!_active)
                return;

            if (instanceId != _hoverInstanceId)
            {
                _hoverInstanceId = instanceId;
                _hoverSprite = sprite;
                _hoverStart = EditorApplication.timeSinceStartup;
            }

            _hoverAnchor = anchorScreen;
            _lastSeen = EditorApplication.timeSinceStartup;
        }

        private static void OnUpdate()
        {
            if (_hoverInstanceId == 0)
                return;

            double now = EditorApplication.timeSinceStartup;

            // No row has confirmed the hover recently -> the cursor moved away.
            if (now - _lastSeen > StaleThreshold)
            {
                ClearHover();
                return;
            }

            // Keep the Project window repainting so hover confirmations keep arriving while the cursor
            // sits still over a row.
            EditorApplication.RepaintProjectWindow();

            if (_hoverSprite != null && now - _hoverStart >= ShowDelay)
                ShowOrUpdatePopup();
        }

        private static void ShowOrUpdatePopup()
        {
            bool isNew = _popup == null;
            if (isNew)
                _popup = ScriptableObject.CreateInstance<HoverPreviewPopup>();

            _popup.SetTarget(_hoverSprite, _hoverAnchor);

            if (isNew)
                _popup.ShowPopup();
        }

        private static void ClearHover()
        {
            _hoverInstanceId = 0;
            _hoverSprite = null;
            if (_popup != null)
            {
                _popup.Close();
                _popup = null;
            }
        }
    }

    /// <summary>
    /// Borderless popup window that renders an enlarged, aspect-correct preview of a single sprite
    /// plus its name and pixel dimensions.
    /// </summary>
    internal sealed class HoverPreviewPopup : EditorWindow
    {
        private const float PreviewSize = 128f;
        private const float Padding = 6f;
        private const float LabelHeight = 34f;

        private Sprite _sprite;

        public void SetTarget(Sprite sprite, Vector2 anchorScreen)
        {
            _sprite = sprite;

            float width = PreviewSize + Padding * 2f;
            float height = PreviewSize + LabelHeight + Padding * 2f;

            // Sit to the right of the row, vertically centred on the cursor anchor.
            position = new Rect(anchorScreen.x + 6f, anchorScreen.y - height * 0.5f, width, height);
            Repaint();
        }

        private void OnGUI()
        {
            if (_sprite == null)
                return;

            var bg = EditorGUIUtility.isProSkin
                ? new Color(0.16f, 0.16f, 0.16f)
                : new Color(0.78f, 0.78f, 0.78f);
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), bg);

            var previewRect = new Rect(Padding, Padding, PreviewSize, PreviewSize);
            Texture2D preview = AssetPreview.GetAssetPreview(_sprite);
            if (preview != null)
                GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
            else
                EditorGUI.DrawRect(previewRect, new Color(0f, 0f, 0f, 0.15f));

            var labelRect = new Rect(Padding, PreviewSize + Padding, PreviewSize, LabelHeight);
            var nameStyle = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true, fontSize = 11 };
            EditorGUI.LabelField(labelRect, _sprite.name, nameStyle);

            var sizeRect = new Rect(Padding, PreviewSize + Padding + 16f, PreviewSize, 16f);
            int w = Mathf.RoundToInt(_sprite.rect.width);
            int h = Mathf.RoundToInt(_sprite.rect.height);
            EditorGUI.LabelField(sizeRect, $"{w} x {h} px", EditorStyles.miniLabel);
        }
    }
}
