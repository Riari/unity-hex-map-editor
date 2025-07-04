using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Editor
{
    [CustomEditor(typeof(HexagonalMap))]
    public class HexagonalMapEditor : UnityEditor.Editor
    {
        public HexagonalMap.Hex? SelectedCell; 
        
        void OnSceneGUI()
        {
            HexagonalMap hexagonalMap = (HexagonalMap)target;
            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseMove || currentEvent.type == EventType.MouseDown)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
                HexagonalMap.Hex targetCell;

                if (!TryGetCellAtWorldPosition(ray, out targetCell)) return;

                switch (currentEvent.type)
                {
                    case EventType.MouseMove:
                        hexagonalMap.SetHoveredCell(targetCell);
                        break;
                    case EventType.MouseDown when currentEvent.button == 0:
                        if (SelectedCell.HasValue && SelectedCell.Value == targetCell)
                        {
                            hexagonalMap.ClearSelectedCell();
                        }
                        else
                        {
                            hexagonalMap.SetSelectedCell(targetCell);
                        }
                    
                        currentEvent.Use();
                        break;
                }
            }
        }

        public bool TryGetCellAtWorldPosition(Ray ray, out HexagonalMap.Hex outCell)
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

        public AssetReference GetCellContent(HexagonalMap.Hex cell)
        {
            return default;
        }

        public void SetCellContent(HexagonalMap.Hex cell, AssetReference asset)
        {
            
        }
    }
}