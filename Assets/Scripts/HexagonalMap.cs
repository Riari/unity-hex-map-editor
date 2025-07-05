using System;
using System.Collections;
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
    public string Guid;
    public string Name;
    public AssetReference ContentAsset;
    public GameObject InstantiatedContent;
}

public class HexagonalMap : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private FlyingCamera playerCamera;

    [Header("Grid Settings")]
    [SerializeField, Range(1, 20)] private int gridSize = 5;
    [SerializeField, Range(0.1f, 2.0f)] private float cellSize = 0.1f;
    
    [Header("Content Settings")]
    [SerializeField, Range(0.1f, 1.0f)] private float contentScale = 0.28f;
    [SerializeField, Range(0, 180)] private int contentYRotation = 30;
    
    public float ContentScale => contentScale;
    public int ContentYRotation => contentYRotation;

    [Header("Preview Settings")]
    [SerializeField] private Material previewMaterial;
    
    public Material PreviewMaterial => previewMaterial;
    
    [Header("Data")]
    [SerializeField] private HexagonalMapData mapData;
    
    public HexagonalMapData MapData => mapData;

    private const float PlaneExtents = 5.0f;

    private Material _gridMaterial;
    private Material GridMaterial
    {
        get
        {
            if (_gridMaterial || this == null) return _gridMaterial;
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

    void Start()
    {
        foreach (var (coords, cell) in mapData.Cells)
        {
            Vector3 worldPos = HexToPoint(coords);
            StartCoroutine(InstantiateContentAsync(cell, worldPos));
        }
    }
    
    void OnValidate()
    {
        UpdateShaderProperties();
    }

    /// <summary>
    /// Instantiates a game object at the given position using the given cell's content asset
    /// </summary>
    /// <param name="cell">The cell to instantiate the object from</param>
    /// <param name="position">The position to place the object at</param>
    /// <returns>IEnumerator</returns>
    private IEnumerator InstantiateContentAsync(HexCell cell, Vector3 position)
    {
        Quaternion contentRotation = Quaternion.AngleAxis(contentYRotation, Vector3.up);
        var handle = cell.ContentAsset.InstantiateAsync(position, contentRotation);
        yield return handle;
        
        if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
        {
            handle.Result.transform.position = position;
            handle.Result.transform.localScale *= contentScale;
            cell.InstantiatedContent = handle.Result;
        }
    }
    
    /// <summary>
    /// Updates the grid and cell size shader properties
    /// </summary>
    private void UpdateShaderProperties()
    {
        if (GridMaterial == null) return;

        GridMaterial.SetInt(ShaderGridSize, gridSize);
        GridMaterial.SetFloat(ShaderCellSize, cellSize);
    }

    /// <summary>
    /// Sets the hovered cell coordinates in the shader
    /// </summary>
    /// <param name="cellCoords">The cell coordinates</param>
    public void SetHoveredCell(HexCoordinates cellCoords)
    {
        if (!GridMaterial) return;
        
        GridMaterial.SetVector(ShaderHoveredCell, new Vector4(cellCoords.Q, cellCoords.R, 0, -1));
    }

    /// <summary>
    /// Disables the hovered cell coordinates in the shader
    /// </summary>
    public void ClearHoveredCell()
    {
        if (!GridMaterial) return;
        
        GridMaterial.SetVector(ShaderHoveredCell, new Vector4(0, 0, -1, -1));
    }

    /// <summary>
    /// Sets the selected cell coordinates in the shader
    /// </summary>
    /// <param name="cellCoords">The cell coordinates</param>
    public void SetSelectedCell(HexCoordinates cellCoords)
    {
        if (!GridMaterial) return;
        
        GridMaterial.SetVector(ShaderSelectedCell, new Vector4(cellCoords.Q, cellCoords.R, 0, -1));
    }

    /// <summary>
    /// Disables the selected cell coordinates in the shader
    /// </summary>
    public void ClearSelectedCell()
    {
        if (!GridMaterial) return;
        
        GridMaterial.SetVector(ShaderSelectedCell, new Vector4(0, 0, -1, -1));
    }

    /// <summary>
    /// Writes content to a cell
    /// </summary>
    /// <param name="cellCoords">The coordinates of the cell to write content to</param>
    /// <param name="guid">A unique identifier for this cell's content</param>
    /// <param name="displayName">A display name to use for this cell's content</param>
    /// <param name="asset">A reference to the asset to use for this cell's content</param>
    public void SetCell(HexCoordinates cellCoords, string guid, string displayName, AssetReference asset)
    {
        if (mapData.Cells.TryGetValue(cellCoords, out HexCell existingCell))
        {
            if (existingCell.InstantiatedContent != null)
            {
                DestroyImmediate(existingCell.InstantiatedContent);
            }
        }

        if (asset != null && asset.RuntimeKeyIsValid())
        {
            HexCell cell = new HexCell
            {
                Guid = guid,
                Name = displayName,
                ContentAsset = asset
            };

            mapData.SetCell(cellCoords, cell);
        }
    }

    /// <summary>
    /// Rounds fractional coordinates to the hexagon grid
    /// </summary>
    /// <param name="hexCoordinates">The fractional coordinates</param>
    /// <returns>The rounded coordinates</returns>
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
    /// <returns>The normalized plane coordinates (between -1.0f and 1.0f)</returns>
    private float NormalizeAxisValue(float value)
    {
        return (Mathf.InverseLerp(-PlaneExtents, PlaneExtents, value) * 2.0f) - 1.0f;
    }

    /// <summary>
    /// Converts a world-space position to normalized 2D coordinates on the plane
    /// </summary>
    /// <param name="point">The world-space position</param>
    /// <returns>The normalized plane coordinates (between -1.0f and 1.0f)</returns>
    private Vector2 PointToNormalizedPlaneCoordinates(Vector3 point)
    {
        Vector2 result;
        result.x = -(NormalizeAxisValue(point.x) / cellSize);
        result.y = -(NormalizeAxisValue(point.z) / cellSize);
        return result;
    }

    /// <summary>
    /// Converts a world-space position to hex coordinates
    /// </summary>
    /// <param name="point">The world-space position</param>
    /// <returns>The hex coordinates (Q, R)</returns>
    public HexCoordinates PointToHex(Vector3 point)
    {
        Vector2 normalizedCoordinates = PointToNormalizedPlaneCoordinates(point);

        HexCoordinates hexCoordinates;
        hexCoordinates.Q = 2.0f / 3.0f * normalizedCoordinates.x;
        hexCoordinates.R = -1.0f / 3.0f * normalizedCoordinates.x + Mathf.Sqrt(3.0f) / 3.0f * normalizedCoordinates.y;

        return AxialRound(hexCoordinates);
    }

    /// <summary>
    /// Converts hex coordinates to a world-space position (the centre point of the cell)
    /// </summary>
    /// <param name="hexCoordinates">The hex coordinates to convert</param>
    /// <returns>The world-space position of the cell</returns>
    public Vector3 HexToPoint(HexCoordinates hexCoordinates)
    {
        float x = 3.0f / 2.0f * hexCoordinates.Q;
        float z = Mathf.Sqrt(3.0f) / 2.0f * hexCoordinates.Q + Mathf.Sqrt(3.0f) * hexCoordinates.R;
        x *= (transform.localScale.x / 2.0f);
        z *= (transform.localScale.z / 2.0f);

        Vector3 point;
        point.x = -x;
        point.y = transform.position.y;
        point.z = -z;

        return point;
    }
}