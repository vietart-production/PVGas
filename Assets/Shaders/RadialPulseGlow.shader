Shader "PVGas/RadialPulseGlow"
{
    // Procedural expanding-ring glow for UI Images - no texture needed.
    // Used for the floor footstep aura (slow, 1 ring) and the finale burst
    // (fast, many rings, restarted via _StartTime each time it fires).
    Properties
    {
        _MainTex ("Sprite Texture (unused, Canvas needs this)", 2D) = "white" {}
        _Color ("Color", Color) = (0.3, 0.9, 1, 1)
        _RingCount ("Ring Count", Float) = 2
        _Speed ("Speed", Float) = 1.2
        _Softness ("Edge Softness", Range(0.05, 1)) = 0.6
        _RingSharpness ("Ring Band Sharpness (lower = softer/wider band)", Float) = 4
        _StartTime ("Start Time", Float) = 0
        _Intensity ("Intensity (brightness boost, overexposes toward white)", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One

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
            float _RingCount;
            float _Speed;
            float _Softness;
            float _RingSharpness;
            float _StartTime;
            float _Intensity;

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
                float2 centered = (i.uv - 0.5) * 2.0;
                float dist = length(centered);

                float t = _Time.y - _StartTime;
                float phase = dist * _RingCount - t * _Speed;
                float ring = pow(saturate(1.0 - abs(frac(phase) - 0.5) * 2.0), max(_RingSharpness, 0.5));

                float softFade = saturate((1.0 - dist) / max(_Softness, 0.001));
                float alpha = ring * softFade;

                fixed4 col = _Color * i.color;
                float3 hot = 1.0 - exp(-col.rgb * _Intensity * 2.2); // overexposes toward white so it never sinks into a same-hue bkg
                return fixed4(hot, col.a * alpha);
            }
            ENDCG
        }
    }
}
