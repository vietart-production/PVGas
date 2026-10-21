Shader "PVGas/FlameFill"
{
    // Starts the sprite fully desaturated (gray), then reveals its real
    // color from the bottom up as _Fill goes 0 -> 1 (UV.y = 0 is the
    // bottom of the sprite in Unity's UI mesh) - one step per energy pulse
    // that reaches the flame. A thin bright line marks the current edge.
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Fill ("Fill (0-1, bottom up)", Range(0, 1)) = 0
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.2)) = 0.025
        _EdgeColor ("Edge Glow Color", Color) = (0.6, 1, 0.75, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float _Fill;
            float _EdgeSoftness;
            fixed4 _EdgeColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float luma = dot(tex.rgb, float3(0.299, 0.587, 0.114));
                fixed3 gray = fixed3(luma, luma, luma);

                float grayAmount = smoothstep(_Fill - _EdgeSoftness, _Fill + _EdgeSoftness, i.uv.y);
                fixed3 col = lerp(tex.rgb, gray, grayAmount);

                float edgeLine = 1.0 - smoothstep(0.0, _EdgeSoftness * 1.5, abs(i.uv.y - _Fill));
                col += _EdgeColor.rgb * edgeLine * _EdgeColor.a * tex.a;

                fixed4 outCol = fixed4(col, tex.a) * i.color;
                return outCol;
            }
            ENDCG
        }
    }
}
