using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HexagonalMapData", menuName = "Hexagonal Map/Map Data")]
public class HexagonalMapData : ScriptableObject
{
    [SerializeField] private List<HexCell> _cellList = new();
    [SerializeField] private List<HexCoordinates> _coordinatesList = new();

    private Dictionary<HexCoordinates, HexCell> _cells = new();
    public Dictionary<HexCoordinates, HexCell> Cells => _cells;
    
    private void OnEnable()
    {
        _cells.Clear();
        for (int i = 0; i < _cellList.Count && i < _coordinatesList.Count; i++)
        {
            _cells[_coordinatesList[i]] = _cellList[i];
        }
    }

    private void SaveData()
    {
        _cellList.Clear();
        _coordinatesList.Clear();
        
        foreach (var kvp in _cells)
        {
            _coordinatesList.Add(kvp.Key);
            _cellList.Add(kvp.Value);
        }
        
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
    
    public void SetCell(HexCoordinates coords, HexCell cell)
    {
        _cells[coords] = cell;
        SaveData();
    }
    
    public bool TryGetCell(HexCoordinates coords, out HexCell cell)
    {
        return _cells.TryGetValue(coords, out cell);
    }
    
    public void RemoveCell(HexCoordinates coords)
    {
        if (_cells.Remove(coords))
        {
            SaveData();
        }
    }
    
    public void ClearAllCells()
    {
        _cells.Clear();
        SaveData();
    }
}
