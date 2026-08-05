using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Kaddumi.UnityTools.ProjectEnhancer
{
    public class ProjectWindowEnhancerSettingsProvider : SettingsProvider
    {
        private SerializedObject serializedSettings;

        public ProjectWindowEnhancerSettingsProvider(string path, SettingsScope scope = SettingsScope.Project)
            : base(path, scope)
        {
        }

        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
        {
            ProjectWindowEnhancerSettings.instance.hideFlags = HideFlags.HideAndDontSave;
            serializedSettings = new SerializedObject(ProjectWindowEnhancerSettings.instance);
        }

        public override void OnGUI(string searchContext)
        {
            serializedSettings.Update();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Customization", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("enableCustomIcons"), new GUIContent("Enable Custom Icons"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("enableCustomColors"), new GUIContent("Enable Custom Colors"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("enableContentMinimap"), new GUIContent("Enable Content Minimap"));

            if (EditorGUI.EndChangeCheck())
            {
                serializedSettings.ApplyModifiedProperties();
                ProjectWindowEnhancerSettings.instance.Save();
                EditorApplication.RepaintProjectWindow();
            }
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new ProjectWindowEnhancerSettingsProvider("Project/Unity Tools/Project Window", SettingsScope.Project)
            {
                label = "Project Window",
                keywords = new HashSet<string>(new[] { "Project", "Window", "Zebra", "Icon", "Color", "Enhancer" })
            };
            return provider;
        }
    }
}
