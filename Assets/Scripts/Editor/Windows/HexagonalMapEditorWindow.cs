using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

#if UNITY_EDITOR
namespace Editor.Windows
{
    public class HexagonalMapEditorWindow : EditorWindow
    {
        [MenuItem("Window/Hex Map Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<HexagonalMapEditorWindow>("Hex Map Editor");
            window.minSize = new Vector2(300, 400);
            window.Show();
        }
    
        private HexagonalMapEditor _selectedMapEditor;
        private Vector2 _scrollPosition;
        private readonly List<PrefabEntry> _prefabEntries = new List<PrefabEntry>();
        private readonly Dictionary<string, Texture2D> _thumbnailCache = new Dictionary<string, Texture2D>();
        private string _filterLabel = "MapObject";
        private PrefabEntry _selectedPrefab;
        private bool _showPreviews = true;
    
        private struct PrefabEntry
        {
            public string Address;
            public string DisplayName;
            public string AssetPath;
            public string Guid;
            public GameObject Prefab;
            public AssetReference AssetReference;
        }
    
        private void OnEnable()
        {
            // Subscribe to selection changes
            Selection.selectionChanged += OnSelectionChanged;
            SceneView.duringSceneGui += OnSceneGUI;
            RefreshPrefabList();
        }
    
        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            SceneView.duringSceneGui -= OnSceneGUI;
        }
    
        private void OnSelectionChanged()
        {
            if (Selection.activeGameObject == null) return;

            var gridEditor = Selection.activeGameObject.GetComponent<HexagonalMapEditor>();
            if (gridEditor != _selectedMapEditor)
            {
                _selectedMapEditor = gridEditor;
                Repaint();
            }
        }
    
        private void OnSceneGUI(SceneView sceneView)
        {
            if (_selectedMapEditor != null && _selectedPrefab.Prefab != null)
            {
                HandleSceneViewPlacement();
            }
        }
    
        private void HandleSceneViewPlacement()
        {
            Event e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0 || e.alt) return;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (_selectedMapEditor.TryGetCellAtWorldPosition(ray, out var cellCoord))
            {
                _selectedMapEditor.SetCellContent(cellCoord, _selectedPrefab.AssetReference);
                e.Use();
                Repaint();
            }
        }
    
