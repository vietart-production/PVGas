Shader "PVGas/FlowRibbon"
{
    // An organic ribbon of flowing neon light, drawn procedurally on one UI quad
    // (no textures, no mesh rebuilds). Naturalness first - nothing is periodic:
    //  * The centerline is a sum of slowly drifting 1D noise layers (a meander,
    //    like smoke / aurora), not a sine.
    //  * The bundle breathes: its width fans out and bunches up irregularly.
    //  * ~14 filaments each wander on their own noise, so spacing is uneven and
    //    they drift across one another; each also surges and fades in brightness
    //    and thickness along its length (like real light streaks).
    //  * Gradient color: hot warm-white core -> base hue -> deeper, hue-shifted
    //    edges, plus a gentle shift along the path. Each filament carries its own
    //    gradient color; sums are tone-mapped per channel (1 - exp(-c*I)) so dense
    //    overlaps overexpose toward white.
    //  * A soft gradient "aurora" body behind the filaments (no hard rim) and a
    //    lorentzian bloom tail on every filament keep the edges gentle.
    //  * Motion: every noise layer scrolls toward the flame at its own speed.
    //  * Geometry is always vertical (u runs bottom -> top). The centerline can
    //    S-bend sideways between two x offsets. All noise is a function of the
    //    absolute path distance (_GStart + y) and time, so a floor segment and an
    //    LED segment line up seamlessly when the screens are stacked.
    //  * _Progress sweeps a bright head up the path (the "shot"), after which the
    //    ribbon stays fully visible and keeps flowing.
    Properties
    {
        _MainTex ("Sprite Texture (unused, UI needs it)", 2D) = "white" {}
        _Color ("Base Color", Color) = (0.3, 1, 0.15, 1)
        _Seed ("Seed", Float) = 1
        _Width ("Quad Width (units)", Float) = 300
        _Height ("Path Height (units, excludes pads)", Float) = 600
        _PadB ("Extra quad below path start (units)", Float) = 0
        _PadT ("Extra quad above path end (units)", Float) = 0
        _BandHalf ("Ribbon half width (units)", Float) = 40
        _WaveAmp ("Meander amplitude (units)", Float) = 46
        _GlowWidth ("Per-filament glow width (multiplier)", Float) = 1
        _Wavelength ("Meander length (units)", Float) = 380
        _SineFreq ("Sine Wave Frequency (wave cycles per Wavelength span - higher = tighter winding)", Float) = 0.5
        _Flow ("Flow speed (features / sec)", Float) = 0.12
        _BendStart ("Centerline X at start (quad-local)", Float) = 0
        _BendEnd ("Centerline X at end (quad-local)", Float) = 0
        _GStart ("Path distance at start (units)", Float) = 0
        _PinchStart ("Pinch at start", Range(0, 1)) = 0
        _PinchEnd ("Pinch at end", Range(0, 1)) = 0
        _PinchLen ("Pinch length (units)", Float) = 170
        _PinchFloor ("Pinch minimum width (0-1, never fully closes)", Range(0, 1)) = 0.2
        _Progress ("Progress", Range(0, 1)) = 1
        _Intensity ("Intensity", Float) = 1
        _TrailLength ("Trail lifetime behind the head (units, large = stays lit like before)", Float) = 100000
        _SpinSpeed ("Spin Speed (rotations/sec around the vertical axis, 0 = off)", Float) = 0
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
            #pragma target 3.0
            #include "UnityCG.cginc"

            #define FILS 9

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
            float _Seed;
            float _Width;
            float _Height;
            float _PadB;
            float _PadT;
            float _BandHalf;
            float _WaveAmp;
            float _GlowWidth;
            float _Wavelength;
            float _SineFreq;
            float _Flow;
            float _BendStart;
            float _BendEnd;
            float _GStart;
            float _PinchStart;
            float _PinchEnd;
            float _PinchLen;
            float _PinchFloor;
            float _Progress;
            float _Intensity;
            float _TrailLength;
            float _SpinSpeed;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float smoother(float x)
            {
                x = saturate(x);
                return x * x * x * (x * (x * 6.0 - 15.0) + 10.0);
            }

            // smooth 1D value noise in [-1, 1]
            float vnoise(float x)
            {
                float i = floor(x);
                float f = frac(x);
                f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                return lerp(hash11(i), hash11(i + 1.0), f) * 2.0 - 1.0;
            }

            // Centerline meander (about [-1, 1]): a real, constant-amplitude
            // sine wave - every hump the same height, winding at a clear,
            // literal frequency (_SineFreq) - instead of value-noise, whose
            // bump heights vary randomly cell to cell ("random amp quá",
            // "độ uốn éo chưa rõ ràng" client feedback). A faint noise layer
            // is still added on top purely as texture, at low enough weight
            // that it can't meaningfully change the wave's height or rhythm.
            float wander(float g, float tm, float sd)
            {
                float u = g / _Wavelength;
                float primary = sin(6.2831853 * (u * _SineFreq - tm * _Flow * 0.9) + sd * 10.0);
                float detail = vnoise(u * 2.4 - tm * _Flow * 1.3 + sd * 3.1 + 5.0) * 0.08;
                return primary + detail;
            }

            // rotate a color's hue around the gray axis
            float3 hueShift(float3 c, float a)
            {
                float cs = cos(a);
                float sn = sin(a);
                float3 k = float3(0.57735, 0.57735, 0.57735);
                return c * cs + cross(k, c) * sn + k * dot(k, c) * (1.0 - cs);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // The quad may extend past the path (pads at pinched ends so
                // the end glows are not clipped): yU is measured along the
                // path, t = yU / path height, tc = t clamped to the path.
                float quadH = _Height + _PadB + _PadT;
                float yU = i.uv.y * quadH - _PadB;
                float xU = (i.uv.x - 0.5) * _Width;
                float t = yU / max(_Height, 1.0);
                float tc = saturate(t);

                // One pixel in quad units - keeps hair-thin filaments from
                // aliasing / vanishing. Computed before any early-out
                // (derivatives need uniform control flow).
                float pxU = max(fwidth(xU), 1e-3);

                // Sweeping head: soft leading edge; at _Progress = 1 the
                // overshoot (1.12) sweeps just past the end. Wind-gust
                // trail: only a limited stretch behind the head stays lit -
                // a gradual exponential falloff (slow fade, easing toward
                // dark rather than a hard cutoff) over _TrailLength - the
                // path doesn't just permanently stay lit as the head
                // passes, it fades back to dark behind it, like wind
                // passing through grass.
                float hp = _Progress * 1.12;
                float leadMask = 1.0 - smoothstep(hp - 0.12, hp, tc);
                float trailFrac = _TrailLength / max(_Height, 1.0);
                float behind = max(hp - tc, 0.0);
                float trailDecay = exp(-behind / max(trailFrac, 0.001));
                float mask = leadMask * trailDecay;

                // Centerline: smootherstep S-bend with vertical tangent at both
                // ends, so it meets the screen seam / the flame head-on.
                float xc = lerp(_BendStart, _BendEnd, smoother(tc));
                float dxc = (_BendEnd - _BendStart) * 30.0 * tc * tc * (1.0 - tc) * (1.0 - tc) / max(_Height, 1.0);

                float g = _GStart + yU;
                float tm = _Time.y;
                float sd = frac(_Seed * 0.0137) * 50.0;

                // Pinch envelope: source / sink ends close in, but never
                // fully to a point - _PinchFloor keeps a minimum width so
                // the start doesn't look needle-thin.
                float pS = lerp(_PinchFloor, 1.0, pow(smoother(yU / _PinchLen), 0.8));
                float pE = lerp(_PinchFloor, 1.0, pow(smoother((_Height - yU) / _PinchLen), 0.8));
                float pinch = lerp(1.0, pS, _PinchStart) * lerp(1.0, pE, _PinchEnd);
                float R = _BandHalf * pinch;
                float A = _WaveAmp * pinch;

                // ---- gradient palette from the base color --------------------------
                float3 baseCol = _Color.rgb / max(max(_Color.r, _Color.g), max(_Color.b, 0.001));
                baseCol = pow(baseCol, 1.5);
                float3 deepCol = saturate(hueShift(baseCol, -0.4)) * 0.55;
                float3 hotCol = lerp(baseCol, float3(1.0, 0.97, 0.78), 0.65);

                // End nodes are cheap: evaluate before the early-out so the cut
                // never leaves a visible hard edge.
                float d0 = length(float2(xU - _BendStart, yU));
                float d1 = length(float2(xU - _BendEnd, _Height - yU));
                float nodeGlow = _PinchStart * exp(-(d0 * d0) / 676.0) * 0.9
                               + _PinchEnd * exp(-(d1 * d1) / 676.0) * 0.9;

                float latMean = xU - xc;

                // Fade to zero toward the quad's side edges so the (very faint)
                // bloom tails can never show a hard cut. Top/bottom edges are
                // the screen seam / end nodes and are left untouched.
                float sideFade = smoothstep(0.0, 0.15, 0.5 - abs(i.uv.x - 0.5));
                float outAlpha = mask * i.color.a * sideFade;

                if (mask <= 0.001)
                    return fixed4(0, 0, 0, 0);
                if (abs(latMean) > 1.55 * A + 1.95 * R + 45.0)
                    return fixed4(saturate(baseCol * nodeGlow), outAlpha);

                // Meander of the bundle's center, plus its local slope (central
                // difference) for perpendicular-distance compensation.
                float W  = A * wander(g, tm, sd);
                float Wp = A * wander(g + 3.0, tm, sd);
                float Wm = A * wander(g - 3.0, tm, sd);
                float slopeC = dxc + (Wp - Wm) / 6.0;
                float invSlope = rsqrt(1.0 + slopeC * slopeC);

                // The bundle breathes: fans out / bunches up, irregularly.
                float breath = vnoise(g / _Wavelength * 1.3 - tm * _Flow * 0.7 + sd * 2.3 + 17.0) * 0.5 + 0.5;
                float Rloc = R * lerp(0.3, 1.25, breath * breath * (3.0 - 2.0 * breath));

                float surge = 0.88 + 0.12 * vnoise(tm * 0.7 + sd);

                // Ambient "holographic column" spin: when _SpinSpeed != 0 each
                // filament's slot is reinterpreted as a fixed angular position
                // on a rotating cylinder (front hemisphere only, like looking
                // at a spinning barber pole edge-on) instead of a static
                // left-right offset - the whole bundle keeps rotating around
                // its own vertical axis in place. Off (0) reproduces the exact
                // old straight-bundle behavior, so existing pulse beams are
                // untouched.
                float spinOn = step(0.0001, abs(_SpinSpeed));
                float spinPhase = 6.28318530718 * _SpinSpeed * tm;

                float3 acc = float3(0, 0, 0);
                float3 glowAcc = float3(0, 0, 0);

                [unroll]
                for (int n = 0; n < FILS; n++)
                {
                    float fk = (float)n;
                    float h1 = hash11(fk * 7.13 + sd);
                    float h4 = hash11(fk * 9.91 + sd * 0.7 + 41.0);

                    // Fixed slot inside the bundle - no independent lateral
                    // drift at all, so a filament's left-right order among
                    // its neighbors never changes (no crossing/braiding).
                    // The whole set rides the shared centerline wander W and
                    // breathes together via Rloc, like parallel silk threads
                    // moving as one sheet instead of weaving over each other.
                    float o = clamp((fk / (FILS - 1.0) * 2.0 - 1.0) * 0.92 + (h1 - 0.5) * 0.3, -1.0, 1.0);

                    float ang = asin(o) + spinPhase;
                    float oSpin = sin(ang);
                    float depth = lerp(1.0, 0.3 + 0.7 * (cos(ang) * 0.5 + 0.5), spinOn);
                    float oAnim = lerp(o, oSpin, spinOn);

                    float off = W + Rloc * oAnim;
                    float d = (latMean - off) * invSlope;

                    // each filament surges and fades along its length...
                    float nb = vnoise(g / _Wavelength * 1.6 - tm * _Flow * 0.9 + o * 4.0 + sd * 0.5) * 0.5 + 0.5;
                    float life = lerp(0.1, 1.2, nb * nb * (3.0 - 2.0 * nb));
                    // ...and swells / thins
                    float nt = vnoise(g / _Wavelength * 2.4 - tm * _Flow + o * 3.0) * 0.5 + 0.5;
                    float sig = lerp(1.0, 2.8, h4 * h4) * lerp(0.7, 1.5, nt) * max(_GlowWidth, 0.05);
                    float sigE = sqrt(sig * sig + 0.25 * pxU * pxU);

                    float core = exp(-0.5 * (d * d) / (sigE * sigE)) * (sig / sigE);
                    float tail = 0.11 / (1.0 + (d * d) / (9.0 * sigE * sigE)); // soft lorentzian bloom
                    float inten = (core + tail) * life * lerp(1.0, 0.55, abs(o)) * lerp(0.8, 1.2, h1) * surge * depth;

                    // per-filament gradient: hot core -> base -> deep edge, easing with path
                    float m = saturate(1.0 - 0.8 * abs(o) + 0.3 * (tc - 0.5) + 0.2 * (nb - 0.5));
                    float3 fc = lerp(deepCol, baseCol, smoothstep(0.0, 0.55, m));
                    fc = lerp(fc, hotCol, smoothstep(0.5, 1.0, m) * 0.75);

                    acc += fc * inten;
                    glowAcc += baseCol * exp(-abs(d) / (sig * 8.0)) * 0.05 * life;
                }

                // Pads past a pinched end hold only the end glow, not filaments
                // (at open/seam ends there is no pad, so this stays 1 there).
                float inPath = lerp(1.0, smoothstep(-6.0, 4.0, yU), step(0.5, _PadB))
                             * lerp(1.0, smoothstep(-6.0, 4.0, _Height - yU), step(0.5, _PadT));
                acc *= inPath;
                glowAcc *= inPath;

                // ---- soft gradient "aurora" body behind the filaments --------------
                float latB = (latMean - W) * invSlope;
                float Rb = max(Rloc * 1.4 + 10.0 * pinch, 0.5);
                float prof = exp(-(latB * latB) / (Rb * Rb * 0.81));
                float flowMod = 0.55 + 0.45 * (vnoise(g / _Wavelength * 1.1 - tm * _Flow * 0.8 + sd * 4.1 + 29.0) * 0.5 + 0.5);
                float3 bodyCol = lerp(deepCol, baseCol, prof) * prof * flowMod * 0.72 * inPath;

                // ---- traveling head of the "shot" ---------------------------------
                float headY = (hp - 0.07) * _Height;
                float headFade = (1.0 - smoothstep(0.82, 1.0, _Progress)) * step(0.001, _Progress);
                float hdy = (yU - headY) / 22.0;
                float hdx = latB / (R * 0.8 + 12.0);
                float headGlow = exp(-(hdy * hdy)) * exp(-(hdx * hdx)) * 1.4 * headFade;

                // ---- compose: overexposed neon ---------------------------------------
                float3 raw = 1.0 - exp(-acc * 2.2);
                float lum = dot(acc, float3(0.3333, 0.3333, 0.3333));
                raw += hotCol * smoothstep(1.3, 3.2, lum) * 0.8;
                raw += glowAcc + bodyCol + baseCol * (nodeGlow + headGlow);

                // Gamma-lift brightening instead of a bigger multiplier
                // before the exp() above: a multiplier just saturates the
                // already-bright core to white faster and does nothing for
                // pixels once they're clipped - there's no such thing as
                // "brighter than white" in LDR. pow(x, 1/_Intensity) with
                // _Intensity > 1 instead lifts the dim halo/bloom/tail
                // (which is most of what reads against a busy background)
                // much more than it affects pixels already near white, so
                // more of the ribbon's cross-section reads as bright
                // instead of just a thin hot line - same idea as the
                // 2-layer additive overdraw we tried, but one pow() per
                // pixel instead of running this whole (expensive, 14
                // filaments x several noise taps each) shader twice.
                float3 col = pow(saturate(raw), 1.0 / max(_Intensity, 0.05));

                return fixed4(col, outAlpha);
            }
            ENDCG
        }
    }
}
