// 화면 후처리: 빛 번짐(발광 표시등·형광등이 번진다), 영화 같은 톤 보정, 색수차, 비네팅, 필름 입자
Shader "Hidden/FactoryPost"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
        _BloomTex2 ("Bloom2", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex, _BloomTex, _BloomTex2;
    float4 _MainTex_TexelSize;
    float _Threshold, _BloomIntensity, _Vignette, _Grain, _Saturation, _Contrast, _Exposure, _Aberration, _Seed;

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

    v2f vert (appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    // 밝은 부분만 뽑으면서 절반 크기로 줄인다
    half4 fragPrefilter (v2f i) : SV_Target
    {
        float2 d = _MainTex_TexelSize.xy * 0.5;
        half3 c = tex2D(_MainTex, i.uv + d * float2(-1, -1)).rgb
                + tex2D(_MainTex, i.uv + d * float2( 1, -1)).rgb
                + tex2D(_MainTex, i.uv + d * float2(-1,  1)).rgb
                + tex2D(_MainTex, i.uv + d * float2( 1,  1)).rgb;
        c *= 0.25;
        half br = max(c.r, max(c.g, c.b));
        half k = saturate((br - _Threshold) / max(br, 0.0001));
        return half4(c * k, 1);
    }

    half4 blur (float2 uv, float2 dir)
    {
        float2 s = _MainTex_TexelSize.xy * dir;
        half3 c = tex2D(_MainTex, uv).rgb * 0.2270270270;
        c += tex2D(_MainTex, uv + s * 1.3846153846).rgb * 0.3162162162;
        c += tex2D(_MainTex, uv - s * 1.3846153846).rgb * 0.3162162162;
        c += tex2D(_MainTex, uv + s * 3.2307692308).rgb * 0.0702702703;
        c += tex2D(_MainTex, uv - s * 3.2307692308).rgb * 0.0702702703;
        return half4(c, 1);
    }

    half4 fragBlurH (v2f i) : SV_Target { return blur(i.uv, float2(1, 0)); }
    half4 fragBlurV (v2f i) : SV_Target { return blur(i.uv, float2(0, 1)); }
    half4 fragCopy (v2f i) : SV_Target { return tex2D(_MainTex, i.uv); }

    half3 aces (half3 x)
    {
        const float a = 2.51, b = 0.03, c = 2.43, d = 0.59, e = 0.14;
        return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
    }

    float hash (float2 p)
    {
        return frac(sin(dot(p, float2(12.9898, 78.233)) + _Seed) * 43758.5453);
    }

    half4 fragComposite (v2f i) : SV_Target
    {
        float2 uv = i.uv;
        float2 cen = uv - 0.5;
        // 가장자리로 갈수록 빨강·파랑이 살짝 어긋난다 (렌즈의 색수차)
        float ab = _Aberration * dot(cen, cen);
        half3 col;
        col.r = tex2D(_MainTex, uv + cen * ab).r;
        col.g = tex2D(_MainTex, uv).g;
        col.b = tex2D(_MainTex, uv - cen * ab).b;
        // 빛 번짐: 가까운 번짐과 넓은 번짐을 함께 더한다
        col += (tex2D(_BloomTex, uv).rgb * 0.6 + tex2D(_BloomTex2, uv).rgb * 0.9) * _BloomIntensity;
        col *= _Exposure;
        // 톤 매핑은 절반만 섞어 원래 색을 유지하면서 하이라이트가 부드럽게 눌리게 한다
        half3 g = lerp(col, aces(col), 0.55);
        g = (g - 0.5) * _Contrast + 0.5;
        half lum = dot(g, half3(0.299, 0.587, 0.114));
        g = lerp(lum.xxx, g, _Saturation);
        // 어두운 곳은 살짝 차갑게, 밝은 곳은 살짝 따뜻하게
        g += half3(-0.008, 0.002, 0.016) * (1.0 - lum) + half3(0.012, 0.006, -0.008) * lum * 0.5;
        float vig = 1.0 - _Vignette * smoothstep(0.35, 0.95, length(cen) * 1.35);
        g *= vig;
        g += (hash(uv * _ScreenParams.xy) - 0.5) * _Grain;
        return half4(saturate(g), 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass { CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragPrefilter
        ENDCG }
        Pass { CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragBlurH
        ENDCG }
        Pass { CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragBlurV
        ENDCG }
        Pass { CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragComposite
        ENDCG }
        Pass { CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragCopy
        ENDCG }
    }
}
