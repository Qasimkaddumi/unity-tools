using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

namespace Kaddumi.UnityTools.ProjectEnhancer
{
    public static class ProjectWindowNavigationBar
    {
        private static List<string> history = new List<string>();
        private static int historyIndex = -1;
        private static bool isNavigating = false;
        
        private static List<VisualElement> activeBookmarkContainers = new List<VisualElement>();

        public static void Enable()
        {
            EditorApplication.update -= InjectNavBar;
            EditorApplication.update += InjectNavBar;
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
        }

        public static void Disable()
        {
            EditorApplication.update -= InjectNavBar;
            Selection.selectionChanged -= OnSelectionChanged;
            RemoveNavBar();
        }

        private static void RemoveNavBar()
        {
            var projectBrowserType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
            if (projectBrowserType == null) return;

            var browsers = Resources.FindObjectsOfTypeAll(projectBrowserType) as EditorWindow[];
            
            foreach (var browser in browsers)
            {
                if (browser.rootVisualElement == null) continue;
                var existingBar = browser.rootVisualElement.Q("ProjectNavBar");
                if (existingBar != null)
                {
                    browser.rootVisualElement.Remove(existingBar);
                }
            }
            activeBookmarkContainers.Clear();
        }

        private static void OnSelectionChanged()
        {
            if (isNavigating) return;

            var activeObject = Selection.activeObject;
            if (activeObject == null) return;

            string path = AssetDatabase.GetAssetPath(activeObject);
            if (!AssetDatabase.IsValidFolder(path)) return;
            
            string guid = AssetDatabase.AssetPathToGUID(path);

            // Add to history
            if (historyIndex == -1 || history[historyIndex] != guid)
            {
                // Remove forward history
                if (historyIndex < history.Count - 1)
                {
                    history.RemoveRange(historyIndex + 1, history.Count - (historyIndex + 1));
                }
                history.Add(guid);
                historyIndex++;
            }
        }

        private static void InjectNavBar()
        {
            var projectBrowserType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
            if (projectBrowserType == null) return;

            var browsers = Resources.FindObjectsOfTypeAll(projectBrowserType) as EditorWindow[];
            
            activeBookmarkContainers.RemoveAll(x => x.panel == null);
            
            foreach (var browser in browsers)
            {
                if (browser.rootVisualElement == null) continue;

                var existingBar = browser.rootVisualElement.Q("ProjectNavBar");
                if (existingBar == null)
                {
                    var navBar = new VisualElement { name = "ProjectNavBar" };
                    navBar.style.flexDirection = FlexDirection.Row;
                    navBar.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
                    navBar.style.borderBottomWidth = 1;
                    navBar.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f, 1f);
                    navBar.style.paddingLeft = 4;
                    navBar.style.paddingRight = 4;
                    navBar.style.paddingTop = 2;
                    navBar.style.paddingBottom = 2;

                    var backBtn = new Button(() => Navigate(-1)) { text = "◀" };
                    var fwdBtn = new Button(() => Navigate(1)) { text = "▶" };
                    
                    backBtn.style.marginRight = 2;
                    fwdBtn.style.marginRight = 8;
                    
                    var bookmarksContainer = new VisualElement { name = "BookmarksContainer" };
                    bookmarksContainer.style.flexDirection = FlexDirection.Row;
                    bookmarksContainer.style.flexGrow = 1;
                    
                    // Drag and drop visual feedback
                    bookmarksContainer.RegisterCallback<DragUpdatedEvent>(evt =>
                    {
                        if (DragAndDrop.objectReferences.Length > 0 && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(DragAndDrop.objectReferences[0])))
                        {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                            evt.StopPropagation();
                        }
                    });

                    bookmarksContainer.RegisterCallback<DragPerformEvent>(evt =>
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            string path = AssetDatabase.GetAssetPath(obj);
                            if (AssetDatabase.IsValidFolder(path))
                            {
                                string guid = AssetDatabase.AssetPathToGUID(path);
                                ProjectFolderData.instance.SetBookmark(guid, true);
                            }
                        }
                        RefreshAllBookmarks();
                        evt.StopPropagation();
                    });

