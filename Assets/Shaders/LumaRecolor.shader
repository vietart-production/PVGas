Shader "PVGas/LumaRecolor"
{
    // Recolors a UI sprite by luminance: keeps its shape/shading/alpha but
    // replaces its hue with _Color. Used to fix baked art generated in the
    // wrong brand color (e.g. an orange flame icon) without a new asset.
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Target Color", Color) = (1, 1, 1, 1)
        _Intensity ("Glow Intensity (emission-style overexposure, 1 = off)", Float) = 1
        _Flat ("Flat Fill (0 = luma-shaded, 1 = solid _Color everywhere alpha>0 - for white, which otherwise reads as dingy gray in its shadows)", Range(0, 1)) = 0
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
            fixed4 _Color;
            float _Intensity;
            float _Flat;

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
                // luma 0.5 keeps _Color as-is; brighter/darker pixels push toward white/black
                fixed3 shaded = luma < 0.5
                    ? _Color.rgb * (luma * 2.0)
                    : lerp(_Color.rgb, fixed3(1, 1, 1), (luma - 0.5) * 2.0);
                // _Flat skips the luma darkening entirely and fills solid
                // _Color everywhere the texture has alpha - the shaded
                // version always dips toward black/gray in the artwork's
                // shadow areas, which reads as dingy rather than crisp for
                // a pure white target (there's no darker shade of "white").
                fixed3 graded = lerp(shaded, _Color.rgb, _Flat);
                // Emission-style overexposure toward white (same tone-map
                // family as FlowRibbon/RadialPulseGlow's _Intensity) so the
                // finale icons can be pushed slightly blown-out/glowing.
                graded = 1.0 - exp(-graded * _Intensity * 2.2);
                fixed4 col = fixed4(graded, tex.a) * i.color;
                return fixed4(col.rgb * _Color.a, col.a * _Color.a);
            }
            ENDCG
        }
    }
}