        private void OnGUI()
        {
            DrawHeader();
            DrawMapEditorStatus();
            DrawPrefabFilters();
            DrawPrefabList();
            DrawSelectedCellInfo();
        }
    
        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Hex Map Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space();
        }
    
        private void DrawMapEditorStatus()
        {
            if (_selectedMapEditor == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject with HexagonalGridEditor component", MessageType.Info);
                return;
            }
        
            EditorGUILayout.LabelField($"Map: {_selectedMapEditor.name}", EditorStyles.boldLabel);
        
            if (_selectedMapEditor.SelectedCell.HasValue)
            {
                var coord = _selectedMapEditor.SelectedCell.Value;
                EditorGUILayout.LabelField($"Selected Cell: ({coord.x}, {coord.y})");
            
                var currentContent = _selectedMapEditor.GetCellContent(coord);
                var contentName = currentContent?.RuntimeKeyIsValid() == true ? "Assigned" : "Empty";
                EditorGUILayout.LabelField($"Current Content: {contentName}");
            }
            else
            {
                EditorGUILayout.LabelField("Selected Cell: None");
            }
        
            EditorGUILayout.Space();
        }
    
        private void DrawPrefabFilters()
        {
            EditorGUILayout.LabelField("Prefab Library", EditorStyles.boldLabel);
        
            EditorGUI.BeginChangeCheck();
            _filterLabel = EditorGUILayout.TextField("Filter Label", _filterLabel);
            if (EditorGUI.EndChangeCheck())
            {
                RefreshPrefabList();
            }
        
            if (GUILayout.Button("Refresh Prefabs"))
            {
                RefreshPrefabList();
            }
        
            _showPreviews = EditorGUILayout.Toggle("Show Previews", _showPreviews);
        
            EditorGUILayout.Space();
        }
    
        private void DrawPrefabList()
        {
            EditorGUILayout.LabelField("Available Prefabs", EditorStyles.boldLabel);
        
            // Clear selection button
            if (GUILayout.Button("Clear Selection"))
            {
                _selectedPrefab = default;
                if (_selectedMapEditor != null && _selectedMapEditor.SelectedCell.HasValue)
                {
                    _selectedMapEditor.SetCellContent(_selectedMapEditor.SelectedCell.Value, null);
                }
            }
        
            EditorGUILayout.Space();
        
            // Scrollable prefab list
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        
            foreach (var entry in _prefabEntries)
            {
                if (DrawPrefabEntry(entry))
                {
                    _selectedPrefab = entry;
                
                    // If we have a selected cell, assign immediately
                    if (_selectedMapEditor != null && _selectedMapEditor.SelectedCell.HasValue)
                    {
                        _selectedMapEditor.SetCellContent(_selectedMapEditor.SelectedCell.Value, entry.AssetReference);
                    }
                }
            }
        
            EditorGUILayout.EndScrollView();
        }
    
        private bool DrawPrefabEntry(PrefabEntry entry)
        {
            var isSelected = _selectedPrefab.Guid == entry.Guid;
            var buttonHeight = _showPreviews ? 60f : 30f;
        
            var rect = GUILayoutUtility.GetRect(0, buttonHeight, GUILayout.ExpandWidth(true));
        
            // Background
            var bgColor = isSelected ? new Color(0.3f, 0.5f, 0.8f, 0.5f) : 
                rect.Contains(Event.current.mousePosition) ? new Color(0.3f, 0.5f, 0.8f, 0.3f) : 
                new Color(0.2f, 0.2f, 0.2f, 0.1f);
            EditorGUI.DrawRect(rect, bgColor);
        
            if (_showPreviews)
            {
                // Icon
                var iconSize = 50f;
                var iconRect = new Rect(rect.x + 5, rect.y + 5, iconSize, iconSize);
            
                var thumbnail = GetThumbnail(entry.Prefab);
                if (thumbnail != null)
                {
                    GUI.DrawTexture(iconRect, thumbnail, ScaleMode.ScaleToFit);
                }
                else
                {
                    var content = EditorGUIUtility.IconContent("Prefab Icon");
                    GUI.DrawTexture(iconRect, content.image, ScaleMode.ScaleToFit);
                }
            
                // Text
                var textRect = new Rect(iconRect.xMax + 10, rect.y + 5, rect.width - iconRect.width - 20, rect.height - 10);
            
                var style = new GUIStyle(GUI.skin.label);
                style.fontSize = 12;
                style.fontStyle = FontStyle.Bold;
                GUI.Label(textRect, entry.DisplayName, style);
            
                // Path
                var pathRect = new Rect(textRect.x, textRect.y + 20, textRect.width, 15);
                style.fontSize = 9;
                style.fontStyle = FontStyle.Normal;
                style.normal.textColor = Color.gray;
                GUI.Label(pathRect, entry.AssetPath, style);
            }
            else
            {
                // Simple text button
                var textRect = new Rect(rect.x + 10, rect.y + 5, rect.width - 20, rect.height - 10);
                var style = new GUIStyle(GUI.skin.label);
                style.fontSize = 12;
                style.fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
                GUI.Label(textRect, entry.DisplayName, style);
            }
        
            // Handle click
            return Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition);
        }
    
        private void DrawSelectedCellInfo()
        {
            if (_selectedMapEditor == null || !_selectedMapEditor.SelectedCell.HasValue)
                return;
        
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selected Cell Actions", EditorStyles.boldLabel);
        
            var coord = _selectedMapEditor.SelectedCell.Value;
        
            if (GUILayout.Button("Clear Cell"))
            {
                _selectedMapEditor.SetCellContent(coord, null);
            }
        
            if (_selectedPrefab.Prefab != null)
            {
                if (GUILayout.Button($"Place: {_selectedPrefab.DisplayName}"))
                {
                    _selectedMapEditor.SetCellContent(coord, _selectedPrefab.AssetReference);
                }
            }
        }
    
        private void RefreshPrefabList()
        {
            _prefabEntries.Clear();
        
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                foreach (var group in settings.groups)
                {
                    if (group == null) continue;
                
                    foreach (var entry in group.entries)
                    {
                        if (entry == null) continue;

                        if (!string.IsNullOrEmpty(_filterLabel) && !entry.labels.Contains(_filterLabel)) continue;

                        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(entry.AssetPath);
                        if (asset == null) continue;

                        var assetRef = new AssetReference(entry.guid);
                        _prefabEntries.Add(new PrefabEntry
                        {
                            Address = entry.address,
                            DisplayName = System.IO.Path.GetFileNameWithoutExtension(entry.AssetPath),
                            AssetPath = entry.AssetPath,
                            Guid = entry.guid,
                            Prefab = asset,
                            AssetReference = assetRef
                        });
                    }
                }
            }
        
            _prefabEntries = _prefabEntries.OrderBy(e => e.DisplayName).ToList();
        }
    
        private Texture2D GetThumbnail(GameObject prefab)
        {
            var assetPath = AssetDatabase.GetAssetPath(prefab);
        
            if (_thumbnailCache.TryGetValue(assetPath, out var cachedThumbnail))
            {
                return cachedThumbnail;
            }
        
            var thumbnail = AssetPreview.GetAssetPreview(prefab);
            if (thumbnail != null)
            {
                _thumbnailCache[assetPath] = thumbnail;
            }
        
            return thumbnail;
        }
    }
}
#endif