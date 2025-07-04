using System;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(HexagonalGrid))]
public class HexagonalGridEditor : Editor
{
    void OnSceneGUI()
    {
        HexagonalGrid hexagonalGrid = (HexagonalGrid)target;
        Event currentEvent = Event.current;

        if (currentEvent.type == EventType.MouseMove || currentEvent.type == EventType.MouseDown)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);

            if (hexagonalGrid.MeshCollider != null &&
                hexagonalGrid.MeshCollider.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
            {
                Vector3 localPosition = hexagonalGrid.transform.InverseTransformPoint(hit.point);

                switch (currentEvent.type)
                {
                    case EventType.MouseMove:
                        hexagonalGrid.HandleEditorMouseMove(localPosition);
                        break;
                    case EventType.MouseDown when currentEvent.button == 0:
                        hexagonalGrid.HandleEditorMouseClick(localPosition);
                    
                        currentEvent.Use();
                        break;
                }
            }
        }
    }
}