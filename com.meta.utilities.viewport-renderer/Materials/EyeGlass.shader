// Copyright (c) Meta Platforms, Inc. and affiliates.

Shader "Unlit/EyeGlass"
{
    Properties
    {
        _BaseColor ("Example Colour", Color) = (0.5, 0.5, 0.5, 0.0)
        _Smoothness ("Smoothness", Float) = 0.9
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque+100"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 300

        Pass
        {
            Name "StencilWrite"
            Tags { "LightMode" = "UniversalForward" }

            Stencil
            {
		        Ref 1
		        Comp Equal
		        ReadMask 1
		        WriteMask 0
            }

            ZWrite Off
            ZTest Always
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma multi_compile_instancing

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : NORMAL;
                float3 viewDirWS    : VIEWDIR;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 1);
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = vertexInput.positionCS;
                o.normalWS =  TransformObjectToWorldNormal(v.normalOS);
                o.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionCS);
                OUTPUT_LIGHTMAP_UV(v.lightmapUV, unity_LightmapST, OUT.lightmapUV);
                OUTPUT_SH(o.normalWS.xyz, o.vertexSH);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                InputData inputData = (InputData)0;
                inputData.normalWS = NormalizeNormalPerPixel(i.normalWS);
                inputData.viewDirectionWS = SafeNormalize(i.viewDirWS);
                inputData.bakedGI = SAMPLE_GI(i.lightmapUV, i.vertexSH, inputData.normalWS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.alpha = _BaseColor.a;
                surfaceData.albedo = _BaseColor.rgb;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = float3(0, 0, 1);
                surfaceData.occlusion = 1;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                return color;
            }

            ENDHLSL
        }

    }
}
