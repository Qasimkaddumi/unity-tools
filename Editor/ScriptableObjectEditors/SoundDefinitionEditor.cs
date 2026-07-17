using Kaddumi.UnityTools.Audio.Core;
using Kaddumi.UnityTools.Audio.Data;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Kaddumi.UnityTools.EditorTools.ScriptableObjectEditors
{
    [CustomEditor(typeof(SoundDefinition))]
    public class SoundDefinitionEditor : Editor
    {
        private static readonly Color[] BusColors =
        {
            new Color(0.9f, 0.7f, 0.2f),  // Master
            new Color(0.6f, 0.4f, 0.9f),  // Music
            new Color(0.3f, 0.8f, 0.5f),  // SFX
            new Color(0.3f, 0.7f, 0.9f),  // UI
            new Color(0.5f, 0.5f, 0.5f),  // Ambience
            new Color(0.9f, 0.4f, 0.5f),  // Voice
        };

        private SerializedProperty _id, _clips, _bus, _volume, _pitchRange, _loop;
        private SerializedProperty _spatialBlend, _minDistance, _maxDistance, _maxVoices, _priority;
        private ReorderableList _clipList;
        private AudioClip _lastPreviewClip;

        private void OnEnable()
        {
            var so = (SoundDefinition)target;
            _id = serializedObject.FindProperty(nameof(SoundDefinition.Id));
            _clips = serializedObject.FindProperty(nameof(SoundDefinition.Clips));
            _bus = serializedObject.FindProperty(nameof(SoundDefinition.Bus));
            _volume = serializedObject.FindProperty(nameof(SoundDefinition.Volume));
            _pitchRange = serializedObject.FindProperty(nameof(SoundDefinition.PitchRange));
            _loop = serializedObject.FindProperty(nameof(SoundDefinition.Loop));
            _spatialBlend = serializedObject.FindProperty(nameof(SoundDefinition.SpatialBlend));
            _minDistance = serializedObject.FindProperty(nameof(SoundDefinition.MinDistance));
            _maxDistance = serializedObject.FindProperty(nameof(SoundDefinition.MaxDistance));
            _maxVoices = serializedObject.FindProperty(nameof(SoundDefinition.MaxVoices));
            _priority = serializedObject.FindProperty(nameof(SoundDefinition.Priority));

            _clipList = new ReorderableList(serializedObject, _clips, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, $"Clips ({_clips.arraySize})"),
                elementHeightCallback = _ => EditorGUIUtility.singleLineHeight + 4,
                drawElementCallback = (rect, index, active, focused) =>
                {
                    var element = _clips.GetArrayElementAtIndex(index);
                    rect.y += 2;
                    rect.height = EditorGUIUtility.singleLineHeight;
                    EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, rect.height), element, GUIContent.none);
                }
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var so = (SoundDefinition)target;

            SOEditorKit.Title("Sound", "d_AudioSource Icon");

            using (SOEditorKit.Box())
            {
                EditorGUILayout.PropertyField(_id, new GUIContent("Id"));
                EditorGUILayout.LabelField("Resolved Id", so.ResolvedId, EditorStyles.miniLabel);

                var prevColor = GUI.backgroundColor;
                GUI.backgroundColor = BusColors[(int)(AudioBus)_bus.enumValueIndex];
                EditorGUILayout.PropertyField(_bus);
                GUI.backgroundColor = prevColor;
            }
            EditorGUILayout.Space(6);

            using (SOEditorKit.Box())
            {
                _clipList.DoLayoutList();
                if (_clips.arraySize == 0)
                    EditorGUILayout.HelpBox("No clips assigned — PickClip() will return null.", MessageType.Warning);

                DrawClipPreviewButton(so);
            }
            EditorGUILayout.Space(6);

            using (SOEditorKit.Box())
            {
                SOEditorKit.SectionHeader("Playback", "d_SceneViewAudio");
                EditorGUILayout.Slider(_volume, 0f, 1f, "Volume");
                var range = _pitchRange.vector2Value;
                EditorGUILayout.MinMaxSlider(new GUIContent("Pitch Range"), ref range.x, ref range.y, 0.1f, 3f);
                range.x = EditorGUILayout.FloatField("  Min", range.x);
                range.y = EditorGUILayout.FloatField("  Max", range.y);
                _pitchRange.vector2Value = range;
                EditorGUILayout.PropertyField(_loop);
                EditorGUILayout.PropertyField(_priority);
                EditorGUILayout.PropertyField(_maxVoices, new GUIContent("Max Voices (0 = unlimited)"));
            }
            EditorGUILayout.Space(6);

            using (SOEditorKit.Box())
            {
                SOEditorKit.SectionHeader("3D Spatialization", "d_SceneViewFx");
                EditorGUILayout.Slider(_spatialBlend, 0f, 1f, "Spatial Blend (2D↔3D)");
                using (new EditorGUI.DisabledScope(_spatialBlend.floatValue <= 0f))
                {
                    EditorGUILayout.PropertyField(_minDistance);
                    EditorGUILayout.PropertyField(_maxDistance);
                    if (_minDistance.floatValue > _maxDistance.floatValue)
                        EditorGUILayout.HelpBox("Min Distance is greater than Max Distance.", MessageType.Error);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawClipPreviewButton(SoundDefinition so)
        {
            var clip = so.Clips is { Length: > 0 } ? so.Clips[0] : null;
            using (new EditorGUI.DisabledScope(clip == null))
            {
                if (GUILayout.Button(new GUIContent(" Preview First Clip", EditorGUIUtility.IconContent("d_PlayButton").image)))
                {
                    PlayClip(clip);
                }
            }
        }

        private static void PlayClip(AudioClip clip)
        {
            var utilType = System.Type.GetType("UnityEditor.AudioUtil,UnityEditor");
            var method = utilType?.GetMethod("PlayPreviewClip", new[] { typeof(AudioClip), typeof(int), typeof(bool) });
            method?.Invoke(null, new object[] { clip, 0, false });
        }
    }
}
