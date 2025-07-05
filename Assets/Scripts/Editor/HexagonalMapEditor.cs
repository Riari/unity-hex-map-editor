using System;
using System.Collections.Generic;
using Editor.Windows;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Editor
{
    [Serializable]
    public class HexCellPreviewObject
    {
        public Vector3 worldPosition;
        public Quaternion rotation;
        public Vector3 scale;
        public GameObject prefab;
        public string label;

        public HexCellPreviewObject(Vector3 worldPos, Quaternion rot, Vector3 scale, GameObject prefab, string label)
        {
            this.worldPosition = worldPos;
            this.rotation = rot;
            this.scale = scale;
            this.prefab = prefab;
            this.label = label;
        }
    }

    [CustomEditor(typeof(HexagonalMap))]
    public class HexagonalMapEditor : UnityEditor.Editor
    {
        private HexagonalMap _hexMap;
        private HexagonalMapEditorWindow _editorWindow;

        public HexCoordinates? SelectedCell;

        private readonly List<HexCellPreviewObject> _previewObjects = new();
        private Dictionary<string, GameObject> _prefabCache = new();
        
        private GUIStyle _previewLabelStyle;
        private GUIStyle PreviewLabelStyle
        {
            get
            {
                if (_previewLabelStyle == null)
                {
                    _previewLabelStyle = new GUIStyle()
                    {
                        fontStyle = FontStyle.Bold,
                        fontSize = 16,
                        normal = new GUIStyleState
                        {
                            textColor = Color.white,
                            background = Texture2D.blackTexture
                        }
                    };
                }
                return _previewLabelStyle;
            }
        }

        private void OnEnable()
        {
            _hexMap = target as HexagonalMap;
            SceneView.duringSceneGui += OnSceneGUI;
            
            RefreshPreviewObjects();
        }

        private void OnDisable()
        {
            _hexMap.ClearSelectedCell();
            if (_editorWindow) _editorWindow.Repaint();
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            if (GUILayout.Button("Open Hex Map Editor Window"))
            {
                HexagonalMapEditorWindow.ShowWindow();
            }

            if (GUILayout.Button("Clear All Cells"))
            {
                if (EditorUtility.DisplayDialog("Clear All Cells",
                        "Are you sure you want to clear all cell content?",
                        "Yes", "Cancel"))
                {
                    _hexMap.MapData.ClearAllCells();
                    RefreshPreviewObjects();
                }
            }

            EditorGUILayout.Space();

            if (SelectedCell.HasValue)
            {
                var coords = SelectedCell.Value;
                EditorGUILayout.LabelField($"Selected Cell: ({coords.Q}, {coords.R})", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(TryGetCell(coords, out HexCell cell)
                    ? $"Content: {cell.Name}"
                    : "Content: Empty");
            }
            else
            {
                EditorGUILayout.LabelField("Selected Cell: None", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Click on a hex cell in the Scene view to select it", MessageType.Info);
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_hexMap == null) return;

            HandleCellSelection();

            foreach (var obj in _previewObjects)
            {
                DrawCellPreviewObject(obj);
            }
        }

        public void SetEditorWindow(HexagonalMapEditorWindow window)
        {
            _editorWindow = window;
        }

        public void Reset()
        {
            _editorWindow = null;
            SelectedCell = null;
            _hexMap = null;
        }

        private void HandleCellSelection()
        {
            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseMove || currentEvent.type == EventType.MouseDown)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
                HexCoordinates targetCell;

                if (!TryGetCellAtWorldPosition(ray, out targetCell)) return;

                switch (currentEvent.type)
                {
                    case EventType.MouseMove:
                        _hexMap.SetHoveredCell(targetCell);
                        break;
                    case EventType.MouseDown when currentEvent.button == 0:
                        if (SelectedCell.HasValue && SelectedCell.Value == targetCell)
                        {
                            _hexMap.ClearSelectedCell();
                            SelectedCell = null;
                        }
                        else
                        {
                            _hexMap.SetSelectedCell(targetCell);
                            SelectedCell = targetCell;
                        }

                        if (_editorWindow) _editorWindow.OnCellSelectionChanged();

                        currentEvent.Use();
                        break;
                }
            }
        }

        private bool TryGetCellAtWorldPosition(Ray ray, out HexCoordinates outCell)
        {
            HexagonalMap hexagonalMap = (HexagonalMap)target;

            if (hexagonalMap.MeshCollider != null &&
                hexagonalMap.MeshCollider.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
            {
                Vector3 localPosition = hexagonalMap.transform.InverseTransformPoint(hit.point);
                outCell = hexagonalMap.PointToHex(localPosition);
                return true;
            }

            outCell = default;
            return false;
        }

        public bool TryGetCell(HexCoordinates coords, out HexCell cell)
        {
            return _hexMap.MapData.TryGetCell(coords, out cell);
        }

        public void SetCell(HexCoordinates coords, PrefabEntry prefab)
        {
            _hexMap.SetCell(coords, prefab.Guid, prefab.DisplayName, prefab.AssetReference);
            RefreshPreviewObjects();
            Repaint();
        }

        public void DeleteCell(HexCoordinates coords)
        {
            _hexMap.MapData.RemoveCell(coords);
            RefreshPreviewObjects();
            Repaint();
        }

        private GameObject LoadPrefabFromAssetReference(AssetReference assetRef)
        {
#if UNITY_EDITOR
            if (_prefabCache.TryGetValue(assetRef.AssetGUID, out GameObject cachedPrefab))
            {
                return cachedPrefab;
            }

            var assetPath = AssetDatabase.GUIDToAssetPath(assetRef.AssetGUID);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (prefab != null)
            {
                _prefabCache[assetRef.AssetGUID] = prefab;
            }

            return prefab;
#else
        return null;
#endif
        }

        private void RefreshPreviewObjects()
        {
            _previewObjects.Clear();

            HexagonalMapData mapData = _hexMap.MapData;

            if (mapData?.Cells == null) return;

            foreach (var (coords, cell) in mapData.Cells)
            {
                if (cell.ContentAsset == null || !cell.ContentAsset.RuntimeKeyIsValid()) continue;

                GameObject prefab = LoadPrefabFromAssetReference(cell.ContentAsset);
                if (prefab == null) continue;

                Vector3 worldPos = _hexMap.HexToPoint(coords);
                Quaternion contentRotation = Quaternion.AngleAxis(_hexMap.ContentYRotation, Vector3.up);
                Vector3 scale = Vector3.one * _hexMap.ContentScale;

                _previewObjects.Add(new HexCellPreviewObject(worldPos, contentRotation, scale, prefab, cell.Name));
            }
        }

        private void DrawCellPreviewObject(HexCellPreviewObject previewObj)
        {
            GameObject prefab = previewObj.prefab;
            Vector3 position = previewObj.worldPosition;
            Quaternion rotation = previewObj.rotation;
            Vector3 scale = previewObj.scale;

            var meshFilters = prefab.GetComponentsInChildren<MeshFilter>();
            var meshRenderers = prefab.GetComponentsInChildren<MeshRenderer>();

            for (int i = 0; i < meshFilters.Length && i < meshRenderers.Length; i++)
            {
                var meshFilter = meshFilters[i];
                var meshRenderer = meshRenderers[i];

                if (meshFilter.sharedMesh == null) continue;

                Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scale) *
                                   Matrix4x4.TRS(meshFilter.transform.localPosition,
                                       meshFilter.transform.localRotation,
                                       meshFilter.transform.localScale);

                for (int submesh = 0; submesh < meshFilter.sharedMesh.subMeshCount; submesh++)
                {
                    if (submesh >= meshRenderer.sharedMaterials.Length) continue;

                    var originalMaterial = meshRenderer.sharedMaterials[submesh];
                    if (originalMaterial == null) continue;

                    var previewMaterial = _hexMap.PreviewMaterial;
                    if (previewMaterial.mainTexture == null)
                    {
                        previewMaterial.mainTexture = originalMaterial.mainTexture;
                    }

                    Graphics.DrawMesh(meshFilter.sharedMesh, matrix, previewMaterial, 0, null, submesh);
                }
            }

            GUIContent labelContent = new GUIContent(previewObj.label);
            Vector2 textSize = PreviewLabelStyle.CalcSize(labelContent);
            PreviewLabelStyle.contentOffset = new Vector2(-textSize.x * 0.5f, 0.0f);
    
            Handles.Label(previewObj.worldPosition, previewObj.label, PreviewLabelStyle);
        }
    }
}