Shader "Custom RP/Lit"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Color", Color) = (0.5, 0.5, 0.5, 1.0)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        [Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
        [Toggle(_PREMULTIPLY_ALPHA)] _PremulAlpha ("Premultiply Alpha", Float) = 0
        [KeywordEnum(On, Clip, Dither, Off)] _Shadows ("Shadows", Float) = 0
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1

        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
    }

    CustomEditor "CustomShaderGUI"
    SubShader
    {
        Pass
        {
            Tags
            {
                "LightMode" = "CustomLit"
            }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma shader_feature _CLIPPING
            #pragma shader_feature _PREMULTIPLY_ALPHA
            #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            #pragma multi_compile_instancing
            #pragma shader_feature _RECEIVE_SHADOWS
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "Assets/Custom RP/ShaderLibrary/Common.hlsl"
            #include "Assets/Custom RP/ShaderLibrary/Surface.hlsl"
            #include "Assets/Custom RP/ShaderLibrary/Shadows.hlsl"
            #include "Assets/Custom RP/ShaderLibrary/Light.hlsl"
            #include "Assets/Custom RP/ShaderLibrary/BRDF.hlsl"
            #include "Assets/Custom RP/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
            UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
            UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
            UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
            UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
            UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 baseUV : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };


            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : VAR_POSITION;
                float3 normalWS : VAR_NORMAL;
                float2 baseUV : VAR_BASE_UV;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS, true);

                float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
                output.baseUV = input.baseUV * baseST.xy + baseST.zw;
                return output;
            }

            float4 LitPassFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
                float4 base = baseMap * baseColor;

                #if defined(_CLIPPING)
                clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
                // Discards fragment if param <= 0
                #endif

                Surface surface;
                surface.position = input.positionWS;
                surface.normal = input.normalWS;
                surface.color = base.rgb;
                surface.depth = -TransformWorldToView(input.positionWS).z;
                surface.alpha = base.a;
                surface.metallic = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
                surface.smoothness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
                surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
                surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);

                #if defined(_PREMULTIPLY_ALPHA)
                BRDF brdf = GetBRDF(surface, true);
                #else
                    BRDF brdf = GetBRDF(surface);
                #endif

                float3 color = GetLighting(surface, brdf);
                return float4(color, surface.alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Tags
            {
                "LightMode" = "ShadowCaster"
            }
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
            #pragma multi_compile_instancing
            #pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment


            #include "../ShaderLibrary/Common.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
            UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
            UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
            UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)


            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 baseUV : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 baseUV : VAR_BASE_UV;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowCasterPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE)
                #endif

                float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
                output.baseUV = input.baseUV * baseST.xy + baseST.zw;
                return output;
            }

            void ShadowCasterPassFragment(Varyings input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
                float4 base = baseMap * baseColor;
                #if defined(_SHADOWS_CLIP)
                clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
                #elif defined(_SHADOWS_DITHER)
                float dither = InterleavedGradientNoise(input.positionCS.xy, 0);
                clip(base.a - dither);
                #endif
            }
            ENDHLSL
        }
    }
}