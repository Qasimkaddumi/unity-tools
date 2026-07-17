using Kaddumi.UnityTools.LoadingSystem.Core;
using UnityEditor;
using UnityEngine;

namespace Kaddumi.UnityTools.EditorTools.ScriptableObjectEditors
{
    [CustomEditor(typeof(LevelDefinition))]
    [CanEditMultipleObjects]
    public class LevelDefinitionEditor : SceneDefinitionEditor
    {
        private SerializedProperty _levelID;
        private SerializedProperty _nextLevel;

        protected override void OnEnable()
        {
            base.OnEnable();
            _levelID = serializedObject.FindProperty("levelID");
            _nextLevel = serializedObject.FindProperty("nextLevel");
        }

        protected override void DrawExtraFields()
        {
            EditorGUILayout.Space(6);
            using (SOEditorKit.Box())
            {
                SOEditorKit.SectionHeader("Level", "d_UnityEditor.HierarchyWindow");
                EditorGUILayout.PropertyField(_levelID, new GUIContent("Level ID"));
                EditorGUILayout.PropertyField(_nextLevel, new GUIContent("Next Level"));

                if (_nextLevel.objectReferenceValue == target)
                    EditorGUILayout.HelpBox("Next Level points to itself.", MessageType.Warning);
            }
        }
    }
}
