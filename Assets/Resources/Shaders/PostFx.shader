// Built-in pipeline post-processing for Hollowmark (see Assets/Scripts/Visuals/Atmosphere.cs, class PostFx).
// Pass 0: bloom prefilter (threshold) + downsample. Pass 1: downsample blur. Pass 2: upsample (additive).
// Pass 3: final composite: bloom, exposure, filmic tone curve (HDR only), colour grade, contrast, saturation, vignette.
Shader "Hidden/Hollowmark/PostFx"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    float _Threshold, _Bloom, _Exposure, _Tonemap, _Vignette, _Saturation, _Contrast;
    float4 _Grade;

    half3 Box4(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d);
        half3 s = tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb
                + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb;
        return s * 0.25;
    }

    half4 FragPrefilter(v2f_img i) : SV_Target
    {
        half3 c = Box4(i.uv, 1.0);
        half bright = max(c.r, max(c.g, c.b));
        half knee = _Threshold * 0.5;
        half soft = clamp(bright - _Threshold + knee, 0, 2 * knee);
        soft = soft * soft / (4 * knee + 1e-4);
        half contrib = max(soft, bright - _Threshold) / max(bright, 1e-4);
        return half4(c * contrib, 1);
    }

    half4 FragDown(v2f_img i) : SV_Target { return half4(Box4(i.uv, 1.0), 1); }

    half4 FragUp(v2f_img i) : SV_Target { return half4(Box4(i.uv, 0.5), 1); }

    half3 Filmic(half3 x)
    {
        // Narkowicz's fit of the ACES curve.
        const half a = 2.51, b = 0.03, c = 2.43, d = 0.59, e = 0.14;
        return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
    }

    half4 FragFinal(v2f_img i) : SV_Target
    {
        half4 src = tex2D(_MainTex, i.uv);
        half3 c = src.rgb + tex2D(_BloomTex, i.uv).rgb * _Bloom;
        c *= _Exposure;
        if (_Tonemap > 0.5) c = Filmic(c * 0.8);
        c = saturate(c * _Grade.rgb);
        half lum = dot(c, half3(0.2126, 0.7152, 0.0722));
        c = lerp(lum.xxx, c, _Saturation);
        c = saturate((c - 0.5) * _Contrast + 0.5);
        float2 v = i.uv - 0.5;
        half vig = 1 - dot(v, v) * _Vignette * 2.2;
        c *= saturate(vig);
        return half4(c, src.a);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragPrefilter
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragDown
            ENDCG
        }
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragUp
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragFinal
            ENDCG
        }
    }
    Fallback Off
}
