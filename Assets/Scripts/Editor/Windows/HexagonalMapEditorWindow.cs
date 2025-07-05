using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

#if UNITY_EDITOR
namespace Editor.Windows
{
    public struct PrefabEntry : IEquatable<PrefabEntry>
    {
        public string Guid;
        public string DisplayName;
        public string AssetPath;
        public GameObject Prefab;
        public AssetReference AssetReference;

        public static bool operator ==(PrefabEntry left, PrefabEntry right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PrefabEntry left, PrefabEntry right)
        {
            return !(left == right);
        }

        public bool Equals(PrefabEntry other)
        {
            return Guid == other.Guid;
        }

        public override bool Equals(object obj)
        {
            return obj is PrefabEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Guid != null ? Guid.GetHashCode() : 0);
        }
    }

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
        private List<PrefabEntry> _prefabEntries = new();
        private readonly Dictionary<string, Texture2D> _thumbnailCache = new();
        private string _addressableGroup = "HexagonalTiles";
        private readonly PrefabEntry _emptyEntry = new()
        {
            Guid = "Empty",
            DisplayName = "(Empty)",
            AssetPath = ""
        };
        private PrefabEntry _selectedPrefab;
    
        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            RefreshPrefabList();
            wantsMouseMove = true;
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

                bool hasContent = _selectedMapEditor.TryGetCell(coords, out HexCell cell);
                EditorGUILayout.LabelField(hasContent ? $"Content: {cell.Name}" : "Content: Empty");

                if (hasContent)
                {
                    foreach (var entry in _prefabEntries)
                    {
                        if (cell.Guid == entry.Guid)
                        {
                            _selectedPrefab = entry;
                            break;
                        }
                    }
                }
                else
                {
                    _selectedPrefab = _emptyEntry;
                }
                
                DrawPrefabFilters();
                DrawPrefabList();
            }
            else
            {
                EditorGUILayout.LabelField("Selected Cell: None");
            }
        
            EditorGUILayout.Space();

            if (Event.current.type == EventType.MouseMove)
            {
                Repaint();
            }
        }

        public void OnCellSelectionChanged()
        {
            Repaint();
        }
    
        private void DrawPrefabFilters()
        {
            EditorGUILayout.LabelField("Prefab Library", EditorStyles.boldLabel);
        
            EditorGUI.BeginChangeCheck();
            _addressableGroup = EditorGUILayout.TextField("Addressable Group", _addressableGroup);
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
            EditorGUILayout.Space();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        
            foreach (var entry in _prefabEntries)
            {
                if (!DrawPrefabEntry(entry)) continue;
                _selectedPrefab = entry;
                
                if (_selectedMapEditor != null && _selectedMapEditor.SelectedCell.HasValue)
                {
                    if (entry == _emptyEntry)
                    {
                        _selectedMapEditor.DeleteCell(_selectedMapEditor.SelectedCell.Value);
                    }
                    else
                    {
                        _selectedMapEditor.SetCell(_selectedMapEditor.SelectedCell.Value, entry);
                    }
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
            style.hover.textColor = Color.gray;
            GUI.Label(pathRect, entry.AssetPath, style);
        
            return Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition);
        }
    
        private void RefreshPrefabList()
        {
            _prefabEntries.Clear();
            
            _prefabEntries.Add(_emptyEntry);
        
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                var group = settings.groups.Find(group => group.Name == _addressableGroup);
                foreach (var entry in group.entries)
                {
                    if (entry == null) continue;

                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(entry.AssetPath);
                    if (asset == null) continue;

                    var assetRef = new AssetReference(entry.guid);
                    _prefabEntries.Add(new PrefabEntry
                    {
                        Guid = entry.guid,
                        DisplayName = System.IO.Path.GetFileNameWithoutExtension(entry.AssetPath),
                        AssetPath = entry.AssetPath,
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