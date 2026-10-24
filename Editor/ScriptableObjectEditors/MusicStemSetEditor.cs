using System.Collections.Generic;
using Kaddumi.UnityTools.Audio.Data;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Kaddumi.UnityTools.EditorTools.ScriptableObjectEditors
{
    [CustomEditor(typeof(MusicStemSet))]
    public class MusicStemSetEditor : Editor
    {
        private SerializedProperty _id, _volume, _stems;
        private ReorderableList _stemList;

        private void OnEnable()
        {
            _id = serializedObject.FindProperty(nameof(MusicStemSet.Id));
            _volume = serializedObject.FindProperty(nameof(MusicStemSet.Volume));
            _stems = serializedObject.FindProperty(nameof(MusicStemSet.Stems));

            _stemList = new ReorderableList(serializedObject, _stems, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, $"Stems ({_stems.arraySize})  -  Id | Clip | Start Volume"),
                elementHeightCallback = _ => EditorGUIUtility.singleLineHeight + 6,
                drawElementCallback = DrawStem
            };
        }

        private void DrawStem(Rect rect, int index, bool active, bool focused)
        {
            var element = _stems.GetArrayElementAtIndex(index);
            var id = element.FindPropertyRelative("Id");
            var clip = element.FindPropertyRelative("Clip");
            var start = element.FindPropertyRelative("StartVolume");

            rect.y += 3;
            rect.height = EditorGUIUtility.singleLineHeight;

            const float gap = 4f;
            float w = rect.width - gap * 2;
            var idRect = new Rect(rect.x, rect.y, w * 0.3f, rect.height);
            var clipRect = new Rect(idRect.xMax + gap, rect.y, w * 0.45f, rect.height);
            var volRect = new Rect(clipRect.xMax + gap, rect.y, w * 0.25f, rect.height);

            EditorGUI.PropertyField(idRect, id, GUIContent.none);
            EditorGUI.PropertyField(clipRect, clip, GUIContent.none);
            EditorGUI.Slider(volRect, start, 0f, 1f, GUIContent.none);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var set = (MusicStemSet)target;

            SOEditorKit.Title("Music Stem Set", "d_AudioMixerController Icon");

            using (SOEditorKit.Box())
            {
                EditorGUILayout.PropertyField(_id, new GUIContent("Id"));
                EditorGUILayout.LabelField("Resolved Id", set.ResolvedId, EditorStyles.miniLabel);
                EditorGUILayout.Slider(_volume, 0f, 1f, "Volume");
            }
            EditorGUILayout.Space(6);

            using (SOEditorKit.Box())
            {
                _stemList.DoLayoutList();
                DrawValidation();
            }
            EditorGUILayout.Space(6);

            using (SOEditorKit.Box())
            {
                SOEditorKit.SectionHeader("Usage", "d_SceneViewAudio");
                EditorGUILayout.HelpBox(
                    "AudioManager.Instance.PlayStems(\"" + set.ResolvedId + "\", 2f);\n" +
                    "AudioManager.Instance.SetStemVolume(\"stemId\", 1f, 4f);\n\n" +
                    "All stems always play in sync; adding a layer means fading it up from 0. " +
                    "Stems must share length and sample rate. Import as Vorbis, Compressed In Memory, Preload Audio Data.",
                    MessageType.None);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawValidation()
        {
            if (_stems.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No stems assigned - PlayStems() will have nothing to play.", MessageType.Warning);
                return;
            }

            var seen = new HashSet<string>();
            var dupes = new List<string>();
            int blankIds = 0, missingClips = 0;
            AudioClip reference = null;
            var mismatched = new List<string>();
            var streaming = new List<string>();

            for (int i = 0; i < _stems.arraySize; i++)
            {
                var element = _stems.GetArrayElementAtIndex(i);
                var id = element.FindPropertyRelative("Id").stringValue;
                var clip = element.FindPropertyRelative("Clip").objectReferenceValue as AudioClip;
                string label = string.IsNullOrEmpty(id) ? "#" + i : id;

                if (string.IsNullOrEmpty(id)) blankIds++;
                else if (!seen.Add(id) && !dupes.Contains(id)) dupes.Add(id);

                if (clip == null)
                {
                    missingClips++;
                    continue;
                }

                if (reference == null) reference = clip;
                else if (clip.samples != reference.samples || clip.frequency != reference.frequency)
                    mismatched.Add(label + " (" + clip.samples + " samples @ " + clip.frequency + " Hz)");

                var path = AssetDatabase.GetAssetPath(clip);
                if (!string.IsNullOrEmpty(path))
                {
                    var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                    if (importer != null && importer.defaultSampleSettings.loadType == AudioClipLoadType.Streaming)
                        streaming.Add(label);
                }
            }

            if (blankIds > 0)
                EditorGUILayout.HelpBox(blankIds + " stem(s) have a blank Id and cannot be addressed by SetStemVolume.", MessageType.Warning);
            if (dupes.Count > 0)
                EditorGUILayout.HelpBox("Duplicate stem Ids: " + string.Join(", ", dupes) + ".", MessageType.Warning);
            if (missingClips > 0)
                EditorGUILayout.HelpBox(missingClips + " stem(s) have no clip assigned.", MessageType.Warning);
            if (mismatched.Count > 0)
                EditorGUILayout.HelpBox(
                    "Stems must match the first clip (" + reference.samples + " samples @ " + reference.frequency +
                    " Hz) or they will drift out of sync. Mismatched: " + string.Join(", ", mismatched) + ".",
                    MessageType.Error);
            if (streaming.Count > 0)
                EditorGUILayout.HelpBox(
                    "Load Type is Streaming for: " + string.Join(", ", streaming) +
                    ". Use Compressed In Memory so stems start together sample-accurately.",
                    MessageType.Warning);
        }
    }
}
