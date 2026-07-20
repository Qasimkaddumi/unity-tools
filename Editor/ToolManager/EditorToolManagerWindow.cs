using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kaddumi.UnityTools.ToolManager.Editor
{
    /// <summary>
    /// Central dashboard for switching the project's editor tools on and off. Every
    /// <see cref="IEditorToolModule"/> in the project appears here automatically, grouped by
    /// category. This window only presents state — <see cref="EditorToolRegistry"/> owns it.
    /// </summary>
    public sealed class EditorToolManagerWindow : EditorWindow
    {
        private const string DefaultCategory = "General";

        private Vector2 _scroll;
        private string _search = string.Empty;

        [MenuItem("Tools/Unity Tools/Tool Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<EditorToolManagerWindow>("Tool Manager");
            window.minSize = new Vector2(360f, 240f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorToolRegistry.StateChanged += Repaint;
        }

        private void OnDisable()
        {
            EditorToolRegistry.StateChanged -= Repaint;
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawToolbar();

            IReadOnlyList<IEditorToolModule> modules = EditorToolRegistry.Modules;
            if (modules.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No tools found. Implement IEditorToolModule to have a tool appear here.",
                    MessageType.Info);
                return;
            }

            List<IEditorToolModule> filtered = FilterModules(modules);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (filtered.Count == 0)
            {
                EditorGUILayout.HelpBox($"No tools match \"{_search}\".", MessageType.None);
            }
            else
            {
                foreach (IGrouping<string, IEditorToolModule> group in GroupByCategory(filtered))
                    DrawCategory(group.Key, group);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(
                EditorGUIUtility.IconContent("SettingsIcon"),
                GUILayout.Width(20), GUILayout.Height(20));
            GUILayout.Label("Editor Tool Manager", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            int enabledCount = EditorToolRegistry.Modules.Count(EditorToolRegistry.IsEnabled);
            GUILayout.Label(
                $"{enabledCount}/{EditorToolRegistry.Modules.Count} active",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                "Turn project editor tools on or off. Choices persist across sessions.",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(2);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(80));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Enable All", EditorStyles.toolbarButton, GUILayout.Width(80)))
                SetAll(true);
            if (GUILayout.Button("Disable All", EditorStyles.toolbarButton, GUILayout.Width(80)))
                SetAll(false);

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        private void DrawCategory(string category, IEnumerable<IEditorToolModule> modules)
        {
            EditorGUILayout.LabelField(category, EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foreach (IEditorToolModule module in modules)
                DrawToolRow(module);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);
        }

        private void DrawToolRow(IEditorToolModule module)
        {
            bool enabled = EditorToolRegistry.IsEnabled(module);

            EditorGUILayout.BeginHorizontal();

            // Toggle drives the tool's lifecycle through the registry.
            bool newEnabled = EditorGUILayout.Toggle(enabled, GUILayout.Width(18));

            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(module.DisplayName, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            DrawStatusPill(enabled);
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(module.Description))
            {
                var descStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                EditorGUILayout.LabelField(module.Description, descStyle);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            if (newEnabled != enabled)
                EditorToolRegistry.SetEnabled(module, newEnabled);

            EditorGUILayout.Space(2);
        }

        private static void DrawStatusPill(bool enabled)
        {
            Color prev = GUI.color;
            GUI.color = enabled ? new Color(0.45f, 0.85f, 0.45f) : new Color(0.7f, 0.7f, 0.7f);
            GUILayout.Label(enabled ? "● On" : "○ Off", EditorStyles.miniLabel, GUILayout.Width(40));
            GUI.color = prev;
        }

        private List<IEditorToolModule> FilterModules(IReadOnlyList<IEditorToolModule> modules)
        {
            if (string.IsNullOrWhiteSpace(_search))
                return modules.ToList();

            string term = _search.Trim();
            return modules.Where(m =>
                    Contains(m.DisplayName, term) ||
                    Contains(m.Description, term) ||
                    Contains(m.Category, term))
                .ToList();
        }

        private static bool Contains(string source, string term) =>
            !string.IsNullOrEmpty(source) &&
            source.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private static IEnumerable<IGrouping<string, IEditorToolModule>> GroupByCategory(
            IEnumerable<IEditorToolModule> modules) =>
            modules.GroupBy(m => string.IsNullOrEmpty(m.Category) ? DefaultCategory : m.Category);

        private static void SetAll(bool enabled)
        {
            foreach (IEditorToolModule module in EditorToolRegistry.Modules)
                EditorToolRegistry.SetEnabled(module, enabled);
        }
    }
}
