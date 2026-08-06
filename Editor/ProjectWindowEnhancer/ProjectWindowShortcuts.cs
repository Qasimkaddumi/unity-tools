using UnityEditor;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

namespace Kaddumi.UnityTools.ProjectEnhancer
{
    public static class ProjectWindowShortcuts
    {
        public static void Enable()
        {
            EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
        }

        public static void Disable()
        {
            EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.E)
            {
                bool shift = Event.current.shift;
                bool ctrl = Event.current.control || Event.current.command;
                
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!AssetDatabase.IsValidFolder(path)) return;
                
                int id = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path).GetInstanceID();
                
                // Ensure we only process this once for the active selection
                if (Selection.activeInstanceID != id) return; 
                if (!selectionRect.Contains(Event.current.mousePosition)) return;

                var prop = typeof(UnityEditorInternal.InternalEditorUtility).GetProperty("expandedProjectWindowItemIDs", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                
                if (ctrl && shift)
                {
                    // Collapse all folders
                    if (prop != null) prop.SetValue(null, new int[0], null);
                }
                else if (shift)
                {
                    // Isolate folder (collapse all, expand this)
                    if (prop != null) prop.SetValue(null, new int[] { id }, null);
                }
                else
                {
                    // Toggle folder expand/collapse
                    if (prop != null)
                    {
                        var expanded = (int[])prop.GetValue(null, null);
                        var list = new List<int>(expanded ?? new int[0]);
                        if (list.Contains(id)) list.Remove(id);
                        else list.Add(id);
                        prop.SetValue(null, list.ToArray(), null);
                    }
                    else
                    {
                        ToggleExpandedReflection(id);
                    }
                }
                
                Event.current.Use();
                EditorApplication.RepaintProjectWindow();
            }
        }
        
        private static void ToggleExpandedReflection(int id)
        {
            var projectBrowserType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
            var browsers = Resources.FindObjectsOfTypeAll(projectBrowserType) as EditorWindow[];
            foreach (var browser in browsers)
            {
                var m_AssetTree = projectBrowserType.GetField("m_AssetTree", BindingFlags.Instance | BindingFlags.NonPublic);
                if (m_AssetTree != null)
                {
                    var tree = m_AssetTree.GetValue(browser);
                    if (tree != null)
                    {
                        var data = tree.GetType().GetProperty("data").GetValue(tree, null);
                        if (data != null)
                        {
                            var isExpandedMethod = data.GetType().GetMethod("IsExpanded", new[] { typeof(int) });
                            var setExpandedMethod = data.GetType().GetMethod("SetExpanded", new[] { typeof(int), typeof(bool) });
                            
                            if (isExpandedMethod != null && setExpandedMethod != null)
                            {
                                bool isExpanded = (bool)isExpandedMethod.Invoke(data, new object[] { id });
                                setExpandedMethod.Invoke(data, new object[] { id, !isExpanded });
                            }
                        }
                    }
                }
                
                var m_FolderTree = projectBrowserType.GetField("m_FolderTree", BindingFlags.Instance | BindingFlags.NonPublic);
                if (m_FolderTree != null)
                {
                    var tree = m_FolderTree.GetValue(browser);
                    if (tree != null)
                    {
                        var data = tree.GetType().GetProperty("data").GetValue(tree, null);
                        if (data != null)
                        {
                            var setExpandedMethod = data.GetType().GetMethod("SetExpanded", new[] { typeof(int), typeof(bool) });
                            if (setExpandedMethod != null)
                            {
                                // We don't know the state, so just expand it if we hit this fallback
                                setExpandedMethod.Invoke(data, new object[] { id, true });
                            }
                        }
                    }
                }
            }
        }
    }
}
