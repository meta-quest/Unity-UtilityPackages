// Copyright (c) Meta Platforms, Inc. and affiliates.

Shader "Unlit/EyeStencilWrite"
{
    Properties
    {
    }

    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    struct Attributes
    {
        float3 positionOS : POSITION;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings vert (Attributes v)
    {
        Varyings o;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

        VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
        o.positionCS = vertexInput.positionCS;
        // Force a bias towards the camera to prevent z fighting
        o.positionCS.z += o.positionCS.w * 0.0001;
        return o;
    }

    half4 frag (Varyings i) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        return unity_FogColor;
    }

    ENDHLSL
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
		        Comp Always
                Pass Replace
                ZFail Keep
		        ReadMask 0
		        WriteMask 1
            }

            ZWrite Off
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma multi_compile_instancing

            #pragma vertex vert
            #pragma fragment frag

            ENDHLSL
        }

        Pass
        {
            Name "DepthWrite"
            Tags { "LightMode" = "UniversalForward" }

            Stencil
            {
		        Ref 1
		        Comp Equal
		        ReadMask 1
		        WriteMask 0
            }

            ZWrite On
            ZTest Always

            HLSLPROGRAM
            #pragma multi_compile_instancing

            #pragma vertex vertDepthBias
            #pragma fragment frag

            Varyings vertDepthBias (Attributes v)
            {
                Varyings o;
                o = vert(v);
                o.positionCS.z = 0;
                return o;
            }

            ENDHLSL
        }
    }
}
