Shader "Custom/SeamlessWater"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0, 0.5, 1, 0.5)
        _BaseMap("Base Map", 2D) = "white" {}
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        _Metallic("Metallic", Range(0, 1)) = 0.0
        _Opacity("Opacity", Range(0, 1)) = 0.5
        _Softness("Softness / Shoreline Fade", Range(0.01, 3.0)) = 1.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On 
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // Standard URP keywords
            #pragma multi_compile_fog
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 normalWS : NORMAL;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _Smoothness;
                float _Metallic;
                float _Opacity;
                float _Softness;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.screenPos = ComputeScreenPos(vertexInput.positionCS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 1. Calculate Soft Edge / Depth Fade
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                
                float sceneDepth, surfaceDepth;
                
                // ORTHOGRAPHIC SUPPORT
                if (unity_OrthoParams.w > 0.5) 
                {
                    #if UNITY_REVERSED_Z
                        sceneDepth = (_ProjectionParams.y - _ProjectionParams.z) * rawDepth + _ProjectionParams.z;
                    #else
                        sceneDepth = (_ProjectionParams.z - _ProjectionParams.y) * rawDepth + _ProjectionParams.y;
                    #endif
                    surfaceDepth = input.positionCS.z; 
                }
                else 
                {
                    sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                    surfaceDepth = input.screenPos.w;
                }
                
                // Distance between water surface and what's behind it
                float depthDiff = sceneDepth - surfaceDepth;
                
                // Fade alpha when getting close to other objects (like neighbors or ground)
                float fade = saturate(depthDiff / _Softness);

                // 2. Sample Texture and Color
                float4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float4 color = tex * _BaseColor;
                float finalAlpha = color.a * _Opacity * fade;
                
                // 3. Simple Lighting
                float3 lightDir = normalize(_MainLightPosition.xyz);
                float3 normal = normalize(input.normalWS);
                float diffuse = saturate(dot(normal, lightDir)) * 0.5 + 0.5; // Soft half-lambert
                
                return float4(color.rgb * diffuse, finalAlpha);
            }
            ENDHLSL
        }
    }
}
