// 纯色体素着色器（角色、道具、方块小人）。放在 Resources 下确保被打进包。

Shader "PixelArena/VoxelSolid"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _SunDir ("Sun Dir", Vector) = (0.45, 0.82, 0.35, 0)
        _SunColor ("Sun Color", Color) = (1, 1, 1, 1)
        _Ambient ("Ambient", Color) = (0.45, 0.48, 0.55, 1)
        _FogColor ("Fog Color", Color) = (0.66, 0.78, 0.92, 1)
        _FogParams ("Fog Start/End", Vector) = (45, 165, 0, 0)
        _FogStrength ("Fog Strength", Float) = 0.85
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float3 _SunDir;
            fixed4 _SunColor;
            fixed4 _Ambient;
            fixed4 _FogColor;
            float4 _FogParams;
            float _FogStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float depth : TEXCOORD0;
                half3 nrm : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.nrm = UnityObjectToWorldNormal(v.normal);
                o.depth = -UnityObjectToViewPos(v.vertex).z;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half3 n = normalize(i.nrm);
                float ndl = max(dot(n, normalize(_SunDir)), 0.0);
                half3 lit = i.color.rgb * (_Ambient.rgb + _SunColor.rgb * ndl);
                float f = saturate((i.depth - _FogParams.x) / max(_FogParams.y - _FogParams.x, 0.001));
                lit = lerp(lit, _FogColor.rgb, f * _FogStrength);
                return fixed4(lit, 1.0);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
