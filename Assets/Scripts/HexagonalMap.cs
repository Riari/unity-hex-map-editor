using System;
using UnityEngine;

public class HexagonalMap : MonoBehaviour
{
    public struct Hex : IEquatable<Hex>
    {

        public float Q;
        public float R;

        public override string ToString()
        {
            return $"({Q}, {R})";
        }

        public static bool operator ==(Hex left, Hex right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Hex left, Hex right)
        {
            return !(left == right);
        }

        public bool Equals(Hex other)
        {
            return Q.Equals(other.Q) && R.Equals(other.R);
        }

        public override bool Equals(object obj)
        {
            return obj is Hex other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Q, R);
        }
    }

    [Header("Components")]
    [SerializeField] private Camera playerCamera;

    [Header("Grid Settings")]
    [SerializeField, Range(1, 20)] private int gridSize = 5;
    [SerializeField, Range(0.1f, 2.0f)] private float cellSize = 0.1f;
    
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

    public void SetHoveredCell(Hex cellCoords)
    {
        if (!GridMaterial) return;
        
        GridMaterial.SetVector(ShaderHoveredCell, new Vector4(cellCoords.Q, cellCoords.R, 0, -1));
    }

    public void ClearHoveredCell()
    {
        if (!GridMaterial) return;
        
        GridMaterial.SetVector(ShaderHoveredCell, new Vector4(0, 0, -1, -1));
    }

    public void SetSelectedCell(Hex cellCoords)
    {
        if (!GridMaterial) return;
        
        GridMaterial.SetVector(ShaderSelectedCell, new Vector4(cellCoords.Q, cellCoords.R, 0, -1));
    }

    public void ClearSelectedCell()
    {
        if (!GridMaterial) return;
        
        GridMaterial.SetVector(ShaderSelectedCell, new Vector4(0, 0, -1, -1));
    }

    void OnValidate()
    {
        UpdateShaderProperties();
    }

    private Hex AxialRound(Hex hex)
    {
        float xGrid = Mathf.Round(hex.Q);
        float yGrid = Mathf.Round(hex.R);
        float x = hex.Q - xGrid;
        float y = hex.R - yGrid;
        float dx = Mathf.Round(x + 0.5f * y) * ((x * x >= y * y) ? 1.0f : 0.0f);
        float dy = Mathf.Round(y + 0.5f * x) * ((x * x < y * y) ? 1.0f : 0.0f);

        Hex rounded;
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

    public Hex PointToHex(Vector3 point)
    {
        Vector2 normalizedCoordinates = PointToNormalizedPlaneCoordinates(point);

        Hex hex;
        hex.Q = 2.0f / 3.0f * normalizedCoordinates.x;
        hex.R = -1.0f / 3.0f * normalizedCoordinates.x + Mathf.Sqrt(3.0f) / 3.0f * normalizedCoordinates.y;

        return AxialRound(hex);
    }
}