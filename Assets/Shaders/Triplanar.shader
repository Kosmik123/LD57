// Triplanar PBR dla jaskin: probkowanie z pozycji swiata (tiling w metrach), blend po normalnej (ostrosc 4),
// normal mapy whiteout, osobna tekstura/tint dla powierzchni skierowanych w gore (mech/trawa).
// URP 17. Shader trzeba dodac do Graphics Settings > Always Included Shaders, jesli ktos bedzie go szukal przez Shader.Find w buildzie.
Shader "LD57/Triplanar"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo (rzut ze swiata)", 2D) = "white" {}
        [MainColor] _BaseColor("Kolor", Color) = (1,1,1,1)
        [Normal] _BumpMap("Normal", 2D) = "bump" {}
        _BumpScale("Sila normal", Float) = 1.0
        _OcclusionMap("Maska AO", 2D) = "white" {}
        _Occlusion("Sila AO", Range(0,1)) = 0.6
        _TopMap("Tekstura gory (mech/trawa)", 2D) = "white" {}
        _TopColor("Kolor gory", Color) = (0.5,0.8,0.3,1)
        _TopThreshold("Prog gory (normalna.y)", Range(0,1)) = 0.6
        _Tiling("Tiling (metry swiata)", Float) = 4.0
        _Smoothness("Gladkosc", Range(0,1)) = 0.15
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl" // LerpWhiteTo dla Shadows.hlsl

        TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);      SAMPLER(sampler_BumpMap);
        TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
        TEXTURE2D(_TopMap);       SAMPLER(sampler_TopMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _TopColor;
            half _BumpScale;
            half _Occlusion;
            half _TopThreshold;
            half _Tiling;
            half _Smoothness;
        CBUFFER_END

        // wagi osi: ostrosc 4, suma = 1
        half3 TriWeights(half3 n)
        {
            half3 w = pow(abs(n), 4);
            return w / max(dot(w, half3(1, 1, 1)), 1e-4h);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs vn = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = vp.positionCS;
                OUT.positionWS = vp.positionWS;
                OUT.normalWS = vn.normalWS;
                OUT.fogFactor = ComputeFogFactor(vp.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 nGeo = normalize(IN.normalWS);
                half3 w = TriWeights(nGeo);
                float inv = rcp(max(_Tiling, 0.01));
                float3 p = IN.positionWS * inv;
                float2 uvX = p.zy, uvY = p.xz, uvZ = p.xy;

                // albedo
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvX).rgb * w.x
                             + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvY).rgb * w.y
                             + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvZ).rgb * w.z;
                albedo *= _BaseColor.rgb;

                // gora: mech/trawa, miekki prog po normalnej geometrii
                half topMask = smoothstep(_TopThreshold, saturate(_TopThreshold + 0.2h), nGeo.y);
                half3 topAlbedo = SAMPLE_TEXTURE2D(_TopMap, sampler_TopMap, uvY * 0.75).rgb * _TopColor.rgb;
                albedo = lerp(albedo, topAlbedo, topMask);

                // AO
                half ao = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uvX).r * w.x
                        + SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uvY).r * w.y
                        + SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uvZ).r * w.z;
                ao = lerp(1.0h, ao, _Occlusion);

                // normalne: whiteout blend
                half3 tx = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvX), _BumpScale);
                half3 ty = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvY), _BumpScale);
                half3 tz = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvZ), _BumpScale);
                tx = half3(tx.xy + nGeo.zy, abs(tx.z) * nGeo.x);
                ty = half3(ty.xy + nGeo.xz, abs(ty.z) * nGeo.y);
                tz = half3(tz.xy + nGeo.xy, abs(tz.z) * nGeo.z);
                half3 normalWS = normalize(tx.zyx * w.x + ty.xzy * w.y + tz.xyz * w.z);
                normalWS = normalize(lerp(normalWS, nGeo, topMask * 0.5h));   // mech wygladza gore

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.positionCS = IN.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(IN.positionWS, 1.0), IN.fogFactor);
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                inputData.vertexLighting = VertexLighting(IN.positionWS, normalWS);
            #else
                inputData.vertexLighting = half3(0, 0, 0);
            #endif
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.metallic = 0.0h;
                s.specular = half3(0, 0, 0);
                s.smoothness = _Smoothness * (1.0h - topMask * 0.4h);
                s.occlusion = ao;
                s.emission = half3(0, 0, 0);
                s.alpha = 1.0h;
                s.normalTS = half3(0, 0, 1);

                half4 color = UniversalFragmentPBR(inputData, s);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0h;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half frag(Varyings IN) : SV_Target { return IN.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 n = normalize(IN.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(n);
                float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
                return half4(PackFloat2To888(remapped), 0.0);
            #else
                return half4(n, 0.0);
            #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
