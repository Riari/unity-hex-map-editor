using Editor.Windows;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Editor
{
    [CustomEditor(typeof(HexagonalMap))]
    public class HexagonalMapEditor : UnityEditor.Editor
    {
        private HexagonalMap _hexMap;
        private HexagonalMapEditorWindow _editorWindow;
        
        public HexCoordinates? SelectedCell;
        
        private void OnEnable()
        {
            _hexMap = target as HexagonalMap;
            SceneView.duringSceneGui += OnSceneGUI;
        }
    
        private void OnDisable()
        {
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
                    _hexMap.ClearAllCells();
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
        }

        public void SetEditorWindow(HexagonalMapEditorWindow window)
        {
            _editorWindow = window;
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

                        _editorWindow.OnCellSelectionChanged();
                    
                        currentEvent.Use();
                        break;
                }
            }
        }

        public bool TryGetCellAtWorldPosition(Ray ray, out HexCoordinates outCell)
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
            return _hexMap.TryGetCell(coords, out cell);
        }

        public void SetCellContent(HexCoordinates cell, string displayName, AssetReference asset)
        {
            _hexMap.SetCellContent(cell, displayName, asset);
        }

        public void ClearCellContent(HexCoordinates cell)
        {
            _hexMap.ClearCell(cell);
        }
    }
}