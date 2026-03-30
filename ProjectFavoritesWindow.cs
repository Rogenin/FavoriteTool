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

        private const float ItemIconSize = 16f;
        private const float ItemLeftPadding = 8f;
        private const float ItemColorBarWidth = 4f;
        private const float ItemBackgroundInset = 1f;

        private const float ControlButtonSize = 22f;
        private const float DragHandleWidth = 26f;
        private const float DragHandleHeight = 22f;
        private const float ControlSpacing = 3f;
        private const float ControlBlockToContentSpacing = 6f;
        private const float IconToNameSpacing = 5f;
        private const float NameToPathSpacing = 8f;
        private const float NameColumnWidth = 150f;

        private const float SearchFieldMinWidth = 140f;
        private const float SearchFieldMaxWidth = 280f;

        private const float MissingRowRemoveButtonWidth = 90f;

        // =========================================================================
        // GUI Style Names
        // =========================================================================

        private const string BoxStyleName = "box";

        // =========================================================================
        // Color Values
        // =========================================================================

        private const float ItemBackgroundColorAlpha = 0.35f;
        private const float SelectedOverlayAlpha = 0.18f;

        private static readonly Color DefaultFavoriteColor = new Color32(56, 56, 56, 255);
        private static readonly Color PinnedButtonColor = new Color32(255, 255, 0, 255);
        private static readonly Color DragMarkerColor = new Color(0.28f, 0.65f, 1.00f, 1.00f);
        private static readonly Color SelectedOutlineColor = new Color(0.35f, 0.70f, 1.00f, 1.00f);

        private static readonly Color DragHandleTint = new Color(0.92f, 0.92f, 0.92f, 1.00f);
        private static readonly Color DragGripColor = new Color(0.36f, 0.36f, 0.36f, 1.00f);
        private static readonly Color DragHandleActiveTint = new Color(0.78f, 0.88f, 1.00f, 1.00f);

        // =========================================================================
        // Save Path
        // =========================================================================

        private const string RelativeStateFilePath = "ProjectSettings/ProjectFavoritesWindowState.json";

        [Serializable]
        private class FavoriteItem
        {
            public string guid;
            public Color color = default;
            public bool pinned;
        }

        [Serializable]
        private class FavoritesState
        {
            public List<FavoriteItem> items = new List<FavoriteItem>();
        }

        private Vector2 _scroll;
        private string _search = string.Empty;
        private readonly List<FavoriteItem> _items = new List<FavoriteItem>();

        private bool _isDraggingItem;
        private int _draggedActualIndex = -1;
        private bool _draggedPinned;
        private int _dragTargetActualIndex = -1;
        private bool _dragInsertAfter;

        private GUIStyle _iconButtonStyle;
        private GUIStyle _dragHandleStyle;

        private static GUIContent _pinIcon;
        private static GUIContent _removeIcon;

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
            EnsureIcons();

            _iconButtonStyle = null;
            _dragHandleStyle = null;
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
            EnsureIcons();

            if (!EnsureStyles())
            {
                Repaint();
                return;
            }

            DrawToolbar();
            EditorGUILayout.Space(ToolbarBottomSpacing);

            DrawDropArea();
            EditorGUILayout.Space(DropAreaBottomSpacing);

            DrawFavoritesList();
        }

        private bool EnsureStyles()
        {
            if (_iconButtonStyle != null && _dragHandleStyle != null)
                return true;

            if (GUI.skin == null || GUI.skin.button == null)
                return false;

            GUIStyle baseButtonStyle = GUI.skin.button;

            if (_iconButtonStyle == null)
            {
                _iconButtonStyle = new GUIStyle(baseButtonStyle)
                {
                    fixedWidth = 0f,
                    fixedHeight = 0f,
                    padding = new RectOffset(3, 3, 3, 3),
                    margin = new RectOffset(0, 0, 0, 0),
                    alignment = TextAnchor.MiddleCenter,
                    imagePosition = ImagePosition.ImageOnly
                };
            }

            if (_dragHandleStyle == null)
            {
                _dragHandleStyle = new GUIStyle(baseButtonStyle)
                {
                    fixedWidth = 0f,
                    fixedHeight = 0f,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    alignment = TextAnchor.MiddleCenter
                };
            }

            return true;
        }

        private static void EnsureIcons()
        {
            if (_pinIcon == null)
                _pinIcon = GetSafeIconContent("Pin / Unpin", "Favorite", "Favorite Icon");

            if (_removeIcon == null)
                _removeIcon = GetSafeIconContent("Remove from favorites", "TreeEditor.Trash", "Toolbar Minus");
        }

        private static GUIContent GetSafeIconContent(string tooltip, params string[] iconNames)
        {
            for (int i = 0; i < iconNames.Length; i++)
            {
                Texture2D texture = EditorGUIUtility.FindTexture(iconNames[i]);
                if (texture != null)
                    return new GUIContent(texture, tooltip);
            }

            return new GUIContent("•", tooltip);
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
                    color = DefaultFavoriteColor,
                    pinned = false
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

            List<int> orderedIndices = BuildDisplayIndexList();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            int removeIndex = -1;
            int pinToggleIndex = -1;
            bool visualSettingsChanged = false;

            for (int displayIndex = 0; displayIndex < orderedIndices.Count; displayIndex++)
            {
                int actualIndex = orderedIndices[displayIndex];
                FavoriteItem item = _items[actualIndex];
                string assetPath = AssetDatabase.GUIDToAssetPath(item.guid);

                if (string.IsNullOrEmpty(assetPath))
                {
                    DrawMissingItemRow(actualIndex, ref removeIndex);
                    continue;
                }

                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset == null)
                {
                    DrawMissingItemRow(actualIndex, ref removeIndex);
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
                    actualIndex,
                    ref removeIndex,
                    ref pinToggleIndex,
                    ref visualSettingsChanged);
            }

            if (visualSettingsChanged)
                SaveState();

            EditorGUILayout.EndScrollView();

            if (HandleGlobalItemDrag())
                return;

            if (pinToggleIndex >= 0)
            {
                TogglePin(pinToggleIndex);
                GUIUtility.ExitGUI();
            }

            if (removeIndex >= 0)
            {
                _items.RemoveAt(removeIndex);
                SaveState();
                GUIUtility.ExitGUI();
            }
        }

        private List<int> BuildDisplayIndexList()
        {
            List<int> orderedIndices = new List<int>(_items.Count);

            for (int i = 0; i < _items.Count; i++)
                orderedIndices.Add(i);

            orderedIndices.Sort(CompareDisplayIndices);
            return orderedIndices;
        }

        private int CompareDisplayIndices(int firstIndex, int secondIndex)
        {
            bool firstPinned = _items[firstIndex].pinned;
            bool secondPinned = _items[secondIndex].pinned;

            if (firstPinned != secondPinned)
                return firstPinned ? -1 : 1;

            return firstIndex.CompareTo(secondIndex);
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

        private void DrawMissingItemRow(int actualIndex, ref int removeIndex)
        {
            using (new EditorGUILayout.VerticalScope(BoxStyleName))
            {
                EditorGUILayout.LabelField(MissingAssetTitle, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(MissingAssetMessage, EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Remove", GUILayout.Width(MissingRowRemoveButtonWidth)))
                    {
                        removeIndex = actualIndex;
                    }
                }
            }
        }

        private void DrawItemRow(
            FavoriteItem item,
            UnityEngine.Object asset,
            string assetPath,
            int actualIndex,
            ref int removeIndex,
            ref int pinToggleIndex,
            ref bool visualSettingsChanged)
        {
            Rect itemRect = EditorGUILayout.BeginVertical(BoxStyleName);
            bool isSelected = Selection.activeObject == asset;

            DrawItemHighlight(itemRect, item, isSelected);

            Rect dragHandleRect;
            Rect pinButtonRect;
            Rect colorFieldRect;
            Rect removeButtonRect;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(ItemLeftPadding);

                dragHandleRect = GUILayoutUtility.GetRect(
                    DragHandleWidth,
                    DragHandleHeight,
                    GUILayout.Width(DragHandleWidth),
                    GUILayout.Height(DragHandleHeight));

                DrawDragHandle(dragHandleRect, actualIndex, item.pinned);

                GUILayout.Space(ControlSpacing);

                pinButtonRect = GUILayoutUtility.GetRect(
                    ControlButtonSize,
                    ControlButtonSize,
                    GUILayout.Width(ControlButtonSize),
                    GUILayout.Height(ControlButtonSize));

                if (DrawIconButton(pinButtonRect, _pinIcon, item.pinned))
                {
                    pinToggleIndex = actualIndex;
                }

                GUILayout.Space(ControlSpacing);

                colorFieldRect = GUILayoutUtility.GetRect(
                    ControlButtonSize,
                    ControlButtonSize,
                    GUILayout.Width(ControlButtonSize),
                    GUILayout.Height(ControlButtonSize));

                DrawColorButton(colorFieldRect, item, ref visualSettingsChanged);

                GUILayout.Space(ControlSpacing);

                removeButtonRect = GUILayoutUtility.GetRect(
                    ControlButtonSize,
                    ControlButtonSize,
                    GUILayout.Width(ControlButtonSize),
                    GUILayout.Height(ControlButtonSize));

                if (DrawIconButton(removeButtonRect, _removeIcon, false))
                {
                    removeIndex = actualIndex;
                }

                GUILayout.Space(ControlBlockToContentSpacing);

                Texture icon = AssetDatabase.GetCachedIcon(assetPath);
                GUILayout.Label(icon, GUILayout.Width(ItemIconSize), GUILayout.Height(ItemIconSize));

                GUILayout.Space(IconToNameSpacing);

                GUILayout.Label(
                    asset.name,
                    EditorStyles.boldLabel,
                    GUILayout.Width(NameColumnWidth));

                GUILayout.Space(NameToPathSpacing);

                EditorGUILayout.LabelField(assetPath, EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.EndVertical();

            HandleRowClick(
                itemRect,
                asset,
                assetPath,
                dragHandleRect,
                pinButtonRect,
                colorFieldRect,
                removeButtonRect);

            UpdateItemDragTarget(itemRect, actualIndex, item.pinned);
            DrawDragInsertionMarker(itemRect, actualIndex, item.pinned);
        }

        private void DrawColorButton(Rect rect, FavoriteItem item, ref bool visualSettingsChanged)
        {
            EditorGUI.BeginChangeCheck();

            Color newColor = EditorGUI.ColorField(
                rect,
                GUIContent.none,
                item.color,
                showEyedropper: false,
                showAlpha: false,
                hdr: false);

            if (EditorGUI.EndChangeCheck())
            {
                item.color = NormalizeFavoriteColor(newColor);
                visualSettingsChanged = true;
            }
        }

        private bool DrawIconButton(Rect rect, GUIContent content, bool highlighted)
        {
            Color previousBackground = GUI.backgroundColor;

            if (highlighted)
                GUI.backgroundColor = PinnedButtonColor;

            bool clicked = GUI.Button(rect, content, _iconButtonStyle);

            GUI.backgroundColor = previousBackground;
            return clicked;
        }

        private void HandleRowClick(
            Rect itemRect,
            UnityEngine.Object asset,
            string assetPath,
            Rect dragHandleRect,
            Rect pinButtonRect,
            Rect colorFieldRect,
            Rect removeButtonRect)
        {
            Event currentEvent = Event.current;

            if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0)
                return;

            if (!itemRect.Contains(currentEvent.mousePosition))
                return;

            if (dragHandleRect.Contains(currentEvent.mousePosition)
                || pinButtonRect.Contains(currentEvent.mousePosition)
                || colorFieldRect.Contains(currentEvent.mousePosition)
                || removeButtonRect.Contains(currentEvent.mousePosition))
            {
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);

            if (currentEvent.clickCount >= 2)
                OpenAsset(asset, assetPath);

            Repaint();
            currentEvent.Use();
        }

        private void DrawDragHandle(Rect dragHandleRect, int actualIndex, bool pinned)
        {
            EditorGUIUtility.AddCursorRect(dragHandleRect, MouseCursor.Pan);

            Color previousBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = _isDraggingItem && _draggedActualIndex == actualIndex
                ? DragHandleActiveTint
                : DragHandleTint;

            GUI.Box(dragHandleRect, GUIContent.none, _dragHandleStyle);
            GUI.backgroundColor = previousBackgroundColor;

            DrawDragGripLines(dragHandleRect);

            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseDown
                && currentEvent.button == 0
                && dragHandleRect.Contains(currentEvent.mousePosition))
            {
                StartItemDrag(actualIndex, pinned);
                currentEvent.Use();
            }
        }

        private void DrawDragGripLines(Rect dragHandleRect)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            float lineWidth = 10f;
            float lineHeight = 1.6f;
            float lineSpacing = 3.5f;

            float centerX = dragHandleRect.center.x;
            float centerY = dragHandleRect.center.y;

            float startY = centerY - lineSpacing;
            float lineX = centerX - lineWidth * 0.5f;

            Rect line1 = new Rect(lineX, startY, lineWidth, lineHeight);
            Rect line2 = new Rect(lineX, centerY - lineHeight * 0.5f, lineWidth, lineHeight);
            Rect line3 = new Rect(lineX, centerY + lineSpacing - lineHeight, lineWidth, lineHeight);

            EditorGUI.DrawRect(line1, DragGripColor);
            EditorGUI.DrawRect(line2, DragGripColor);
            EditorGUI.DrawRect(line3, DragGripColor);
        }

        private void StartItemDrag(int actualIndex, bool pinned)
        {
            _isDraggingItem = true;
            _draggedActualIndex = actualIndex;
            _draggedPinned = pinned;
            _dragTargetActualIndex = actualIndex;
            _dragInsertAfter = false;
            Repaint();
        }

        private void UpdateItemDragTarget(Rect rowRect, int actualIndex, bool pinned)
        {
            if (!_isDraggingItem)
                return;

            if (_draggedPinned != pinned)
                return;

            if (!rowRect.Contains(Event.current.mousePosition))
                return;

            _dragTargetActualIndex = actualIndex;
            _dragInsertAfter = Event.current.mousePosition.y >= rowRect.center.y;
            Repaint();
        }

        private void DrawDragInsertionMarker(Rect rowRect, int actualIndex, bool pinned)
        {
            if (!_isDraggingItem)
                return;

            if (_draggedPinned != pinned)
                return;

            if (actualIndex != _dragTargetActualIndex)
                return;

            if (actualIndex == _draggedActualIndex)
                return;

            if (Event.current.type != EventType.Repaint)
                return;

            float markerY = _dragInsertAfter
                ? rowRect.yMax - 2f
                : rowRect.yMin + 1f;

            Rect markerRect = new Rect(rowRect.x + 6f, markerY, rowRect.width - 12f, 2f);
            EditorGUI.DrawRect(markerRect, DragMarkerColor);
        }

        private bool HandleGlobalItemDrag()
        {
            if (!_isDraggingItem)
                return false;

            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape)
            {
                ClearItemDragState();
                Repaint();
                currentEvent.Use();
                return false;
            }

            if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
            {
                bool moved = MoveDraggedItemIfNeeded();
                ClearItemDragState();
                currentEvent.Use();

                if (moved)
                {
                    SaveState();
                    Repaint();
                    GUIUtility.ExitGUI();
                    return true;
                }

                Repaint();
            }

            return false;
        }

        private bool MoveDraggedItemIfNeeded()
        {
            if (_draggedActualIndex < 0 || _dragTargetActualIndex < 0)
                return false;

            if (_draggedActualIndex == _dragTargetActualIndex)
                return false;

            if (_draggedActualIndex >= _items.Count || _dragTargetActualIndex >= _items.Count)
                return false;

            if (_items[_draggedActualIndex].pinned != _items[_dragTargetActualIndex].pinned)
                return false;

            FavoriteItem draggedItem = _items[_draggedActualIndex];
            bool pinned = draggedItem.pinned;

            _items.RemoveAt(_draggedActualIndex);

            int targetIndex = _dragTargetActualIndex;
            if (_draggedActualIndex < targetIndex)
                targetIndex--;

            int insertIndex = _dragInsertAfter ? targetIndex + 1 : targetIndex;

            GetGroupBounds(pinned, out int groupStart, out int groupEndExclusive);
            insertIndex = Mathf.Clamp(insertIndex, groupStart, groupEndExclusive);

            _items.Insert(insertIndex, draggedItem);
            NormalizePinnedOrder();

            return true;
        }

        private void ClearItemDragState()
        {
            _isDraggingItem = false;
            _draggedActualIndex = -1;
            _dragTargetActualIndex = -1;
            _draggedPinned = false;
            _dragInsertAfter = false;
        }

        private void DrawItemHighlight(Rect itemRect, FavoriteItem item, bool isSelected)
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

            if (isSelected)
            {
                Color selectedFill = SelectedOutlineColor;
                selectedFill.a = SelectedOverlayAlpha;
                EditorGUI.DrawRect(backgroundRect, selectedFill);

                Rect top = new Rect(backgroundRect.x, backgroundRect.y, backgroundRect.width, 1f);
                Rect bottom = new Rect(backgroundRect.x, backgroundRect.yMax - 1f, backgroundRect.width, 1f);
                Rect left = new Rect(backgroundRect.x, backgroundRect.y, 1f, backgroundRect.height);
                Rect right = new Rect(backgroundRect.xMax - 1f, backgroundRect.y, 1f, backgroundRect.height);

                EditorGUI.DrawRect(top, SelectedOutlineColor);
                EditorGUI.DrawRect(bottom, SelectedOutlineColor);
                EditorGUI.DrawRect(left, SelectedOutlineColor);
                EditorGUI.DrawRect(right, SelectedOutlineColor);
            }
        }

        private void TogglePin(int actualIndex)
        {
            if (actualIndex < 0 || actualIndex >= _items.Count)
                return;

            FavoriteItem item = _items[actualIndex];
            _items.RemoveAt(actualIndex);

            item.pinned = !item.pinned;

            if (item.pinned)
            {
                _items.Insert(0, item);
            }
            else
            {
                int pinnedCount = GetPinnedCount();
                _items.Insert(pinnedCount, item);
            }

            NormalizePinnedOrder();
            SaveState();
        }

        private int GetPinnedCount()
        {
            int pinnedCount = 0;

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].pinned)
                    pinnedCount++;
            }

            return pinnedCount;
        }

        private void GetGroupBounds(bool pinned, out int start, out int endExclusive)
        {
            int pinnedCount = GetPinnedCount();

            if (pinned)
            {
                start = 0;
                endExclusive = pinnedCount;
            }
            else
            {
                start = pinnedCount;
                endExclusive = _items.Count;
            }
        }

        private void NormalizePinnedOrder()
        {
            List<FavoriteItem> pinnedItems = new List<FavoriteItem>();
            List<FavoriteItem> normalItems = new List<FavoriteItem>();

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].pinned)
                    pinnedItems.Add(_items[i]);
                else
                    normalItems.Add(_items[i]);
            }

            _items.Clear();
            _items.AddRange(pinnedItems);
            _items.AddRange(normalItems);
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

                NormalizePinnedOrder();
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
    }
}