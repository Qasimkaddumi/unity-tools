using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Kaddumi.UnityTools.Consent.UI
{
    /// <summary>
    /// A self-contained privacy consent dialog. It builds its own screen-space Canvas, a dimmed
    /// backdrop and Accept / Decline buttons entirely in code, so it works out of the box with
    /// no prefab wiring or scene setup. Used by the Manual (non-Google) consent provider.
    ///
    /// Present it with <see cref="Present(ConsentDialogSettings, Action{bool})"/>; the callback
    /// receives <c>true</c> when the user accepts and <c>false</c> when they decline.
    /// </summary>
    public class ConsentDialog : MonoBehaviour
    {
        private Action<bool> _onResult;
        private bool _resolved;

        /// <summary>
        /// Spawns the dialog on a persistent GameObject and shows it immediately.
        /// </summary>
        public static ConsentDialog Present(ConsentDialogSettings settings, Action<bool> onResult)
        {
            var host = new GameObject("[ConsentDialog]");
            DontDestroyOnLoad(host);
            var dialog = host.AddComponent<ConsentDialog>();
            dialog.Build(settings ?? new ConsentDialogSettings(), onResult);
            return dialog;
        }

        private void Build(ConsentDialogSettings settings, Action<bool> onResult)
        {
            _onResult = onResult;

            EnsureEventSystem();
            Font font = LoadDefaultFont();

            // ---- Canvas (always on top) ----
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // ---- Dimmed, click-blocking backdrop ----
            var backdrop = CreateChild("Backdrop", transform);
            Stretch(backdrop.rectTransform);
            backdrop.color = new Color(0f, 0f, 0f, 0.75f);

            // ---- Centered panel ----
            var panel = CreateChild("Panel", backdrop.transform);
            panel.color = new Color(0.13f, 0.14f, 0.16f, 1f);
            var panelRt = panel.rectTransform;
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(900, 900);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(60, 60, 60, 60);
            layout.spacing = 40;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childAlignment = TextAnchor.UpperCenter;
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ---- Title ----
            CreateText("Title", panel.transform, settings.Title, font, 54, FontStyle.Bold,
                TextAnchor.MiddleCenter, Color.white);

            // ---- Message ----
            CreateText("Message", panel.transform, settings.Message, font, 36, FontStyle.Normal,
                TextAnchor.UpperLeft, new Color(0.85f, 0.85f, 0.85f, 1f));

            // ---- Optional privacy-policy link ----
            if (!string.IsNullOrEmpty(settings.PrivacyPolicyUrl))
            {
                string url = settings.PrivacyPolicyUrl;
                CreateButton("PrivacyLink", panel.transform, settings.PrivacyPolicyLabel, font,
                    new Color(0f, 0f, 0f, 0f), new Color(0.4f, 0.7f, 1f, 1f),
                    () => Application.OpenURL(url));
            }

            // ---- Action buttons ----
            CreateButton("Accept", panel.transform, settings.AcceptLabel, font,
                new Color(0.20f, 0.55f, 0.95f, 1f), Color.white, () => Resolve(true));
            CreateButton("Decline", panel.transform, settings.DeclineLabel, font,
                new Color(0.30f, 0.31f, 0.34f, 1f), Color.white, () => Resolve(false));
        }

        private void Resolve(bool accepted)
        {
            if (_resolved) return;
            _resolved = true;

            var callback = _onResult;
            _onResult = null;
            callback?.Invoke(accepted);

            Destroy(gameObject);
        }

        // ---------- UI construction helpers ----------

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("[EventSystem]", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        private static Font LoadDefaultFont()
        {
            // Unity 2022+/6 ships "LegacyRuntime.ttf"; older versions used "Arial.ttf".
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }

        private static Image CreateChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Image>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Text CreateText(string name, Transform parent, string content, Font font,
            int fontSize, FontStyle style, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            return text;
        }

        private static void CreateButton(string name, Transform parent, string label, Font font,
            Color background, Color textColor, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = background;

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 110;
            le.preferredHeight = 110;

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() => onClick());

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = font;
            text.text = label;
            text.fontSize = 40;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = textColor;
            Stretch(text.rectTransform);
        }

        private void OnDestroy()
        {
            // If the dialog is torn down without an explicit choice (e.g. scene teardown),
            // make sure a pending caller is not left waiting forever — treat it as a decline.
            if (!_resolved)
            {
                _resolved = true;
                var callback = _onResult;
                _onResult = null;
                callback?.Invoke(false);
            }
        }
    }
}