                    var customizeBtn = new Button(() => 
                    {
                        var active = Selection.activeObject;
                        if (active != null)
                        {
                            string path = AssetDatabase.GetAssetPath(active);
                            if (AssetDatabase.IsValidFolder(path))
                            {
                                string guid = AssetDatabase.AssetPathToGUID(path);
                                var window = EditorWindow.GetWindow<FolderCustomizationPopup>(true, "Customize Folder", true);
                                window.minSize = new Vector2(300, 420);
                                // We need to make Init public or internal
                                window.GetType().GetMethod("Init", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.Invoke(window, new object[] { guid });
                            }
                        }
                    }) { text = "⚙ Customize" };
                    customizeBtn.style.marginLeft = new StyleLength(StyleKeyword.Auto); // Push to the right
                    
                    navBar.Add(backBtn);
                    navBar.Add(fwdBtn);
                    navBar.Add(bookmarksContainer);
                    navBar.Add(customizeBtn);

                    activeBookmarkContainers.Add(bookmarksContainer);
                    RefreshBookmarks(bookmarksContainer);

                    browser.rootVisualElement.Insert(0, navBar);
                }
            }
        }
        
        public static void RefreshAllBookmarks()
        {
            activeBookmarkContainers.RemoveAll(x => x.panel == null);
            foreach (var c in activeBookmarkContainers)
            {
                RefreshBookmarks(c);
            }
        }

        private static void RefreshBookmarks(VisualElement bookmarksContainer)
        {
            bookmarksContainer.Clear();
            
            var data = ProjectFolderData.instance.folderConfigs;
            var bookmarks = data.Where(x => x.isBookmarked).ToList();
            
            if (bookmarks.Count == 0)
            {
                var lbl = new Label("Drag folders here to bookmark");
                lbl.style.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                lbl.style.unityTextAlign = TextAnchor.MiddleLeft;
                bookmarksContainer.Add(lbl);
                return;
            }
            
            foreach (var config in bookmarks)
            {
                string path = AssetDatabase.GUIDToAssetPath(config.guid);
                if (!string.IsNullOrEmpty(path))
                {
                    string name = System.IO.Path.GetFileName(path);
                    var btn = new Button(() => 
                    {
                        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                        if (obj != null)
                        {
                            isNavigating = true;
                            Selection.activeObject = obj;
                            EditorGUIUtility.PingObject(obj);
                            isNavigating = false;
                            
                            // Also expand the folder in project browser
                            var prop = typeof(UnityEditorInternal.InternalEditorUtility).GetProperty("expandedProjectWindowItemIDs", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                            if (prop != null)
                            {
                                var expanded = (int[])prop.GetValue(null, null);
                                var list = new List<int>(expanded ?? new int[0]);
                                int id = obj.GetInstanceID();
                                if (!list.Contains(id))
                                {
                                    list.Add(id);
                                    prop.SetValue(null, list.ToArray(), null);
                                }
                            }
                        }
                    }) { text = name };
                    
                    btn.RegisterCallback<ContextClickEvent>(evt =>
                    {
                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Remove Bookmark"), false, () => 
                        {
                            ProjectFolderData.instance.SetBookmark(config.guid, false);
                            RefreshAllBookmarks();
                        });
                        menu.ShowAsContext();
                        evt.StopPropagation();
                    });

                    // Styling
                    btn.style.marginRight = 2;
                    btn.style.marginLeft = 2;
                    btn.style.paddingLeft = 8;
                    btn.style.paddingRight = 8;
                    btn.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f);
                    btn.style.borderTopLeftRadius = 3;
                    btn.style.borderTopRightRadius = 3;
                    btn.style.borderBottomLeftRadius = 3;
                    btn.style.borderBottomRightRadius = 3;
                    btn.tooltip = "Right-click to remove bookmark";
                    
                    bookmarksContainer.Add(btn);
                }
            }
        }

        private static void Navigate(int dir)
        {
            if (history.Count == 0) return;
            
            int newIndex = historyIndex + dir;
            if (newIndex >= 0 && newIndex < history.Count)
            {
                historyIndex = newIndex;
                string guid = history[historyIndex];
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (obj != null)
                {
                    isNavigating = true;
                    Selection.activeObject = obj;
                    EditorGUIUtility.PingObject(obj);
                    isNavigating = false;
                }
            }
        }
    }
}
