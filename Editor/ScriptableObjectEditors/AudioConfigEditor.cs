using Kaddumi.UnityTools.Audio.Data;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Kaddumi.UnityTools.EditorTools.ScriptableObjectEditors
{
    [CustomEditor(typeof(AudioConfig))]
    public class AudioConfigEditor : Editor
    {
        private SerializedProperty _mixer, _buses;
        private SerializedProperty _initialPoolSize, _maxPoolSize;
        private SerializedProperty _defaultMusicFadeSeconds;
        private SerializedProperty _duckBus, _duckTargetVolume, _duckAttackSeconds, _duckHoldSeconds, _duckReleaseSeconds;
        private SerializedProperty _minDecibels;
        private ReorderableList _busList;

        private void OnEnable()
        {
            _mixer = serializedObject.FindProperty(nameof(AudioConfig.Mixer));
            _buses = serializedObject.FindProperty(nameof(AudioConfig.Buses));
            _initialPoolSize = serializedObject.FindProperty(nameof(AudioConfig.InitialPoolSize));
            _maxPoolSize = serializedObject.FindProperty(nameof(AudioConfig.MaxPoolSize));
            _defaultMusicFadeSeconds = serializedObject.FindProperty(nameof(AudioConfig.DefaultMusicFadeSeconds));
            _duckBus = serializedObject.FindProperty(nameof(AudioConfig.DuckBus));
            _duckTargetVolume = serializedObject.FindProperty(nameof(AudioConfig.DuckTargetVolume));
            _duckAttackSeconds = serializedObject.FindProperty(nameof(AudioConfig.DuckAttackSeconds));
            _duckHoldSeconds = serializedObject.FindProperty(nameof(AudioConfig.DuckHoldSeconds));
            _duckReleaseSeconds = serializedObject.FindProperty(nameof(AudioConfig.DuckReleaseSeconds));
            _minDecibels = serializedObject.FindProperty(nameof(AudioConfig.MinDecibels));

            _busList = new ReorderableList(serializedObject, _buses, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Bus Bindings"),
                elementHeightCallback = _ => EditorGUIUtility.singleLineHeight * 4 + 16,
                drawElementCallback = DrawBusElement
            };
        }

        private void DrawBusElement(Rect rect, int index, bool active, bool focused)
        {
            var element = _buses.GetArrayElementAtIndex(index);
            var bus = element.FindPropertyRelative("Bus");
            var exposedParam = element.FindPropertyRelative("ExposedVolumeParam");
            var group = element.FindPropertyRelative("Group");
            var defaultVolume = element.FindPropertyRelative("DefaultVolume");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            rect.y += 2;

            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, lineHeight), bus);
            rect.y += lineHeight + 2;
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, lineHeight), exposedParam, new GUIContent("Exposed Param"));
            rect.y += lineHeight + 2;
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, lineHeight), group);
            rect.y += lineHeight + 2;
            EditorGUI.Slider(new Rect(rect.x, rect.y, rect.width, lineHeight), defaultVolume, 0f, 1f, "Default Volume");

            if (string.IsNullOrEmpty(exposedParam.stringValue) || group.objectReferenceValue == null)
            {
                var warnRect = new Rect(rect.x + rect.width - 20, rect.y - (lineHeight + 2) * 3, 18, 18);
                GUI.Label(warnRect, EditorGUIUtility.IconContent("console.warnicon.sml"));
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SOEditorKit.Title("Audio Config", "d_AudioMixerController Icon");

            using (SOEditorKit.Box())
            {
                SOEditorKit.SectionHeader("Mixer", "d_AudioMixerGroup Icon");
                EditorGUILayout.PropertyField(_mixer);
                if (_mixer.objectReferenceValue == null)
                    EditorGUILayout.HelpBox("No AudioMixer assigned — bus volume control and ducking are disabled.", MessageType.Warning);
            }
            EditorGUILayout.Space(6);

            using (SOEditorKit.Box())
                _busList.DoLayoutList();
            EditorGUILayout.Space(6);

            using (SOEditorKit.Box())
            {
                SOEditorKit.SectionHeader("SFX Pool", "d_UnityEditor.SceneHierarchyWindow");
                EditorGUILayout.PropertyField(_initialPoolSize);
                EditorGUILayout.PropertyField(_maxPoolSize);
                if (_maxPoolSize.intValue < _initialPoolSize.intValue)
                    EditorGUILayout.HelpBox("Max Pool Size is smaller than Initial Pool Size.", MessageType.Error);
            }
            EditorGUILayout.Space(6);

            using (SOEditorKit.Box())
            {
                SOEditorKit.SectionHeader("Music", "d_AudioSource Icon");
                EditorGUILayout.PropertyField(_defaultMusicFadeSeconds, new GUIContent("Default Fade (s)"));
            }
            EditorGUILayout.Space(6);

            using (SOEditorKit.Box())
            {
                SOEditorKit.SectionHeader("Ducking", "d_SceneViewFx");
                EditorGUILayout.PropertyField(_duckBus);
                EditorGUILayout.Slider(_duckTargetVolume, 0f, 1f, "Target Volume");
                EditorGUILayout.PropertyField(_duckAttackSeconds, new GUIContent("Attack (s)"));
                EditorGUILayout.PropertyField(_duckHoldSeconds, new GUIContent("Hold (s)"));
                EditorGUILayout.PropertyField(_duckReleaseSeconds, new GUIContent("Release (s)"));
            }
            EditorGUILayout.Space(6);

            using (SOEditorKit.Box())
            {
                SOEditorKit.SectionHeader("Volume Range");
                EditorGUILayout.PropertyField(_minDecibels, new GUIContent("Min Decibels (silence)"));
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
