using Kaddumi.UnityTools.Save.Data;
using UnityEditor;
using UnityEngine;

namespace Kaddumi.UnityTools.EditorTools.ScriptableObjectEditors
{
    [CustomEditor(typeof(SaveConfig))]
    public class SaveConfigEditor : Editor
    {
        private SerializedProperty _saveVersion, _prettyPrint;
        private SerializedProperty _slotCount, _defaultSlot;
        private SerializedProperty _autoSave, _autoSaveInterval;
        private SerializedProperty _saveOnQuit, _saveOnPause;
        private SerializedProperty _trackPlaytime;

        private void OnEnable()
        {
            _saveVersion = serializedObject.FindProperty(nameof(SaveConfig.SaveVersion));
            _prettyPrint = serializedObject.FindProperty(nameof(SaveConfig.PrettyPrint));
            _slotCount = serializedObject.FindProperty(nameof(SaveConfig.SlotCount));
            _defaultSlot = serializedObject.FindProperty(nameof(SaveConfig.DefaultSlot));
            _autoSave = serializedObject.FindProperty(nameof(SaveConfig.AutoSave));
            _autoSaveInterval = serializedObject.FindProperty(nameof(SaveConfig.AutoSaveIntervalSeconds));
            _saveOnQuit = serializedObject.FindProperty(nameof(SaveConfig.SaveOnQuit));
            _saveOnPause = serializedObject.FindProperty(nameof(SaveConfig.SaveOnPause));
            _trackPlaytime = serializedObject.FindProperty(nameof(SaveConfig.TrackPlaytime));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SOEditorKit.Title("Save Config", "SaveActive");

            using (SOEditorKit.Box())
            {
                SOEditorKit.SectionHeader("Format", "d_SaveAs");
                EditorGUILayout.PropertyField(_saveVersion);
                EditorGUILayout.PropertyField(_prettyPrint);
            }
            EditorGUILayout.Space(6);

            using (SOEditorKit.Box())
            {
                SOEditorKit.SectionHeader("Slots", "d_FolderOpened Icon");
                EditorGUILayout.PropertyField(_slotCount);
                _defaultSlot.intValue = EditorGUILayout.IntSlider("Default Slot", _defaultSlot.intValue, 0, Mathf.Max(0, _slotCount.intValue - 1));
                if (_defaultSlot.intValue >= _slotCount.intValue)
                    EditorGUILayout.HelpBox("Default Slot is outside the valid range for Slot Count.", MessageType.Error);
            }
            EditorGUILayout.Space(6);

            using (SOEditorKit.Box())
            {
                SOEditorKit.SectionHeader("Auto Save", "d_UnityEditor.AnimationWindow");
                EditorGUILayout.PropertyField(_autoSave);
                using (new EditorGUI.DisabledScope(!_autoSave.boolValue))
                    EditorGUILayout.PropertyField(_autoSaveInterval, new GUIContent("Interval (s)"));
            }
            EditorGUILayout.Space(6);

            using (SOEditorKit.Box())
            {
                SOEditorKit.SectionHeader("Lifecycle Saves", "d_PlayButton");
                EditorGUILayout.PropertyField(_saveOnQuit);
                EditorGUILayout.PropertyField(_saveOnPause);
                EditorGUILayout.PropertyField(_trackPlaytime);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
