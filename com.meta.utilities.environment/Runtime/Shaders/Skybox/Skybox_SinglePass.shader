// Copyright (c) Meta Platforms, Inc. and affiliates.
// Single-pass skybox: only the "Universal Forward" pass of the sky Shader Graph.
// Unity draws a skybox whose shader has exactly 6 passes as a 6-sided skybox (one pass per cube face). URP 17.3 gives
// Shader Graph skyboxes 6 passes on Vulkan (it adds XRMotionVectors), so the sky graph itself must not be the skybox shader.
Shader "Meta/Environment/Skybox (Single Pass)"
{
    Properties
    {
        [Toggle(_IS_CELESTIAL_OBJECT)] _IS_CELESTIAL_OBJECT ("Is Celestial Object", Float) = 0
        [HDR] _Celestial_Light_Color ("Celestial Light Color", Color) = (1, 1, 1, 1)
        _Celestial_Light_Pitch ("Celestial Light Pitch", Range(0, 180)) = 0
        _Celestial_Light_Yaw ("Celestial Light Yaw", Range(0, 360)) = 0
        [NoScaleOffset] _Celestial_Texture ("Celestial Texture", 2D) = "white" {}
        [NoScaleOffset] [Normal] _Celestial_Normal_Map ("Celestial Normal Map", 2D) = "bump" {}
        [HDR] _Celestial_Color ("Celestial Color", Color) = (1, 1, 1, 1)
        _Celestial_Edge_Fade ("Celestial Edge Fade", Range(0, 2)) = 0.5
        [Toggle(_ENABLESTARS)] _ENABLESTARS ("EnableStars", Float) = 1
        _Star_Power ("Star Power", Float) = 1
        _Star_Scale ("Star Scale", Float) = 1
        [HDR] _Star_Color ("Star Color", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _Star_Texture ("Star Texture", 2D) = "black" {}
        _Sky_Zenith_Color ("Sky Zenith Color", Color) = (0.1755143, 0.5718953, 0.9396226, 1)
        _Sky_Horizon_Color ("Sky Horizon Color", Color) = (0.4886792, 0.7539268, 1, 1)
        _Ground_Color ("Ground Color", Color) = (0.245283, 0.245283, 0.245283, 0)
        _Sky_Blend_Offset ("Sky Blend Offset", Float) = 0
        _Sky_Blend_Power ("Sky Blend Power", Float) = 2
        [HDR] _Cloud_Sun_Color ("Cloud Sun Color", Color) = (1, 1, 1, 1)
        [HDR] _Cloud_Back_Color ("Cloud Back Color", Color) = (1, 1, 1, 1)
        _Cloud_Blend_Offset ("Cloud Blend Offset", Float) = 0
        _Cloud_Blend_Power ("Cloud Blend Power", Float) = 1
        [HDR] _Cloud_Ambient ("Cloud Ambient", Color) = (0.4897206, 0.6147841, 0.6698113, 1)
        _Cloud_Offset ("Cloud Offset", Range(0, 1)) = 0.75
        _Cloud_Scale ("Cloud Scale", Float) = 1
        _Cloud_Speed ("Cloud Speed", Float) = 0.001
        _Cloud_Direction ("Cloud Direction", Range(0, 1)) = 0.1
        [NoScaleOffset] _Cloud_Texture ("Cloud Texture", 2D) = "black" {}
        _Cloud_Opacity_Scale ("Cloud Opacity Scale", Float) = 1
        _Cloud_Opacity_Offset ("Cloud Opacity Offset", Float) = 0
        [NoScaleOffset] [Normal] _Cloud_Normal ("Cloud Normal", 2D) = "bump" {}
        _Front_Scatter_Power ("Front Scatter Power", Range(0, 1)) = 0.85
        _Back_Scatter_Power ("Back Scatter Power", Range(0, 1)) = 0.3
        _Scatter_Blend ("Scatter Blend", Range(0, 1)) = 0.5
        _Fog_Power ("Fog Power", Float) = 4
        _Fog_Offset ("Fog Offset", Float) = 0
        [Toggle(_USE_UNDERWATER_FOG)] _USE_UNDERWATER_FOG ("Use Underwater Fog", Float) = 0
        _Underwater_Fog_Height_Offset ("Underwater Fog Height Offset", Vector) = (0, 0, 0, 0)
        [HideInInspector] _CastShadows ("_CastShadows", Float) = 0
        [HideInInspector] _Surface ("_Surface", Float) = 0
        [HideInInspector] _Blend ("_Blend", Float) = 0
        [HideInInspector] _AlphaClip ("_AlphaClip", Float) = 0
        [HideInInspector] _SrcBlend ("_SrcBlend", Float) = 1
        [HideInInspector] _DstBlend ("_DstBlend", Float) = 0
        [HideInInspector] _SrcBlendAlpha ("_SrcBlendAlpha", Float) = 1
        [HideInInspector] _DstBlendAlpha ("_DstBlendAlpha", Float) = 0
        [ToggleUI] [HideInInspector] _ZWrite ("_ZWrite", Float) = 1
        [HideInInspector] _ZWriteControl ("_ZWriteControl", Float) = 2
        [HideInInspector] _ZTest ("_ZTest", Float) = 4
        [HideInInspector] _Cull ("_Cull", Float) = 2
        [HideInInspector] _AlphaToMask ("_AlphaToMask", Float) = 0
        [HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl ("_QueueControl", Float) = -1
        [HideInInspector] _XRMotionVectorsPass ("_XRMotionVectorsPass", Float) = 1
        [HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Background" "Queue" = "Background" "PreviewType" = "Skybox" }
        UsePass "Shader Graphs/SG_Skybox_New/UNIVERSAL FORWARD"
    }
    Fallback Off
}
