Shader "Custom/WaveSurface"
{
    Properties
    {
        _Color ("Color", Color) = (0.1, 0.4, 0.7, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5   // StructuredBuffer in the vertex stage
            #include "UnityCG.cginc"

            StructuredBuffer<float3> _Positions;
            StructuredBuffer<float3> _Normals;
            StructuredBuffer<int>    _Indices;
            float4x4 _ObjectToWorld;
            float4   _Color;

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            // No vertex input: each invocation fetches its own vertex
            // through the triangle list.
            v2f vert(uint vertexID : SV_VertexID)
            {
                int index = _Indices[vertexID];
                float3 position = _Positions[index];
                float3 normal = _Normals[index];

                float4 posWS = mul(_ObjectToWorld, float4(position, 1.0));

                v2f o;
                o.pos      = mul(UNITY_MATRIX_VP, posWS);
                o.normalWS = mul((float3x3)_ObjectToWorld, normal); // fine for uniform scale
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 l = normalize(float3(0.3, 1.0, 0.2));
                float ndl = saturate(dot(n, l));
                return float4(_Color.rgb * (0.2 + 0.8 * ndl), 1.0);
            }
            ENDCG
        }
    }
}
