using System.Collections.Generic;
using Kaddumi.UnityTools.Audio.Data;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Kaddumi.UnityTools.EditorTools.ScriptableObjectEditors
{
    [CustomEditor(typeof(SoundLibrary))]
    public class SoundLibraryEditor : Editor
    {
        private SerializedProperty _sounds;
        private ReorderableList _list;

        private void OnEnable()
        {
            _sounds = serializedObject.FindProperty("sounds");
            _list = new ReorderableList(serializedObject, _sounds, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, $"Sounds ({_sounds.arraySize})"),
                elementHeightCallback = _ => EditorGUIUtility.singleLineHeight + 6,
                drawElementCallback = DrawElement
            };
        }

        private void DrawElement(Rect rect, int index, bool active, bool focused)
        {
            var element = _sounds.GetArrayElementAtIndex(index);
            var sound = element.objectReferenceValue as SoundDefinition;

            rect.y += 3;
            rect.height = EditorGUIUtility.singleLineHeight;

            bool isDuplicate = sound != null && IsDuplicateId(sound, index);
            if (isDuplicate)
            {
                var warnRect = new Rect(rect.x, rect.y, 18, rect.height);
                GUI.Label(warnRect, EditorGUIUtility.IconContent("console.warnicon.sml"));
                rect.x += 20;
                rect.width -= 20;
            }

            var fieldRect = new Rect(rect.x, rect.y, rect.width * 0.6f, rect.height);
            EditorGUI.PropertyField(fieldRect, element, GUIContent.none);

            var idRect = new Rect(rect.x + rect.width * 0.6f + 6, rect.y, rect.width * 0.4f - 6, rect.height);
            EditorGUI.LabelField(idRect, sound != null ? sound.ResolvedId : string.Empty, EditorStyles.miniLabel);
        }

        private bool IsDuplicateId(SoundDefinition sound, int index)
        {
            var id = sound.ResolvedId;
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < _sounds.arraySize; i++)
            {
                if (i == index) continue;
                var other = _sounds.GetArrayElementAtIndex(i).objectReferenceValue as SoundDefinition;
                if (other != null && other.ResolvedId == id) return true;
            }
            return false;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SOEditorKit.Title("Sound Library", "d_AudioMixerController Icon");

            using (SOEditorKit.Box())
            {
                _list.DoLayoutList();
            }

            var duplicates = FindDuplicateIds();
            if (duplicates.Count > 0)
                EditorGUILayout.HelpBox("Duplicate Ids detected: " + string.Join(", ", duplicates) + ". Only the last entry with each Id is playable.", MessageType.Warning);

            serializedObject.ApplyModifiedProperties();
        }

        private List<string> FindDuplicateIds()
        {
            var seen = new HashSet<string>();
            var dupes = new List<string>();
            for (int i = 0; i < _sounds.arraySize; i++)
            {
                var sound = _sounds.GetArrayElementAtIndex(i).objectReferenceValue as SoundDefinition;
                if (sound == null) continue;
                var id = sound.ResolvedId;
                if (string.IsNullOrEmpty(id)) continue;
                if (!seen.Add(id) && !dupes.Contains(id)) dupes.Add(id);
            }
            return dupes;
        }
    }
}
