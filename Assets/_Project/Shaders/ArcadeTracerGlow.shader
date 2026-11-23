Shader "AirCombat/ArcadeTracerGlow"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Intensity ("Emission strength", Range(0, 8)) = 1
        _Radial ("Radial disc (otherwise streak)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Intensity;
                half _Radial;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                // Streak: fall off across width AND toward the trailing end.
                // Bright head: radial falloff in both UV directions.
                // Mesh UVs come directly from LineRenderer (no texture asset).
                half across = saturate(1.0h - abs(i.uv.y * 2.0h - 1.0h));
                half widthFade = across * across * (3.0h - 2.0h * across);
                half lengthFade = lerp(.45h, 1.0h, saturate(i.uv.x));
                half2 discUv = (i.uv - .5h) * 2.0h;
                half radial = saturate(1.0h - dot(discUv, discUv));
                half radialFade = radial * radial;
                half mask = lerp(widthFade * lengthFade, radialFade, _Radial);
                return half4(i.color.rgb * _Tint.rgb * _Intensity,
                    saturate(i.color.a * _Tint.a * mask));
            }
            ENDHLSL
        }
    }
}
