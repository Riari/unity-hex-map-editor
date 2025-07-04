using UnityEngine;

public class HexagonalMap : MonoBehaviour
{
    private struct Cube
    {
        public float Q;
        public float R;
        public float S;

        public override string ToString()
        {
            return $"({Q}, {R}, {S})";
        }
    }

    private struct Hex
    {
        public float Q;
        public float R;

        public override string ToString()
        {
            return $"({Q}, {R})";
        }
    }

    [Header("Components")]
    [SerializeField] private Camera playerCamera;

    [Header("Grid Settings")]
    [SerializeField, Range(1, 20)] private int gridSize = 5;
    [SerializeField, Range(0.1f, 2.0f)] private float cellSize = 0.1f;
    
    private const float PlaneExtents = 5.0f;

    private Hex? _currentSelectedCell;

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

    private void SetHoveredCell(Hex cellCoords)
    {
        if (!GridMaterial) return;
        
        GridMaterial.SetVector(ShaderHoveredCell, new Vector4(cellCoords.Q, cellCoords.R, 0, -1));
    }

    private void ClearHoveredCell()
    {
        if (!GridMaterial) return;
        
        GridMaterial.SetVector(ShaderHoveredCell, new Vector4(0, 0, -1, -1));
    }

    private void SetSelectedCell(Hex cellCoords)
    {
        if (!GridMaterial) return;
        
        GridMaterial.SetVector(ShaderSelectedCell, new Vector4(cellCoords.Q, cellCoords.R, 0, -1));
        _currentSelectedCell = cellCoords;
    }

    private void ClearSelectedCell()
    {
        if (!GridMaterial) return;
        
        GridMaterial.SetVector(ShaderSelectedCell, new Vector4(0, 0, -1, -1));
        _currentSelectedCell = null;
    }

    void OnValidate()
    {
        UpdateShaderProperties();
    }

    private Hex CubeToAxial(Cube cube)
    {
        Hex hex;
        hex.Q = cube.Q;
        hex.R = cube.R;
        return hex;
    }

    private Cube AxialToCube(Hex hex)
    {
        Cube cube;
        cube.Q = hex.Q;
        cube.R = hex.R;
        cube.S = -hex.Q - hex.R;
        return cube;
    }

    private Cube CubeRound(Cube cube)
    {
        Cube cubeRounded;
        cubeRounded.Q = Mathf.Round(cube.Q);
        cubeRounded.R = Mathf.Round(cube.R);
        cubeRounded.S = Mathf.Round(cube.S);
        
        float qDiff = Mathf.Abs(cubeRounded.Q - cube.Q);
        float rDiff = Mathf.Abs(cubeRounded.R - cube.R);
        float sDiff = Mathf.Abs(cubeRounded.S - cube.S);

        if (qDiff > rDiff && qDiff > sDiff)
        {
            cubeRounded.Q = -cubeRounded.R - cubeRounded.S;
        }
        else if (rDiff > sDiff)
        {
            cubeRounded.R = -cubeRounded.Q - cubeRounded.S;
        }
        else
        {
            cubeRounded.S = -cubeRounded.Q - cubeRounded.R;
        }
        
        return cubeRounded;
    }

    private Hex AxialRound(Hex hex)
    {
        return CubeToAxial(CubeRound(AxialToCube(hex)));
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

    private Hex PointToHex(Vector3 point)
    {
        Vector2 normalizedCoordinates = PointToNormalizedPlaneCoordinates(point);

        Hex hex;
        hex.Q = 2.0f / 3.0f * normalizedCoordinates.x;
        hex.R = -1.0f / 3.0f * normalizedCoordinates.x + Mathf.Sqrt(3.0f) / 3.0f * normalizedCoordinates.y;

        return AxialRound(hex);
    }

    public void HandleEditorMouseMove(Vector3 localPosition)
    {
        Hex hex = PointToHex(localPosition);
        SetHoveredCell(hex);
    }

    public void HandleEditorMouseClick(Vector3 localPosition)
    {
        Hex hex = PointToHex(localPosition);
        if (_currentSelectedCell.HasValue && _currentSelectedCell.Value.Q == hex.Q &&
            _currentSelectedCell.Value.R == hex.R)
        {
            ClearSelectedCell();
        }
        else
        {
            SetSelectedCell(hex);
        }
    }
}