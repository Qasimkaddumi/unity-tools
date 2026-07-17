using Kaddumi.UnityTools.LoadingSystem.Core;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Kaddumi.UnityTools.EditorTools.ScriptableObjectEditors
{
    [CustomEditor(typeof(SceneCatalog))]
    public class SceneCatalogEditor : Editor
    {
        private SerializedProperty _mainMenuScenes, _loadingScene, _gameplayScenes, _levels;
        private ReorderableList _mainMenuList, _gameplayList, _levelList;

        private void OnEnable()
        {
            _mainMenuScenes = serializedObject.FindProperty("mainMenuScenes");
            _loadingScene = serializedObject.FindProperty("loadingScene");
            _gameplayScenes = serializedObject.FindProperty("gameplayScenes");
            _levels = serializedObject.FindProperty("levels");

            _mainMenuList = MakeSceneList(_mainMenuScenes, "Main Menu Scenes");
            _gameplayList = MakeSceneList(_gameplayScenes, "Gameplay Scenes");
            _levelList = MakeSceneList(_levels, "Levels");
        }

        private ReorderableList MakeSceneList(SerializedProperty prop, string header)
        {
            return new ReorderableList(serializedObject, prop, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, $"{header} ({prop.arraySize})"),
                elementHeightCallback = _ => EditorGUIUtility.singleLineHeight + 4,
                drawElementCallback = (rect, index, active, focused) =>
                {
                    var element = prop.GetArrayElementAtIndex(index);
                    rect.y += 2;
                    rect.height = EditorGUIUtility.singleLineHeight;

                    var sceneDef = element.objectReferenceValue as SceneDefinition;
                    if (sceneDef != null && !sceneDef.IsValid())
                    {
                        GUI.Label(new Rect(rect.x, rect.y, 18, rect.height), EditorGUIUtility.IconContent("console.warnicon.sml"));
                        rect.x += 20;
                        rect.width -= 20;
                    }
                    EditorGUI.PropertyField(rect, element, GUIContent.none);
                }
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SOEditorKit.Title("Scene Catalog", "d_BuildSettings.Standalone Icon");

            using (SOEditorKit.Box())
            {
                SOEditorKit.SectionHeader("General", "d_SceneAsset Icon");
                EditorGUILayout.PropertyField(_loadingScene, new GUIContent("Loading Scene"));
                if (_loadingScene.objectReferenceValue == null)
                    EditorGUILayout.HelpBox("No Loading Scene assigned.", MessageType.Info);
                EditorGUILayout.Space(4);
                _mainMenuList.DoLayoutList();
            }
            EditorGUILayout.Space(6);

            using (SOEditorKit.Box())
            {
                SOEditorKit.SectionHeader("Gameplay", "d_UnityEditor.GameView");
                _gameplayList.DoLayoutList();
            }
            EditorGUILayout.Space(6);

            using (SOEditorKit.Box())
            {
                SOEditorKit.SectionHeader("Levels", "d_UnityEditor.HierarchyWindow");
                _levelList.DoLayoutList();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
