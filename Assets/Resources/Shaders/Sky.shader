// Hollowmark's day/night sky (driven by Assets/Scripts/Visuals/DayNight.cs): a zenith-to-horizon gradient,
// a sun with a warm glow, a moon, and twinkling stars that fade in after dusk. Works as a skybox in the
// Built-in pipeline and in URP.
Shader "Hollowmark/Sky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.17, 0.38, 0.82, 1)
        _Horizon ("Horizon", Color) = (0.62, 0.74, 0.9, 1)
        _Ground ("Below horizon", Color) = (0.3, 0.3, 0.28, 1)
        _SunDir ("Sun direction", Vector) = (0, 0.7, -0.7, 0)
        _SunColor ("Sun colour", Color) = (1, 0.95, 0.9, 1)
        _MoonDir ("Moon direction", Vector) = (0, 0.5, 0.8, 0)
        _MoonColor ("Moon colour", Color) = (0, 0, 0, 1)
        _Stars ("Stars", Range(0, 1)) = 0
        _Time2 ("Time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _Zenith, _Horizon, _Ground, _SunColor, _MoonColor;
            float4 _SunDir, _MoonDir;
            float _Stars, _Time2;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;

                // Gradient: horizon band to zenith above, fading to dark ground below.
                half3 col = lerp(_Horizon.rgb, _Zenith.rgb, pow(saturate(h), 0.45));
                col = lerp(col, _Ground.rgb, saturate(-h * 5.0));

                // Sun: a bright disc, a tight halo and a wide warm glow near the horizon.
                float3 sd = normalize(_SunDir.xyz);
                float cs = dot(d, sd);
                half disc = smoothstep(0.9993, 0.9996, cs);
                half halo = pow(saturate(cs), 64.0) * 0.6 + pow(saturate(cs), 6.0) * 0.25 * (1.0 - saturate(sd.y * 2.0));
                col += _SunColor.rgb * (disc * 18.0 + halo) * saturate(h * 20.0 + 1.0);

                // Moon: a pale disc with soft shading and a faint halo.
                float3 md = normalize(_MoonDir.xyz);
                float cm = dot(d, md);
                half moon = smoothstep(0.99955, 0.9998, cm);
                half mottling = 0.8 + 0.2 * Hash(floor(d * 900.0));
                col += _MoonColor.rgb * (moon * 2.2 * mottling + pow(saturate(cm), 200.0) * 0.12);

                // Stars: sparse points that twinkle, hidden near the horizon and behind the moonlit haze.
                if (_Stars > 0.001 && h > 0.0)
                {
                    float3 p = d * 260.0;
                    float3 cell = floor(p);
                    float r = Hash(cell);
                    float3 offs = float3(Hash(cell + 1.3), Hash(cell + 2.7), Hash(cell + 4.1)) * 0.6 + 0.2;
                    float dist = length(frac(p) - offs);
                    half star = step(0.993, r) * saturate(1.0 - dist * 3.5);
                    half twinkle = 0.65 + 0.35 * sin(_Time2 * (2.0 + r * 5.0) + r * 40.0);
                    col += star * twinkle * _Stars * saturate(h * 6.0) * lerp(half3(0.8, 0.85, 1.0), half3(1.0, 0.9, 0.75), Hash(cell + 7.0)) * 1.6;
                }
                return half4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
