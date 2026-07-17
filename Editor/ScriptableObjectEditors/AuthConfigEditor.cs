using Kaddumi.UnityTools.Auth.Data;
using UnityEditor;
using UnityEngine;

namespace Kaddumi.UnityTools.EditorTools.ScriptableObjectEditors
{
    [CustomEditor(typeof(AuthConfig))]
    public class AuthConfigEditor : Editor
    {
        private SerializedProperty _autoSignInGuest;
        private SerializedProperty _googleWebClientId;
        private SerializedProperty _facebookAppId;
        private SerializedProperty _appleServiceId;

        private void OnEnable()
        {
            _autoSignInGuest = serializedObject.FindProperty(nameof(AuthConfig.AutoSignInGuest));
            _googleWebClientId = serializedObject.FindProperty(nameof(AuthConfig.GoogleWebClientId));
            _facebookAppId = serializedObject.FindProperty(nameof(AuthConfig.FacebookAppId));
            _appleServiceId = serializedObject.FindProperty(nameof(AuthConfig.AppleServiceId));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SOEditorKit.Title("Auth Config", "d_UnityEditor.ConsoleWindow");

            using (SOEditorKit.Box())
            {
                SOEditorKit.SectionHeader("Startup");
                EditorGUILayout.PropertyField(_autoSignInGuest, new GUIContent("Auto Sign-In Guest"));
            }
            EditorGUILayout.Space(6);

            DrawProvider("Google", _googleWebClientId, "Web Client ID", new Color(0.92f, 0.45f, 0.35f));
            DrawProvider("Facebook", _facebookAppId, "App ID", new Color(0.35f, 0.45f, 0.85f));
            DrawProvider("Apple", _appleServiceId, "Service ID", new Color(0.75f, 0.75f, 0.75f));

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawProvider(string name, SerializedProperty prop, string fieldLabel, Color tint)
        {
            using (SOEditorKit.Box())
            {
                var prevColor = GUI.color;
                GUILayout.BeginHorizontal();
                GUI.color = tint;
                GUILayout.Label("●", GUILayout.Width(14));
                GUI.color = prevColor;
                EditorGUILayout.LabelField(name, EditorStyles.boldLabel);
                GUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(prop, new GUIContent(fieldLabel));

                if (string.IsNullOrEmpty(prop.stringValue))
                    EditorGUILayout.HelpBox($"{name} sign-in will fail without a {fieldLabel}.", MessageType.Info);
            }
            EditorGUILayout.Space(6);
        }
    }
}
