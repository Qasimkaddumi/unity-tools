using UnityEditor;
using UnityEngine;

namespace Kaddumi.UnityTools.EditorTools.ScriptableObjectEditors
{
    /// <summary>
    /// Shared drawing helpers for the project's ScriptableObject custom editors —
    /// keeps section headers, boxed groups, and platform-icon rows consistent across configs.
    /// </summary>
    internal static class SOEditorKit
    {
        public static readonly Color AndroidTint = new Color(0.65f, 0.85f, 0.45f);
        public static readonly Color IOSTint = new Color(0.55f, 0.75f, 1f);

        public static void Title(string text, string iconName = null)
        {
            EditorGUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(iconName))
            {
                var icon = EditorGUIUtility.IconContent(iconName);
                if (icon?.image != null)
                    GUILayout.Label(icon.image, GUILayout.Width(20), GUILayout.Height(20));
            }
            GUILayout.Label(text, TitleStyle);
            GUILayout.EndHorizontal();
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.4f));
            EditorGUILayout.Space(2);
        }

        public static void SectionHeader(string text, string iconName = null)
        {
            GUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(iconName))
            {
                var icon = EditorGUIUtility.IconContent(iconName);
                if (icon?.image != null)
                    GUILayout.Label(icon.image, GUILayout.Width(16), GUILayout.Height(16));
            }
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
            GUILayout.EndHorizontal();
        }

        public static System.IDisposable Box() => new BoxScope();

        private sealed class BoxScope : System.IDisposable
        {
            public BoxScope() => EditorGUILayout.BeginVertical("box");
            public void Dispose() => EditorGUILayout.EndVertical();
        }

        private static GUIStyle _titleStyle;
        private static GUIStyle TitleStyle => _titleStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14
        };

        public static void PlatformField(SerializedProperty androidProp, SerializedProperty iosProp, string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            using (Box())
            {
                DrawTintedField(androidProp, "Android", AndroidTint);
                DrawTintedField(iosProp, "iOS", IOSTint);
            }
        }

        private static void DrawTintedField(SerializedProperty prop, string label, Color tint)
        {
            var prevColor = GUI.color;
            GUILayout.BeginHorizontal();
            GUI.color = tint;
            GUILayout.Label("●", GUILayout.Width(14));
            GUI.color = prevColor;
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
            GUILayout.EndHorizontal();
        }

        public static void MissingWarning(bool isMissing, string message)
        {
            if (isMissing)
                EditorGUILayout.HelpBox(message, MessageType.Warning);
        }
    }
}
