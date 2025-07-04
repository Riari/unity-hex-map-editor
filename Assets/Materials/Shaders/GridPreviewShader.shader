Shader "Unlit/GridPreviewShader"
{
    Properties
    {
        [HideInInspector] _GridSize ("Grid Size", Range(1, 20)) = 5
        [HideInInspector] _CellSize ("Cell Size", Range(0.1, 2.0)) = 0.1
        _HoveredCell ("Hovered Cell", Vector) = (0, 0, 0, -1)
        _SelectedCell ("Selected Cell", Vector) = (-1, -1, 0, -1)
        _CellColor ("Cell Color", Color) = (0.2, 0.2, 0.8, 1)
        _BorderColor ("Border Color", Color) = (0, 0, 0, 1)
        _HoveredColor ("Hovered Color", Color) = (0.8, 0.8, 0.2, 1)
        _SelectedColor ("Selected Color", Color) = (0.8, 0.2, 0.2, 1)
        _BorderWidth ("Border Width", Range(0.1, 0.5)) = 0.015
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            float _GridSize;
            float _CellSize;
            float3 _HoveredCell;
            float3 _SelectedCell;
            fixed4 _CellColor;
            fixed4 _BorderColor;
            fixed4 _HoveredColor;
            fixed4 _SelectedColor;
            float _BorderWidth;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            // Convert axial coordinates to world position
            float2 axialToWorld(float2 axial)
            {
                float x = _CellSize * (3.0/2.0 * axial.x);
                float y = _CellSize * (sqrt(3.0)/2.0 * axial.x + sqrt(3.0) * axial.y);
                return float2(x, y);
            }

            // Convert world position to axial coordinates
            float2 worldToAxial(float2 worldPos)
            {
                float q = (2.0/3.0 * worldPos.x) / _CellSize;
                float r = (-1.0/3.0 * worldPos.x + sqrt(3.0)/3.0 * worldPos.y) / _CellSize;
                return float2(q, r);
            }

            // Round axial coordinates to nearest hex
            float2 roundAxial(float2 axial)
            {
                float q = axial.x;
                float r = axial.y;
                float s = -q - r;
                
                float rq = round(q);
                float rr = round(r);
                float rs = round(s);
                
                float q_diff = abs(rq - q);
                float r_diff = abs(rr - r);
                float s_diff = abs(rs - s);
                
                if (q_diff > r_diff && q_diff > s_diff)
                    rq = -rr - rs;
                else if (r_diff > s_diff)
                    rr = -rq - rs;
                
                return float2(rq, rr);
            }

            // Proper hexagon SDF (Signed Distance Field)
            float hexSDF(float2 p, float radius)
            {
                // Convert to first quadrant
                p = abs(p);
                
                // Hexagon constants
                float3 k = float3(-0.866025404, 0.5, 0.577350269); // sqrt(3)/2, 1/2, 1/sqrt(3)
                
                // Project onto the first edge
                p -= 2.0 * min(dot(k.xy, p), 0.0) * k.xy;
                
                // Distance to edge
                p -= float2(clamp(p.x, -k.z * radius, k.z * radius), radius);
                
                return length(p) * sign(p.y);
            }

            // Check if point is within hexagonal grid bounds
            bool isInGrid(float2 axial)
            {
                float q = axial.x;
                float r = axial.y;
                float s = -q - r;
                return abs(q) <= _GridSize && abs(r) <= _GridSize && abs(s) <= _GridSize;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Center UV coordinates
                float2 uv = (i.uv - 0.5) * 2.0;
                
                // Convert to axial coordinates
                float2 axial = worldToAxial(uv);
                float2 roundedAxial = roundAxial(axial);
                
                // Check if we're within the hexagonal grid
                if (!isInGrid(roundedAxial))
                {
                    discard;
                }
                
                // Get the world position of the current hex center
                float2 hexCenter = axialToWorld(roundedAxial);
                
                // Calculate signed distance to hexagon edge
                float2 localPos = uv - hexCenter;
                float dist = hexSDF(localPos, _CellSize);
                
                // Make border width absolute (not relative to cell size)
                float absoluteBorderWidth = _BorderWidth * _CellSize;
                
                // Determine if we're in the border area
                bool isBorder = dist > -absoluteBorderWidth;
                
                // Determine cell color based on state
                fixed4 cellColor = _CellColor;
                
                // Check if this is the hovered cell
                if (roundedAxial.x == _HoveredCell.x && roundedAxial.y == _HoveredCell.y && 
                    _HoveredCell.z == 0)
                {
                    cellColor = _HoveredColor;
                }
                
                // Check if this is the selected cell (overrides hover)
                if (roundedAxial.x == _SelectedCell.x && roundedAxial.y == _SelectedCell.y && 
                    _SelectedCell.z == 0)
                {
                    cellColor = _SelectedColor;
                }
                
                // Return border or cell color
                fixed4 finalColor = isBorder ? _BorderColor : cellColor;
                
                // Apply fog
                UNITY_APPLY_FOG(i.fogCoord, finalColor);
                return finalColor;
            }
            ENDCG
        }
    }
}