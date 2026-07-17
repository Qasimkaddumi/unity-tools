using Kaddumi.UnityTools.Api.Config;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Kaddumi.UnityTools.EditorTools.ScriptableObjectEditors
{
    [CustomEditor(typeof(ApiConfigSO))]
    public class ApiConfigSOEditor : Editor
    {
        private SerializedProperty _baseUrl, _timeout;
        private SerializedProperty _authHeaderName, _authScheme;
        private SerializedProperty _defaultHeaders;
        private ReorderableList _headerList;

        private void OnEnable()
        {
            _baseUrl = serializedObject.FindProperty("baseUrl");
            _timeout = serializedObject.FindProperty("defaultTimeoutSeconds");
            _authHeaderName = serializedObject.FindProperty("authHeaderName");
            _authScheme = serializedObject.FindProperty("authScheme");
            _defaultHeaders = serializedObject.FindProperty("defaultHeaders");

            _headerList = new ReorderableList(serializedObject, _defaultHeaders, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Default Headers"),
                elementHeightCallback = _ => EditorGUIUtility.singleLineHeight + 4,
                drawElementCallback = (rect, index, active, focused) =>
                {
                    var element = _defaultHeaders.GetArrayElementAtIndex(index);
                    var nameProp = element.FindPropertyRelative("name");
                    var valueProp = element.FindPropertyRelative("value");

                    rect.y += 2;
                    rect.height = EditorGUIUtility.singleLineHeight;
                    float half = rect.width * 0.42f;

                    EditorGUI.PropertyField(new Rect(rect.x, rect.y, half, rect.height), nameProp, GUIContent.none);
                    EditorGUI.LabelField(new Rect(rect.x + half + 4, rect.y, 12, rect.height), ":");
                    EditorGUI.PropertyField(new Rect(rect.x + half + 18, rect.y, rect.width - half - 18, rect.height), valueProp, GUIContent.none);
                }
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SOEditorKit.Title("Api Config", "d_BuildSettings.Web.Small");

            using (SOEditorKit.Box())
            {
                SOEditorKit.SectionHeader("Endpoint", "d_UnityEditor.WebView");
                EditorGUILayout.PropertyField(_baseUrl, new GUIContent("Base URL"));
                if (string.IsNullOrEmpty(_baseUrl.stringValue))
                    EditorGUILayout.HelpBox("Base URL is empty — requests will fail to resolve.", MessageType.Warning);
                EditorGUILayout.PropertyField(_timeout, new GUIContent("Default Timeout (s)"));
            }
            EditorGUILayout.Space(6);

            using (SOEditorKit.Box())
            {
                SOEditorKit.SectionHeader("Auth", "d_LockIcon");
                EditorGUILayout.PropertyField(_authHeaderName, new GUIContent("Header Name"));
                EditorGUILayout.PropertyField(_authScheme, new GUIContent("Scheme Prefix"));
            }
            EditorGUILayout.Space(6);

            using (SOEditorKit.Box())
            {
                _headerList.DoLayoutList();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
