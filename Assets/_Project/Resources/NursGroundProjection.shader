Shader "AirCombat/NursGroundProjection"
{
    Properties
    {
        _Centre ("Centre", Vector) = (0, 0, 0, 0)
        _Forward ("Forward", Vector) = (0, 0, 1, 0)
        _Radii ("Radii", Vector) = (5, 10, 0, 0)
        _Stroke ("Stroke", Float) = 0.65
        _TickLength ("Tick length", Float) = 1.8
        _MaxReceiverY ("Maximum receiver height", Float) = 73
        _Tint ("Tint", Color) = (1, 0.77, 0.27, 0.88)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Centre, _Forward, _Radii, _Tint;
                float _Stroke, _TickLength, _MaxReceiverY;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = float4(v.positionOS.xy, 0.5, 1.0);
                return o;
            }

            // Signed distance to a world-space rectangle. Used for the four
            // small radial ticks, mirrored about the centre of the ellipse.
            float BoxDistance(float2 p, float2 halfSize)
            {
                float2 d = abs(p) - halfSize;
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (rawDepth < 0.000001) return 0;
                    float deviceDepth = rawDepth;
                #else
                    if (rawDepth > 0.999999) return 0;
                    float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                float3 world = ComputeWorldSpacePosition(uv, deviceDepth, UNITY_MATRIX_I_VP);
                float2 delta = world.xz - _Centre.xz;
                float2 forward = normalize(_Forward.xz);
                float2 right = float2(forward.y, -forward.x);
                float2 p = float2(dot(delta, right), dot(delta, forward));
                float2 radii = max(_Radii.xy, 0.05);
                float2 q = p / radii;
                float qLength = length(q);
                // Approximate metric distance to the ellipse, so the stroke
                // has a consistent world-space width even when stretched.
                float gradient = length(q / radii) / max(qLength, 0.0001);
                float ring = abs(qLength - 1.0) / max(gradient, 0.0001);
                float halfStroke = min(_Stroke * 0.5, min(radii.x, radii.y) * 0.45);
                float distance = ring - halfStroke;
                if (_TickLength > 0.0)
                {
                    float2 a = abs(p);
                    float tickX = BoxDistance(a - float2(radii.x + _TickLength * 0.5, 0),
                        float2(_TickLength * 0.5, halfStroke));
                    float tickZ = BoxDistance(a - float2(0, radii.y + _TickLength * 0.5),
                        float2(halfStroke, _TickLength * 0.5));
                    distance = min(distance, min(tickX, tickZ));
                }
                // Clamp derivative at depth discontinuities: no smeared halo
                // between a roof edge and the much lower ground behind it.
                float aa = clamp(fwidth(distance) * 0.5, 0.025, max(halfStroke, 0.025));
                float coverage = 1.0 - smoothstep(-aa, aa, distance);
                if (world.y > _MaxReceiverY) coverage = 0;
                return half4(_Tint.rgb, _Tint.a * coverage);
            }
            ENDHLSL
        }
    }
}
