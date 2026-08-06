using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Kaddumi.UnityTools.ProjectEnhancer
{
    public static class ProjectWindowNavigationBar
    {
        private static List<string> history = new List<string>();
        private static int historyIndex = -1;
        private static bool isNavigating = false;
        private static bool isRenamingProfile = false;
        
        private static List<VisualElement> activeNavBars = new List<VisualElement>();
        
        // Track dragging
        private static VisualElement draggedBookmark = null;
        private static int draggedOriginalIndex = -1;

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
            activeNavBars.Clear();
        }

        private static void OnSelectionChanged()
        {
            if (isNavigating) return;

            var activeObject = Selection.activeObject;
            if (activeObject == null) return;

            string path = AssetDatabase.GetAssetPath(activeObject);
            if (string.IsNullOrEmpty(path)) return;
            
            string guid = AssetDatabase.AssetPathToGUID(path);

            // Add to history if folder
            if (AssetDatabase.IsValidFolder(path))
            {
                if (historyIndex == -1 || history[historyIndex] != guid)
                {
                    if (historyIndex < history.Count - 1)
                    {
                        history.RemoveRange(historyIndex + 1, history.Count - (historyIndex + 1));
                    }
                    history.Add(guid);
                    historyIndex++;
                }
            }
        }

        private static void InjectNavBar()
        {
            var projectBrowserType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
            if (projectBrowserType == null) return;

            var browsers = Resources.FindObjectsOfTypeAll(projectBrowserType) as EditorWindow[];
            
            activeNavBars.RemoveAll(x => x.panel == null);
            
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
                    navBar.style.alignItems = Align.Center;
                    navBar.style.flexWrap = Wrap.NoWrap;
                    navBar.style.overflow = Overflow.Hidden;

                    // History buttons
                    var backBtn = new Button(() => Navigate(-1)) { text = "◀" };
                    var fwdBtn = new Button(() => Navigate(1)) { text = "▶" };
                    backBtn.style.marginRight = 2;
                    fwdBtn.style.marginRight = 4;
                    

                    // Bookmarks
                    var bookmarksContainer = new VisualElement { name = "BookmarksContainer" };
                    bookmarksContainer.style.flexDirection = FlexDirection.Row;
                    bookmarksContainer.style.flexGrow = 1;
                    bookmarksContainer.style.overflow = Overflow.Hidden;
                    
                    // Drag and drop visual feedback for bookmarks
                    bookmarksContainer.RegisterCallback<DragUpdatedEvent>(evt =>
                    {
                        if (DragAndDrop.objectReferences.Length > 0 && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(DragAndDrop.objectReferences[0])))
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
                            if (!string.IsNullOrEmpty(path))
                            {
                                string guid = AssetDatabase.AssetPathToGUID(path);
                                ProjectBookmarkData.instance.SetBookmark(guid, true);
                            }
                        }
                        RefreshAllBookmarks();
                        evt.StopPropagation();
                    });

                    // Profiles
                    var profileBtn = new Button(() => ShowProfilesMenu()) { name = "ProfileBtn" };
                    profileBtn.style.marginLeft = 4;

                    navBar.Add(backBtn);
                    navBar.Add(fwdBtn);
                    navBar.Add(bookmarksContainer);
                    navBar.Add(profileBtn);

                    activeNavBars.Add(navBar);
                    
                    RefreshBookmarks(navBar);
                    RefreshProfileButton(navBar);

                    browser.rootVisualElement.Insert(0, navBar);
                }
            }
        }


        private static void ShowProfilesMenu()
        {
            var menu = new GenericMenu();
            var data = ProjectBookmarkData.instance;
            
            for (int i = 0; i < data.profiles.Count; i++)
            {
                int index = i;
                menu.AddItem(new GUIContent(data.profiles[i].profileName), data.activeProfileIndex == i, () => 
                {
                    data.activeProfileIndex = index;
                    data.Save();
                    RefreshAllBookmarks();
                    RefreshAllProfileButtons();
                });
            }
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Rename Current Profile..."), false, () => 
            {
                isRenamingProfile = true;
                RefreshAllProfileButtons();
            });
            menu.AddItem(new GUIContent("Add Profile..."), false, () => 
            {
                data.profiles.Add(new BookmarkProfile { profileName = "Profile " + (data.profiles.Count + 1) });
                data.activeProfileIndex = data.profiles.Count - 1;
                data.Save();
                RefreshAllBookmarks();
                RefreshAllProfileButtons();
            });
            menu.ShowAsContext();
        }


        public static void RefreshAllProfileButtons()
        {
            activeNavBars.RemoveAll(x => x.panel == null);
            foreach (var navBar in activeNavBars)
            {
                RefreshProfileButton(navBar);
            }
        }

        private static void RefreshProfileButton(VisualElement navBar)
        {
            var btn = navBar.Q<Button>("ProfileBtn");
            if (btn != null)
            {
                if (isRenamingProfile)
                {
                    btn.style.display = DisplayStyle.None;
                    var field = navBar.Q<TextField>("ProfileRenameField");
                    if (field == null)
                    {
                        field = new TextField();
                        field.name = "ProfileRenameField";
                        field.style.marginLeft = 4;
                        field.style.width = 100;
                        field.RegisterCallback<FocusOutEvent>(evt => FinishRename(field.value));
                        field.RegisterCallback<KeyDownEvent>(evt => 
                        {
                            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                            {
                                FinishRename(field.value);
                            }
                            else if (evt.keyCode == KeyCode.Escape)
                            {
                                FinishRename(null);
                            }
                        });
                        navBar.Add(field);
                    }
                    field.style.display = DisplayStyle.Flex;
                    field.value = ProjectBookmarkData.instance.ActiveProfile.profileName;
                    field.Focus();
                }
                else
                {
                    btn.style.display = DisplayStyle.Flex;
                    btn.text = ProjectBookmarkData.instance.ActiveProfile.profileName + " ▾";
                    var field = navBar.Q<TextField>("ProfileRenameField");
                    if (field != null) field.style.display = DisplayStyle.None;
                }
            }
        }

        private static void FinishRename(string newName)
        {
            if (!isRenamingProfile) return;
            if (newName != null && !string.IsNullOrWhiteSpace(newName))
            {
                ProjectBookmarkData.instance.ActiveProfile.profileName = newName;
                ProjectBookmarkData.instance.Save();
            }
            isRenamingProfile = false;
            RefreshAllProfileButtons();
        }

        public static void RefreshAllBookmarks()
        {
            activeNavBars.RemoveAll(x => x.panel == null);
            foreach (var navBar in activeNavBars)
            {
                RefreshBookmarks(navBar);
            }
        }

        private static void RefreshBookmarks(VisualElement navBar)
        {
            var bookmarksContainer = navBar.Q("BookmarksContainer");
            if (bookmarksContainer == null) return;
            bookmarksContainer.Clear();
            
            var profile = ProjectBookmarkData.instance.ActiveProfile;
            var bookmarks = profile.bookmarks.Where(x => x.isBookmarked).ToList();
            
            if (bookmarks.Count == 0)
            {
                var lbl = new Label("Drag assets/folders here to bookmark");
                lbl.style.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                lbl.style.unityTextAlign = TextAnchor.MiddleLeft;
                bookmarksContainer.Add(lbl);
                return;
            }
            
            for (int i = 0; i < bookmarks.Count; i++)
            {
                var config = bookmarks[i];
                int index = i; // capture index for drag
                string path = AssetDatabase.GUIDToAssetPath(config.guid);
                if (!string.IsNullOrEmpty(path))
                {
                    string name = Path.GetFileName(path);
                    var btn = new Button(() => 
                    {
                        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                        if (obj != null)
                        {
                            isNavigating = true;
                            Selection.activeObject = obj;
                            EditorGUIUtility.PingObject(obj);
                            isNavigating = false;
                            
                            // Expand folder in project browser
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
                        
                        menu.AddItem(new GUIContent("Color/Clear"), false, () => { config.color = Color.clear; ProjectBookmarkData.instance.Save(); RefreshAllBookmarks(); });
                        menu.AddItem(new GUIContent("Color/Red"), false, () => { config.color = new Color(0.8f, 0.2f, 0.2f, 1f); ProjectBookmarkData.instance.Save(); RefreshAllBookmarks(); });
                        menu.AddItem(new GUIContent("Color/Green"), false, () => { config.color = new Color(0.2f, 0.8f, 0.2f, 1f); ProjectBookmarkData.instance.Save(); RefreshAllBookmarks(); });
                        menu.AddItem(new GUIContent("Color/Blue"), false, () => { config.color = new Color(0.2f, 0.4f, 0.8f, 1f); ProjectBookmarkData.instance.Save(); RefreshAllBookmarks(); });
                        menu.AddItem(new GUIContent("Color/Yellow"), false, () => { config.color = new Color(0.8f, 0.8f, 0.2f, 1f); ProjectBookmarkData.instance.Save(); RefreshAllBookmarks(); });
                        
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Remove Bookmark"), false, () => 
                        {
                            ProjectBookmarkData.instance.SetBookmark(config.guid, false);
                            RefreshAllBookmarks();
                        });
                        menu.ShowAsContext();
                        evt.StopPropagation();
                    });

                    bool isDragging = false;
                    Vector2 startPos = Vector2.zero;
                    VisualElement caretLine = null;
                    int targetIndex = -1;

                    btn.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        if (evt.button == 0) // left click
                        {
                            draggedBookmark = btn;
                            draggedOriginalIndex = index;
                            isDragging = false;
                            startPos = evt.position;
                        }
                    }, TrickleDown.TrickleDown);

                    btn.RegisterCallback<PointerUpEvent>(evt =>
                    {
                        if (draggedBookmark == btn)
                        {
                            if (isDragging)
                            {
                                btn.ReleasePointer(evt.pointerId);
                                
                                if (caretLine != null && caretLine.parent != null)
                                {
                                    caretLine.parent.Remove(caretLine);
                                }
                                
                                if (targetIndex != -1 && targetIndex != draggedOriginalIndex)
                                {
                                    var profile = ProjectBookmarkData.instance.ActiveProfile;
                                    var bookmarkedList = profile.bookmarks.Where(x => x.isBookmarked).ToList();
                                    
                                    bookmarkedList.Remove(config);
                                    if (targetIndex > bookmarkedList.Count) targetIndex = bookmarkedList.Count;
                                    if (targetIndex < 0) targetIndex = 0;
                                    bookmarkedList.Insert(targetIndex, config);
                                    
                                    var unbookmarkedList = profile.bookmarks.Where(x => !x.isBookmarked).ToList();
                                    profile.bookmarks = bookmarkedList.Concat(unbookmarkedList).ToList();
                                    
                                    ProjectBookmarkData.instance.Save();
                                }
                                
                                RefreshAllBookmarks();
                                evt.StopPropagation();
                            }
                            draggedBookmark = null;
                            isDragging = false;
                            caretLine = null;
                            targetIndex = -1;
                        }
                    }, TrickleDown.TrickleDown);

                    btn.RegisterCallback<PointerMoveEvent>(evt =>
                    {
                        if (draggedBookmark == btn)
                        {
                            if (!isDragging && Vector2.Distance(startPos, evt.position) > 5f)
                            {
                                isDragging = true;
                                btn.CapturePointer(evt.pointerId);
                                btn.style.opacity = 0.8f;
                                
                                caretLine = new VisualElement();
                                caretLine.style.position = Position.Absolute;
                                caretLine.style.width = 2;
                                caretLine.style.height = btn.layout.height;
                                caretLine.style.backgroundColor = Color.white;
                                caretLine.style.top = btn.layout.y;
                                
                                btn.parent.Add(caretLine);
                                btn.style.position = Position.Absolute;
                                btn.BringToFront();
                            }

                            if (isDragging && btn.HasPointerCapture(evt.pointerId))
                            {
                                var container = btn.parent;
                                Vector2 localPos = container.WorldToLocal(evt.position);
                                
                                float maxLeft = container.layout.width - btn.layout.width;
                                float maxTop = container.layout.height - btn.layout.height;
                                float targetLeft = localPos.x - (btn.layout.width / 2);
                                float targetTop = localPos.y - (btn.layout.height / 2);
                                
                                btn.style.left = Mathf.Clamp(targetLeft, 0, maxLeft > 0 ? maxLeft : 0);
                                btn.style.top = Mathf.Clamp(targetTop, 0, maxTop > 0 ? maxTop : 0);

                                List<VisualElement> flexSiblings = new List<VisualElement>();
                                for (int i = 0; i < container.childCount; i++)
                                {
                                    var child = container.ElementAt(i);
                                    if (child != btn && child != caretLine)
                                    {
                                        flexSiblings.Add(child);
                                    }
                                }

                                bool inserted = false;
                                VisualElement lastSibling = null;
                                targetIndex = 0;
                                float caretX = 0;

                                for (int i = 0; i < flexSiblings.Count; i++)
                                {
                                    var sibling = flexSiblings[i];
                                    if (evt.position.x < sibling.worldBound.center.x)
                                    {
                                        targetIndex = i;
                                        caretX = sibling.layout.x - 2;
                                        inserted = true;
                                        break;
                                    }
                                    lastSibling = sibling;
                                }

                                if (!inserted)
                                {
                                    targetIndex = flexSiblings.Count;
                                    if (lastSibling != null)
                                        caretX = lastSibling.layout.x + lastSibling.layout.width + 2;
                                    else
                                        caretX = 0;
                                }

                                caretLine.style.left = caretX;
                                caretLine.BringToFront();
                                btn.BringToFront();
                            }
                        }
                    }, TrickleDown.TrickleDown);

                    // Styling
                    btn.style.marginRight = 2;
                    btn.style.marginLeft = 2;
                    btn.style.paddingLeft = 8;
                    btn.style.paddingRight = 8;
                    
                    if (config.color != Color.clear)
                    {
                        btn.style.backgroundColor = config.color;
                    }
                    else
                    {
                        btn.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f);
                    }
                    
                    btn.style.borderTopLeftRadius = 3;
                    btn.style.borderTopRightRadius = 3;
                    btn.style.borderBottomLeftRadius = 3;
                    btn.style.borderBottomRightRadius = 3;
                    btn.tooltip = path + "\nDrag to reorder. Right-click for options.";
                    
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
