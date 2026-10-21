Shader "PVGas/OrganicRay"
{
    // Organic glowing ray: noise-wobbled width along its length (not a clean
    // parallel-edged ribbon), flickering brightness, soft fades at both
    // ends, plus a traveling pulse highlight (energy flow feel).
    Properties
    {
        _MainTex ("Sprite Texture (unused)", 2D) = "white" {}
        _Color ("Color", Color) = (0.3, 0.9, 0.4, 1)
        _NoiseScale ("Edge Noise Scale", Float) = 8
        _NoiseAmount ("Edge Noise Amount", Range(0, 1)) = 0.6
        _CoreSharpness ("Core Sharpness", Range(1, 8)) = 3
        _PulseWidth ("Pulse Width", Range(0.05, 1)) = 0.3
        _Speed ("Pulse Speed", Float) = 2
        _StartTime ("Start Time", Float) = 0
        _EndFade ("End Fade", Range(0, 0.3)) = 0.1
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
            float _NoiseScale;
            float _NoiseAmount;
            float _CoreSharpness;
            float _PulseWidth;
            float _Speed;
            float _StartTime;
            float _EndFade;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float hash(float n) { return frac(sin(n) * 43758.5453123); }

            float valueNoise(float x)
            {
                float i = floor(x);
                float f = frac(x);
                float a = hash(i);
                float b = hash(i + 1.0);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, f);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y - _StartTime;

                float n1 = valueNoise(i.uv.x * _NoiseScale);
                float n2 = valueNoise(i.uv.x * _NoiseScale * 2.3 + 11.0);
                float widthNoise = (n1 * 0.7 + n2 * 0.3) * 2.0 - 1.0;
                float halfWidth = saturate(0.5 + widthNoise * _NoiseAmount * 0.5);

                float distFromCenter = abs(i.uv.y - 0.5) * 2.0;
                float envelope = saturate(1.0 - distFromCenter / max(halfWidth, 0.05));
                float core = pow(envelope, _CoreSharpness);

                float brightness = 0.55 + 0.45 * valueNoise(i.uv.x * (_NoiseScale * 0.6) + 5.0);

                float pulseT = frac(t * _Speed);
                float pulseDist = abs(i.uv.x - pulseT);
                float pulse = saturate(1.0 - pulseDist / max(_PulseWidth, 0.001));

                float endFade = smoothstep(0.0, _EndFade, i.uv.x) * smoothstep(1.0, 1.0 - _EndFade, i.uv.x);

                float alpha = (envelope * 0.4 + core * 0.6) * brightness * endFade;
                alpha = saturate(alpha * (0.6 + 0.6 * pulse));

                fixed4 col = _Color * i.color;
                return fixed4(col.rgb, col.a * alpha);
            }
            ENDCG
        }
    }
}
