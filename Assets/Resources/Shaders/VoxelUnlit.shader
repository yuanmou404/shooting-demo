// 无光照透明着色器（曳光弹、枪口火光、碎块、准星高亮框）。放在 Resources 下确保被打进包。

Shader "PixelArena/VoxelUnlit"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _FogColor ("Fog Color", Color) = (0.66, 0.78, 0.92, 1)
        _FogParams ("Fog Start/End", Vector) = (45, 165, 0, 0)
        _FogStrength ("Fog Strength", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        LOD 100
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _FogColor;
            float4 _FogParams;
            float _FogStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float depth : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.depth = -UnityObjectToViewPos(v.vertex).z;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half3 c = i.color.rgb;
                float f = saturate((i.depth - _FogParams.x) / max(_FogParams.y - _FogParams.x, 0.001));
                c = lerp(c, _FogColor.rgb, f * _FogStrength);
                return fixed4(c, i.color.a);
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
