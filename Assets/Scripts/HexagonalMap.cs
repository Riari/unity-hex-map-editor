using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

[Serializable]
public struct HexCoordinates : IEquatable<HexCoordinates>
{

    public float Q;
    public float R;

    public override string ToString()
    {
        return $"({Q}, {R})";
    }

    public static bool operator ==(HexCoordinates left, HexCoordinates right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(HexCoordinates left, HexCoordinates right)
    {
        return !(left == right);
    }

    public bool Equals(HexCoordinates other)
    {
        return Q.Equals(other.Q) && R.Equals(other.R);
    }

    public override bool Equals(object obj)
    {
        return obj is HexCoordinates other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Q, R);
    }
}

[Serializable]
public class HexCell
{
    public string Name;
    public AssetReference ContentAsset;
    public GameObject InstantiatedContent;
    public bool ShowPreview = true;
    
    public bool HasContent => ContentAsset != null && ContentAsset.RuntimeKeyIsValid();
}

public class HexagonalMap : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Camera playerCamera;

    [Header("Grid Settings")]
    [SerializeField, Range(1, 20)] private int gridSize = 5;
    [SerializeField, Range(0.1f, 2.0f)] private float cellSize = 0.1f;
    
    [Header("Preview Settings")]
    [SerializeField] private Color previewColor = new(1f, 1f, 1f, 0.5f);
    
    private Dictionary<HexCoordinates, HexCell> _cellStorage = new();
    
    private const float PlaneExtents = 5.0f;

    private Material _gridMaterial;
    private Material GridMaterial
    {
        get
        {
            if (_gridMaterial) return _gridMaterial;
            Renderer gridRenderer = GetComponent<Renderer>();
            if (gridRenderer)
            {
                _gridMaterial = Application.isPlaying ? gridRenderer.material : gridRenderer.sharedMaterial;
            }
            return _gridMaterial;
        }
    }

    private MeshRenderer _meshRenderer;
    private MeshRenderer MeshRenderer
    {
        get
        {
            if (_meshRenderer) return _meshRenderer;
            MeshRenderer planeRenderer = GetComponent<MeshRenderer>();
            _meshRenderer = planeRenderer;
            return _meshRenderer;
        }
    }

    private MeshCollider _meshCollider;
    public MeshCollider MeshCollider
    {
        get
        {
            if (_meshCollider) return _meshCollider;
            MeshCollider meshCollider = GetComponent<MeshCollider>();
            _meshCollider = meshCollider;
            return _meshCollider;
        }
    }

    private static readonly int ShaderGridSize = Shader.PropertyToID("_GridSize");
    private static readonly int ShaderCellSize = Shader.PropertyToID("_CellSize");
    private static readonly int ShaderHoveredCell = Shader.PropertyToID("_HoveredCell");
    private static readonly int ShaderSelectedCell = Shader.PropertyToID("_SelectedCell");
    
    private void UpdateShaderProperties()
    {
        if (GridMaterial == null) return;

        GridMaterial.SetInt(ShaderGridSize, gridSize);
        GridMaterial.SetFloat(ShaderCellSize, cellSize);
    }

    public void SetHoveredCell(HexCoordinates cellCoords)
    {
        if (!GridMaterial) return;
        
        GridMaterial.SetVector(ShaderHoveredCell, new Vector4(cellCoords.Q, cellCoords.R, 0, -1));
    }

    public void ClearHoveredCell()
    {
        if (!GridMaterial) return;
        
        GridMaterial.SetVector(ShaderHoveredCell, new Vector4(0, 0, -1, -1));
    }

    public void SetSelectedCell(HexCoordinates cellCoords)
    {
        if (!GridMaterial) return;
        
        GridMaterial.SetVector(ShaderSelectedCell, new Vector4(cellCoords.Q, cellCoords.R, 0, -1));
    }

    public void ClearSelectedCell()
    {
        if (!GridMaterial) return;
        
        GridMaterial.SetVector(ShaderSelectedCell, new Vector4(0, 0, -1, -1));
    }

    public void SetCellContent(HexCoordinates cellCoords, string displayName, AssetReference asset)
    {
        if (_cellStorage.TryGetValue(cellCoords, out HexCell existingCell))
        {
            if (existingCell.InstantiatedContent != null)
            {
                if (Application.isPlaying)
                {
                    if (existingCell.ContentAsset != null && existingCell.ContentAsset.RuntimeKeyIsValid())
                    {
                        Addressables.ReleaseInstance(existingCell.InstantiatedContent);
                    }
                    else
                    {
                        Destroy(existingCell.InstantiatedContent);
                    }
                }
                else
                {
                    DestroyImmediate(existingCell.InstantiatedContent);
                }
            }
        }

        if (asset != null && asset.RuntimeKeyIsValid())
        {
            HexCell cell = new HexCell
            {
                Name = displayName,
                ContentAsset = asset
            };

            var worldPos = HexToPoint(cellCoords);

            if (Application.isPlaying)
            {
                StartCoroutine(InstantiateContentAsync(cell, worldPos));
            }
            else
            {
                // TODO: Implement cell content preview
            }

            _cellStorage[cellCoords] = cell;
        }
    }
    
    private IEnumerator InstantiateContentAsync(HexCell cell, Vector3 position)
    {
        var handle = cell.ContentAsset.InstantiateAsync(position, Quaternion.identity, transform);
        yield return handle;
        
        if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
        {
            cell.InstantiatedContent = handle.Result;
        }
    }

    public bool TryGetCell(HexCoordinates cellCoords, out HexCell cell)
    {
        return _cellStorage.TryGetValue(cellCoords, out cell);
    }

    public void ClearCell(HexCoordinates coords)
    {
        ClearCell(_cellStorage[coords]);
    }

    public void ClearCell(HexCell cell)
    {
        if (cell.InstantiatedContent != null)
        {
            if (Application.isPlaying)
            {
                Destroy(cell.InstantiatedContent);
            }
            else
            {
                DestroyImmediate(cell.InstantiatedContent);
            }
        }
            
        cell.ContentAsset = null;
        cell.InstantiatedContent = null;
    }

    public void ClearAllCells()
    {
        foreach (var (_, cell) in _cellStorage)
        {
            ClearCell(cell);
        }
        
        _cellStorage.Clear();
    }
    
    void OnValidate()
    {
        UpdateShaderProperties();
    }

    private HexCoordinates AxialRound(HexCoordinates hexCoordinates)
    {
        float xGrid = Mathf.Round(hexCoordinates.Q);
        float yGrid = Mathf.Round(hexCoordinates.R);
        float x = hexCoordinates.Q - xGrid;
        float y = hexCoordinates.R - yGrid;
        float dx = Mathf.Round(x + 0.5f * y) * ((x * x >= y * y) ? 1.0f : 0.0f);
        float dy = Mathf.Round(y + 0.5f * x) * ((x * x < y * y) ? 1.0f : 0.0f);

        HexCoordinates rounded;
        rounded.Q = xGrid + dx;
        rounded.R = yGrid + dy;

        return rounded;
    }

    /// <summary>
    /// Normalizes local space coordinates to plane coordinates
    /// </summary>
    /// <param name="value">Value on a local space axis</param>
    /// <returns>The normalized plane coordinate value (between -1.0f and 1.0f)</returns>
    private float NormalizeAxisValue(float value)
    {
        return (Mathf.InverseLerp(-PlaneExtents, PlaneExtents, value) * 2.0f) - 1.0f;
    }

    private Vector2 PointToNormalizedPlaneCoordinates(Vector3 point)
    {
        Vector2 result;
        result.x = -(NormalizeAxisValue(point.x) / cellSize);
        result.y = -(NormalizeAxisValue(point.z) / cellSize);
        return result;
    }

    public HexCoordinates PointToHex(Vector3 point)
    {
        Vector2 normalizedCoordinates = PointToNormalizedPlaneCoordinates(point);

        HexCoordinates hexCoordinates;
        hexCoordinates.Q = 2.0f / 3.0f * normalizedCoordinates.x;
        hexCoordinates.R = -1.0f / 3.0f * normalizedCoordinates.x + Mathf.Sqrt(3.0f) / 3.0f * normalizedCoordinates.y;

        return AxialRound(hexCoordinates);
    }

    public Vector3 HexToPoint(HexCoordinates hexCoordinates)
    {
        float x = 3.0f / 2.0f * hexCoordinates.Q;
        float z = Mathf.Sqrt(3.0f) / 2.0f + Mathf.Sqrt(3.0f) * hexCoordinates.R;
        x *= cellSize;
        z *= cellSize;

        Vector3 point;
        point.x = x;
        point.y = transform.position.y;
        point.z = z;

        return point;
    }
}