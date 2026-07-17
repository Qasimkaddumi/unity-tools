using Kaddumi.UnityTools.LoadingSystem.Core;
using UnityEditor;
using UnityEngine;

namespace Kaddumi.UnityTools.EditorTools.ScriptableObjectEditors
{
    [CustomEditor(typeof(SceneDefinition))]
    [CanEditMultipleObjects]
    public class SceneDefinitionEditor : Editor
    {
        private SerializedProperty _sceneAsset;
        private SerializedProperty _scenePath;
        private SerializedProperty _sceneID;

        protected virtual void OnEnable()
        {
            _sceneAsset = serializedObject.FindProperty("sceneAsset");
            _scenePath = serializedObject.FindProperty("scenePath");
            _sceneID = serializedObject.FindProperty("sceneID");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var so = (SceneDefinition)target;

            SOEditorKit.Title("Scene Definition", "d_SceneAsset Icon");

            using (SOEditorKit.Box())
            {
                EditorGUILayout.PropertyField(_sceneAsset, new GUIContent("Scene Asset"));

                bool isValid = so.IsValid();
                var statusIcon = EditorGUIUtility.IconContent(isValid ? "d_Toggle Icon" : "d_console.warnicon.sml");
                GUILayout.BeginHorizontal();
                GUILayout.Label(statusIcon.image, GUILayout.Width(16), GUILayout.Height(16));
                GUILayout.Label(isValid ? "Valid" : "Missing scene reference", isValid ? EditorStyles.label : EditorStyles.boldLabel);
                GUILayout.EndHorizontal();

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(_scenePath, new GUIContent("Scene Path"));
                    EditorGUILayout.PropertyField(_sceneID, new GUIContent("Scene ID"));
                }
            }

            DrawExtraFields();

            serializedObject.ApplyModifiedProperties();
        }

        protected virtual void DrawExtraFields() { }
    }
}
