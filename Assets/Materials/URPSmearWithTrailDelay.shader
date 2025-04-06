Shader "Custom/URPSmearWithTrailDelay"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _BaseMap ("Albedo (RGB)", 2D) = "white" {}
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        
        // Właściwości dla trailsa
        _SmearVelocity ("Smear Velocity", Vector) = (5,0,0,0)
        _TrailIntensity ("Trail Intensity", Range(0,1)) = 0.5
        _TrailColor ("Trail Color", Color) = (1,0,0,0.5) // Czerwony z alfą
    }
    SubShader
    {
        Tags 
        { 
            "RenderPipeline" = "UniversalPipeline" 
            "RenderType" = "Opaque" 
            "Queue" = "Geometry" 
        }
        LOD 100

        // Pass 1: Postać (Opaque)
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float3 normalOS : NORMAL; };
            struct Varyings { float2 uv : TEXCOORD0; float4 positionHCS : SV_POSITION; float3 normalWS : TEXCOORD1; };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Smoothness;
                half _Metallic;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            Varyings vert (Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 normal = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half3 lighting = mainLight.color * max(0, dot(normal, mainLight.direction));
                half3 color = albedo.rgb * lighting;
                return half4(color, 1.0); // Pełna nieprzezroczystość
            }
            ENDHLSL
        }

        // Pass 2: Trails (Transparent)
        Pass
        {
            Name "TrailPass"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float trailAlpha : TEXCOORD1; };

            CBUFFER_START(UnityPerMaterial)
                float4 _TrailColor;
                float3 _SmearVelocity;
                float _TrailIntensity;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output = (Varyings)0;
                float speed = length(_SmearVelocity);
                float3 smearDir = normalize(_SmearVelocity);
                float3 offset = -smearDir * speed * _TrailIntensity;
                float4 posOS = input.positionOS + float4(offset, 0.0);
                output.positionHCS = TransformObjectToHClip(posOS.xyz);
                output.trailAlpha = saturate(speed * 0.5) * _TrailIntensity;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                half4 col = _TrailColor;
                col.a *= input.trailAlpha;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}