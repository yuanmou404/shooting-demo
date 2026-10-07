// 放在 Resources 目录下 —— Resources 内的资源一定会被打进包，
// 这样打包后才能通过 Resources.Load 拿到，不会出现 Shader.Find 返回 null 的白屏问题。
// 注意：一个 .shader 文件只放一个 Shader，方便按名 Resources.Load。

Shader "PixelArena/VoxelBlocks"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _SunDir ("Sun Dir", Vector) = (0.45, 0.82, 0.35, 0)
        _SunColor ("Sun Color", Color) = (1, 1, 1, 1)
        _Ambient ("Ambient", Color) = (0.42, 0.45, 0.52, 1)
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

            sampler2D _MainTex;
            float4 _MainTex_ST;
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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float depth : TEXCOORD1;
                half3 nrm : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                o.nrm = UnityObjectToWorldNormal(v.normal);
                float3 vp = UnityObjectToViewPos(v.vertex);
                o.depth = -vp.z;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                half3 n = normalize(i.nrm);
                float ndl = max(dot(n, normalize(_SunDir)), 0.0);
                half3 lit = tex.rgb * i.color.rgb * (_Ambient.rgb + _SunColor.rgb * ndl);
                float f = saturate((i.depth - _FogParams.x) / max(_FogParams.y - _FogParams.x, 0.001));
                lit = lerp(lit, _FogColor.rgb, f * _FogStrength);
                return fixed4(lit, 1.0);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
