using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(HexagonalMap))]
    public class HexagonalMapEditor : UnityEditor.Editor
    {
        void OnSceneGUI()
        {
            HexagonalMap hexagonalMap = (HexagonalMap)target;
            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseMove || currentEvent.type == EventType.MouseDown)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);

                if (hexagonalMap.MeshCollider != null &&
                    hexagonalMap.MeshCollider.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
                {
                    Vector3 localPosition = hexagonalMap.transform.InverseTransformPoint(hit.point);

                    switch (currentEvent.type)
                    {
                        case EventType.MouseMove:
                            hexagonalMap.HandleEditorMouseMove(localPosition);
                            break;
                        case EventType.MouseDown when currentEvent.button == 0:
                            hexagonalMap.HandleEditorMouseClick(localPosition);
                    
                            currentEvent.Use();
                            break;
                    }
                }
            }
        }
    }
}