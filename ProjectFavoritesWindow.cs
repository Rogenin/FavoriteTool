using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FavoriteTool
{
    public class ProjectFavoritesWindow : EditorWindow
    {
        // =========================================================================
        // Menu / Window
        // =========================================================================

        private const string MenuItemPath = "Tools/Favorites";
        private const string WindowTitle = "Favorites";

        private const float WindowMinWidth = 420f;
        private const float WindowMinHeight = 300f;
        private static readonly Vector2 WindowMinSize = new Vector2(WindowMinWidth, WindowMinHeight);

        // =========================================================================
        // Toolbar Text
        // =========================================================================

        private const string ToolbarTitle = "Favorites";
        private const string ClearSearchButtonText = "X";
        private const string ClearAllButtonText = "Clear All";
        private const string ToolbarSearchTextFieldStyleName = "ToolbarSeachTextField";

        // =========================================================================
        // Dialog Text
        // =========================================================================

        private const string ClearFavoritesDialogTitle = "Clear favorites?";
        private const string ClearFavoritesDialogMessage = "Remove all items from favorites?";
        private const string YesButtonText = "Yes";
        private const string NoButtonText = "No";

        // =========================================================================
        // Drop Area / Empty State / Missing State Text
        // =========================================================================

        private const string DropAreaText = "Drag folders, scripts, prefabs, materials, and other assets here";
        private const string EmptyListMessage = "The list is empty. Drag objects ↑ here from the Project window.";
        private const string MissingAssetTitle = "[Missing Asset]";
        private const string MissingAssetMessage = "The file was deleted or moved without preserving the correct GUID.";

        // =========================================================================
        // Row Button Text
        // =========================================================================

        private const string SelectButtonText = "Select";
        private const string OpenButtonText = "Open";
        private const string RemoveButtonText = "Remove";
        private const string MoveUpButtonText = "↑";
        private const string MoveDownButtonText = "↓";

        // =========================================================================
        // Internal Unity Reflection Names
        // =========================================================================

        private const string ProjectWindowMenuPath = "Window/General/Project";
        private const string ProjectBrowserTypeName = "UnityEditor.ProjectBrowser";
        private const string ShowFolderContentsMethodName = "ShowFolderContents";

        // =========================================================================
        // Layout Values
        // =========================================================================

        private const float ToolbarTitleWidth = 80f;
        private const float ToolbarSpacing = 3f;
        private const float ClearSearchButtonWidth = 24f;
        private const float ClearAllButtonWidth = 100f;
        private const float ToolbarBottomSpacing = 6f;
        private const float DropAreaHeight = 35f;
        private const float DropAreaBottomSpacing = 8f;

        private const float ItemIconSize = 18f;
        private const float ItemContentSpacing = 3f;
        private const float MissingRowRemoveButtonWidth = 90f;
        private const float MoveButtonWidth = 20f;
        private const float RemoveButtonWidth = 68f;
        private const float SelectButtonWidth = 52f;
        private const float OpenButtonWidth = 52f;

        private const float ItemLeftPadding = 8f;
        private const float ItemColorBarWidth = 4f;
        private const float ItemBackgroundInset = 1f;
        private const float CompactColorFieldWidth = 18f;
        private const float CompactColorFieldHeight = 16f;

        private const float SearchFieldMinWidth = 140f;
        private const float SearchFieldMaxWidth = 280f;

        // =========================================================================
        // GUI Style Names
        // =========================================================================

        private const string BoxStyleName = "box";

        // =========================================================================
        // Color Values
        // =========================================================================

        private const float ItemBackgroundColorAlpha = 0.35f;

        // #383838
        private static readonly Color DefaultFavoriteColor = new Color32(56, 56, 56, 255);

        // =========================================================================
        // Save Path
        // =========================================================================

        private const string RelativeStateFilePath = "ProjectSettings/ProjectFavoritesWindowState.json";

        [Serializable]
        private class FavoriteItem
        {
            public string guid;
            public Color color = default;
        }

        [Serializable]
        private class FavoritesState
        {
            public List<FavoriteItem> items = new List<FavoriteItem>();
        }

        private Vector2 _scroll;
        private string _search = string.Empty;
        private readonly List<FavoriteItem> _items = new List<FavoriteItem>();

        private static string StateFilePath
        {
            get
            {
                return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), RelativeStateFilePath));
            }
        }

        [MenuItem(MenuItemPath)]
        public static void ShowWindow()
        {
            ProjectFavoritesWindow window = GetWindow<ProjectFavoritesWindow>(WindowTitle);
            window.minSize = WindowMinSize;
            window.Show();
        }

        private void OnEnable()
        {
            minSize = WindowMinSize;
            LoadState();
        }

        private void OnDisable()
        {
            SaveState();
        }

        private void OnDestroy()
        {
            SaveState();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(ToolbarBottomSpacing);

            DrawDropArea();
            EditorGUILayout.Space(DropAreaBottomSpacing);

            DrawFavoritesList();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(ToolbarTitle, GUILayout.Width(ToolbarTitleWidth));
                GUILayout.Space(ToolbarSpacing);

                GUIStyle searchStyle = GUI.skin.FindStyle(ToolbarSearchTextFieldStyleName) ?? EditorStyles.toolbarTextField;

                _search = GUILayout.TextField(
                    _search,
                    searchStyle,
                    GUILayout.MinWidth(SearchFieldMinWidth),
                    GUILayout.MaxWidth(SearchFieldMaxWidth),
                    GUILayout.ExpandWidth(true));

                if (GUILayout.Button(
                        ClearSearchButtonText,
                        EditorStyles.toolbarButton,
                        GUILayout.Width(ClearSearchButtonWidth)))
                {
                    _search = string.Empty;
                    GUI.FocusControl(null);
                }

                GUILayout.Space(4f);

                if (GUILayout.Button(
                        ClearAllButtonText,
                        EditorStyles.toolbarButton,
                        GUILayout.Width(ClearAllButtonWidth)))
                {
                    bool shouldClear = EditorUtility.DisplayDialog(
                        ClearFavoritesDialogTitle,
                        ClearFavoritesDialogMessage,
                        YesButtonText,
                        NoButtonText);

                    if (shouldClear)
                    {
                        _items.Clear();
                        SaveState();
                        Repaint();
                    }
                }
            }
        }

        private void DrawDropArea()
        {
            Rect dropArea = GUILayoutUtility.GetRect(0f, DropAreaHeight, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, DropAreaText);

            Event currentEvent = Event.current;
            if (!dropArea.Contains(currentEvent.mousePosition))
                return;

            switch (currentEvent.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    {
                        bool hasValidAssets = HasValidDraggedAssets();
                        DragAndDrop.visualMode = hasValidAssets
                            ? DragAndDropVisualMode.Copy
                            : DragAndDropVisualMode.Rejected;

                        if (currentEvent.type == EventType.DragPerform && hasValidAssets)
                        {
                            DragAndDrop.AcceptDrag();
                            AddDraggedAssets();
                        }

                        currentEvent.Use();
                        break;
                    }
            }
        }

        private bool HasValidDraggedAssets()
        {
            foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
            {
                if (draggedObject == null)
                    continue;

                string assetPath = AssetDatabase.GetAssetPath(draggedObject);
                if (!string.IsNullOrEmpty(assetPath))
                    return true;
            }

            return false;
        }

        private void AddDraggedAssets()
        {
            bool hasChanges = false;

            foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
            {
                if (draggedObject == null)
                    continue;

                string assetPath = AssetDatabase.GetAssetPath(draggedObject);
                if (string.IsNullOrEmpty(assetPath))
                    continue;

                string assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(assetGuid))
                    continue;

                if (ContainsGuid(assetGuid))
                    continue;

                FavoriteItem item = new FavoriteItem
                {
                    guid = assetGuid,
                    color = DefaultFavoriteColor
                };

                _items.Add(item);
                hasChanges = true;
            }

            if (!hasChanges)
                return;

            SaveState();
            Repaint();
        }

        private bool ContainsGuid(string guid)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].guid == guid)
                    return true;
            }

            return false;
        }

        private void DrawFavoritesList()
        {
            if (_items.Count == 0)
            {
                EditorGUILayout.HelpBox(EmptyListMessage, MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            int removeIndex = -1;
            int moveUpIndex = -1;
            int moveDownIndex = -1;
            bool visualSettingsChanged = false;

            for (int i = 0; i < _items.Count; i++)
            {
                FavoriteItem item = _items[i];
                string assetPath = AssetDatabase.GUIDToAssetPath(item.guid);

                if (string.IsNullOrEmpty(assetPath))
                {
                    DrawMissingItemRow(i, ref removeIndex);
                    continue;
                }

                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset == null)
                {
                    DrawMissingItemRow(i, ref removeIndex);
                    continue;
                }

                string assetName = asset.name;
                string assetTypeName = asset.GetType().Name;

                if (!MatchesSearch(assetName, assetPath, assetTypeName))
                    continue;

                DrawItemRow(
                    item,
                    asset,
                    assetPath,
                    i,
                    ref removeIndex,
                    ref moveUpIndex,
                    ref moveDownIndex,
                    ref visualSettingsChanged);
            }

            if (visualSettingsChanged)
            {
                SaveState();
            }

            HandleListActions(removeIndex, moveUpIndex, moveDownIndex);

            EditorGUILayout.EndScrollView();
        }

        private bool MatchesSearch(string assetName, string assetPath, string assetTypeName)
        {
            if (string.IsNullOrWhiteSpace(_search))
                return true;

            string searchLower = _search.ToLowerInvariant();

            bool nameMatches = assetName.ToLowerInvariant().Contains(searchLower);
            bool pathMatches = assetPath.ToLowerInvariant().Contains(searchLower);
            bool typeMatches = assetTypeName.ToLowerInvariant().Contains(searchLower);

            return nameMatches || pathMatches || typeMatches;
        }

        private void HandleListActions(int removeIndex, int moveUpIndex, int moveDownIndex)
        {
            if (removeIndex >= 0)
            {
                _items.RemoveAt(removeIndex);
                SaveState();
                GUIUtility.ExitGUI();
            }

            if (moveUpIndex > 0)
            {
                Swap(_items, moveUpIndex, moveUpIndex - 1);
                SaveState();
                GUIUtility.ExitGUI();
            }

            if (moveDownIndex >= 0 && moveDownIndex < _items.Count - 1)
            {
                Swap(_items, moveDownIndex, moveDownIndex + 1);
                SaveState();
                GUIUtility.ExitGUI();
            }
        }

        private void DrawMissingItemRow(int index, ref int removeIndex)
        {
            using (new EditorGUILayout.VerticalScope(BoxStyleName))
            {
                EditorGUILayout.LabelField(MissingAssetTitle, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(MissingAssetMessage, EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(RemoveButtonText, GUILayout.Width(MissingRowRemoveButtonWidth)))
                    {
                        removeIndex = index;
                    }
                }
            }
        }

        private void DrawItemRow(
            FavoriteItem item,
            UnityEngine.Object asset,
            string assetPath,
            int index,
            ref int removeIndex,
            ref int moveUpIndex,
            ref int moveDownIndex,
            ref bool visualSettingsChanged)
        {
            Texture icon = AssetDatabase.GetCachedIcon(assetPath);

            Rect itemRect = EditorGUILayout.BeginVertical(BoxStyleName);
            DrawItemHighlight(itemRect, item);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(ItemLeftPadding);

                GUILayout.Label(icon, GUILayout.Width(ItemIconSize), GUILayout.Height(ItemIconSize));

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(asset.name, EditorStyles.boldLabel);
                    EditorGUILayout.SelectableLabel(
                        assetPath,
                        EditorStyles.textField,
                        GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }

                GUILayout.Space(4f);

                DrawCompactColorField(item, ref visualSettingsChanged);
            }

            EditorGUILayout.Space(ItemContentSpacing);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(ItemLeftPadding);

                if (GUILayout.Button(SelectButtonText, GUILayout.Width(SelectButtonWidth)))
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }

                if (GUILayout.Button(OpenButtonText, GUILayout.Width(OpenButtonWidth)))
                {
                    OpenAsset(asset, assetPath);
                }

                GUI.enabled = index > 0;
                if (GUILayout.Button(MoveUpButtonText, GUILayout.Width(MoveButtonWidth)))
                {
                    moveUpIndex = index;
                }

                GUI.enabled = index < _items.Count - 1;
                if (GUILayout.Button(MoveDownButtonText, GUILayout.Width(MoveButtonWidth)))
                {
                    moveDownIndex = index;
                }

                GUI.enabled = true;

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(RemoveButtonText, GUILayout.Width(RemoveButtonWidth)))
                {
                    removeIndex = index;
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCompactColorField(FavoriteItem item, ref bool visualSettingsChanged)
        {
            EditorGUI.BeginChangeCheck();

            Color newColor = EditorGUILayout.ColorField(
                GUIContent.none,
                item.color,
                showEyedropper: false,
                showAlpha: false,
                hdr: false,
                GUILayout.Width(CompactColorFieldWidth),
                GUILayout.Height(CompactColorFieldHeight));

            if (EditorGUI.EndChangeCheck())
            {
                item.color = NormalizeFavoriteColor(newColor);
                visualSettingsChanged = true;
            }
        }

        private void DrawItemHighlight(Rect itemRect, FavoriteItem item)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            float inset = ItemBackgroundInset;

            Color backgroundColor = item.color;
            backgroundColor.a = ItemBackgroundColorAlpha;

            Color accentColor = item.color;
            accentColor.a = 1f;

            Rect backgroundRect = new Rect(
                itemRect.x + inset,
                itemRect.y + inset,
                itemRect.width - inset * 2f,
                itemRect.height - inset * 2f);

            Rect accentRect = new Rect(
                itemRect.x + inset,
                itemRect.y + inset,
                ItemColorBarWidth,
                itemRect.height - inset * 2f);

            EditorGUI.DrawRect(backgroundRect, backgroundColor);
            EditorGUI.DrawRect(accentRect, accentColor);
        }

        private static Color NormalizeFavoriteColor(Color color)
        {
            if (ApproximatelyBlack(color))
                return DefaultFavoriteColor;

            color.a = 1f;
            return color;
        }

        private static bool ApproximatelyBlack(Color color)
        {
            return Mathf.Approximately(color.r, 0f)
                   && Mathf.Approximately(color.g, 0f)
                   && Mathf.Approximately(color.b, 0f);
        }

        private void OpenAsset(UnityEngine.Object asset, string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                OpenFolderInProjectWindow(asset);
                return;
            }

            AssetDatabase.OpenAsset(asset);
        }

        private void OpenFolderInProjectWindow(UnityEngine.Object folderAsset)
        {
            if (folderAsset == null)
                return;

            var folderEntityId = folderAsset.GetEntityId();

            Selection.entityIds = new[] { folderEntityId };
            EditorUtility.FocusProjectWindow();

            Assembly editorAssembly = typeof(Editor).Assembly;
            Type projectBrowserType = editorAssembly.GetType(ProjectBrowserTypeName);

            if (projectBrowserType == null)
            {
                EditorGUIUtility.PingObject(folderAsset);
                return;
            }

            UnityEngine.Object[] projectBrowsers = Resources.FindObjectsOfTypeAll(projectBrowserType);
            if (projectBrowsers == null || projectBrowsers.Length == 0)
            {
                EditorApplication.ExecuteMenuItem(ProjectWindowMenuPath);
                projectBrowsers = Resources.FindObjectsOfTypeAll(projectBrowserType);
            }

            if (projectBrowsers == null || projectBrowsers.Length == 0)
            {
                EditorGUIUtility.PingObject(folderAsset);
                return;
            }

            object projectBrowser = projectBrowsers[0];

            MethodInfo showFolderContentsMethod = projectBrowserType.GetMethod(
                ShowFolderContentsMethodName,
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { folderEntityId.GetType(), typeof(bool) },
                modifiers: null);

            if (showFolderContentsMethod != null)
            {
                showFolderContentsMethod.Invoke(projectBrowser, new object[]
                {
                folderEntityId,
                true
                });

                return;
            }

            EditorGUIUtility.PingObject(folderAsset);
        }

        private void LoadState()
        {
            _items.Clear();

            if (!File.Exists(StateFilePath))
                return;

            try
            {
                string json = File.ReadAllText(StateFilePath);
                if (string.IsNullOrWhiteSpace(json))
                    return;

                FavoritesState state = JsonUtility.FromJson<FavoritesState>(json);
                if (state == null || state.items == null)
                    return;

                for (int i = 0; i < state.items.Count; i++)
                {
                    FavoriteItem item = state.items[i];
                    if (item == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(item.guid))
                        continue;

                    item.color = NormalizeFavoriteColor(item.color);
                    _items.Add(item);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to load favorites state from '{StateFilePath}'.\n{exception}");
            }
        }

        private void SaveState()
        {
            try
            {
                string directoryPath = Path.GetDirectoryName(StateFilePath);
                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                FavoritesState state = new FavoritesState
                {
                    items = new List<FavoriteItem>(_items)
                };

                string json = JsonUtility.ToJson(state, true);
                File.WriteAllText(StateFilePath, json);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to save favorites state to '{StateFilePath}'.\n{exception}");
            }
        }

        private void Swap<T>(List<T> list, int firstIndex, int secondIndex)
        {
            T temp = list[firstIndex];
            list[firstIndex] = list[secondIndex];
            list[secondIndex] = temp;
        }
    }
}