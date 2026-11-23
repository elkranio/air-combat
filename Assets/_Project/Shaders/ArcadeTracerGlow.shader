Shader "AirCombat/ArcadeTracerGlow"
{
    Properties { _Tint ("Tint", Color) = (1, 1, 1, 1) }
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
                // LineRenderer supplies width across UV.y: hot centre, soft edge.
                // Additive blending makes this visible over sand/green terrain,
                // with or without camera postprocessing bloom.
                half across = saturate(1.0h - abs(i.uv.y * 2.0h - 1.0h));
                half softness = across * across;
                return half4(i.color.rgb * _Tint.rgb,
                    i.color.a * _Tint.a * softness);
            }
            ENDHLSL
        }
    }
}
