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
        private List<PrefabEntry> _prefabEntries = new List<PrefabEntry>();
        private readonly Dictionary<string, Texture2D> _thumbnailCache = new Dictionary<string, Texture2D>();
        private string _assetGroup = "HexagonalTiles";
        private PrefabEntry _selectedPrefab;
    
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
            Selection.selectionChanged += OnSelectionChanged;
            RefreshPrefabList();
        }
    
        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            
            _selectedMapEditor = null;
        }
    
        private void OnSelectionChanged()
        {
            if (Selection.activeGameObject == null) return;

            var hexMap = Selection.activeGameObject.GetComponent<HexagonalMap>();
            if (hexMap == null) return;

            var editors = Resources.FindObjectsOfTypeAll<HexagonalMapEditor>();
            var editor = System.Array.Find(editors, e => e.target == hexMap);
            
            if (editor != _selectedMapEditor)
            {
                if (_selectedMapEditor != null)
                {
                    _selectedMapEditor.SetEditorWindow(null);
                }
            
                _selectedMapEditor = editor;
            
                if (_selectedMapEditor != null)
                {
                    _selectedMapEditor.SetEditorWindow(this);
                }
            
                Repaint();

            }
        }
    
        private void OnGUI()
        {
            if (_selectedMapEditor == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject with a HexagonalMap component", MessageType.Info);
                return;
            }
        
            if (_selectedMapEditor.SelectedCell.HasValue)
            {
                var coords = _selectedMapEditor.SelectedCell.Value;
                EditorGUILayout.LabelField($"Selected Cell: ({coords.Q}, {coords.R})");

                EditorGUILayout.LabelField(_selectedMapEditor.TryGetCell(coords, out HexCell cell)
                    ? $"Content: {cell.Name}"
                    : "Content: Empty");
                
                DrawPrefabFilters();
                DrawPrefabList();
                DrawSelectedCellInfo();
            }
            else
            {
                EditorGUILayout.LabelField("Selected Cell: None");
            }
        
            EditorGUILayout.Space();
        }

        public void OnCellSelectionChanged()
        {
            Repaint();
        }
    
        private void DrawPrefabFilters()
        {
            EditorGUILayout.LabelField("Prefab Library", EditorStyles.boldLabel);
        
            EditorGUI.BeginChangeCheck();
            _assetGroup = EditorGUILayout.TextField("Filter Label", _assetGroup);
            if (EditorGUI.EndChangeCheck())
            {
                RefreshPrefabList();
            }
        
            if (GUILayout.Button("Refresh Prefabs"))
            {
                RefreshPrefabList();
            }
        
            EditorGUILayout.Space();
        }
    
        private void DrawPrefabList()
        {
            EditorGUILayout.LabelField("Available Prefabs", EditorStyles.boldLabel);
        
            if (GUILayout.Button("Clear Selection"))
            {
                _selectedPrefab = default;
                if (_selectedMapEditor != null && _selectedMapEditor.SelectedCell.HasValue)
                {
                    _selectedMapEditor.ClearCellContent(_selectedMapEditor.SelectedCell.Value);
                }
            }
        
            EditorGUILayout.Space();
        
            // Scrollable prefab list
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        
            foreach (var entry in _prefabEntries)
            {
                if (!DrawPrefabEntry(entry)) continue;
                _selectedPrefab = entry;
                
                // If we have a selected cell, assign immediately
                if (_selectedMapEditor != null && _selectedMapEditor.SelectedCell.HasValue)
                {
                    _selectedMapEditor.SetCellContent(_selectedMapEditor.SelectedCell.Value, entry.DisplayName, entry.AssetReference);
                }
            }
        
            EditorGUILayout.EndScrollView();
        }
    
        private bool DrawPrefabEntry(PrefabEntry entry)
        {
            var isSelected = _selectedPrefab.Guid == entry.Guid;
            var buttonHeight = 60f;
        
            var rect = GUILayoutUtility.GetRect(0, buttonHeight, GUILayout.ExpandWidth(true));
        
            var bgColor = isSelected ? new Color(0.3f, 0.5f, 0.8f, 0.5f) : 
                rect.Contains(Event.current.mousePosition) ? new Color(0.3f, 0.5f, 0.8f, 0.3f) : 
                new Color(0.2f, 0.2f, 0.2f, 0.1f);
            EditorGUI.DrawRect(rect, bgColor);
        
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
        
            var textRect = new Rect(iconRect.xMax + 10, rect.y, rect.width - iconRect.width - 20, rect.height - 10);
        
            var style = new GUIStyle(GUI.skin.label);
            style.fontSize = 12;
            style.fontStyle = FontStyle.Bold;
            GUI.Label(textRect, entry.DisplayName, style);
        
            var pathRect = new Rect(textRect.x, textRect.y + 35, textRect.width, 15);
            style.fontSize = 9;
            style.fontStyle = FontStyle.Normal;
            style.normal.textColor = Color.gray;
            GUI.Label(pathRect, entry.AssetPath, style);
        
            return Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition);
        }
    
        private void DrawSelectedCellInfo()
        {
            if (_selectedMapEditor == null || !_selectedMapEditor.SelectedCell.HasValue)
                return;
        
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selected Cell Actions", EditorStyles.boldLabel);
        
            var coords = _selectedMapEditor.SelectedCell.Value;
        
            if (GUILayout.Button("Clear Cell"))
            {
                _selectedMapEditor.ClearCellContent(coords);
            }
        
            if (_selectedPrefab.Prefab != null)
            {
                if (GUILayout.Button($"Place: {_selectedPrefab.DisplayName}"))
                {
                    _selectedMapEditor.SetCellContent(coords, _selectedPrefab.DisplayName, _selectedPrefab.AssetReference);
                }
            }
        }
    
        private void RefreshPrefabList()
        {
            _prefabEntries.Clear();
        
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                var group = settings.groups.Find(group => group.Name == _assetGroup);
                foreach (var entry in group.entries)
                {
                    if (entry == null) continue;

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