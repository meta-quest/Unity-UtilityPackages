// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;
#if URP_17_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace Meta.Utilities.ViewportRenderer
{
    /// <summary>
    /// Renders the forward passes to a smaller viewport covering both eyes and with a tilted and adjusted projection matrix for use with telescope rendering
    /// </summary>
    [ExecuteAlways]
    public class ViewportRenderer : MonoBehaviour
    {
        public const int STENCIL_BIT = 1;

        private bool m_prewarm = false;
        private bool m_isRegistered = false;

        public class ViewportRenderPass : DrawObjectsPass
        {
            private static readonly int s_fogParams = Shader.PropertyToID("unity_FogParams");

            public ViewportRenderer ViewportRenderer;

            public ViewportRenderPass(string profilerTag, ShaderTagId[] shaderTagIds, bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask, StencilState stencilState, int stencilReference) : base(profilerTag, shaderTagIds, opaque, evt, renderQueueRange, layerMask, stencilState, stencilReference)
            {
#if URP_17_OR_NEWER
                m_shaderTagIds = new(shaderTagIds);
                m_filteringSettings = new FilteringSettings(renderQueueRange, layerMask);
                m_renderStateBlock = new RenderStateBlock(RenderStateMask.Stencil)
                {
                    stencilReference = stencilReference,
                    stencilState = stencilState,
                };
#endif
            }

#if !URP_17_3_OR_NEWER || URP_COMPATIBILITY_MODE
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cmd = renderingData.commandBuffer;
                if (cmd == null) return;

                try
                {
                    ref var cameraData = ref renderingData.cameraData;
                    var mainCamera = cameraData.camera;
                    var lensCamera = ViewportRenderer.m_lensCamera;
                    var scaleFactor = new Vector2(ScalableBufferManager.widthScaleFactor, ScalableBufferManager.heightScaleFactor);

                    // Cannot render without a lens camera
                    if (lensCamera == null) return;

                    // A lens mesh is required for sizing and stenciling
                    if (ViewportRenderer.m_lensMesh == null) return;

                    // Lens mesh handles writing stencil bits
                    cmd.DrawRenderer(ViewportRenderer.m_lensMesh, ViewportRenderer.m_lensMesh.sharedMaterial);

                    // Determine the pixel rect for each eye
                    var mesh = ViewportRenderer.m_lensMesh.GetComponent<MeshFilter>().sharedMesh;
                    var meshTransform = ViewportRenderer.m_lensMesh.transform.localToWorldMatrix;
                    var min = Vector2.positiveInfinity;
                    var max = Vector2.negativeInfinity;
                    Span<Rect> eyeRects = stackalloc Rect[2];
                    var cameraTarget = cameraData.cameraTargetDescriptor;
                    var screenSize = new Vector3(cameraTarget.width * scaleFactor.x, cameraTarget.height * scaleFactor.y);
                    for (var i = 0; i < 2; i++)
                    {
                        var eyeRect = GetBoundsRect(meshTransform, mesh.bounds, cameraData.GetProjectionMatrix(i) * cameraData.GetViewMatrix(i));
                        if (eyeRect.xMin >= 1f || eyeRect.yMin >= 1f || eyeRect.xMax <= -1f || eyeRect.yMax <= -1f) continue;
                        if (eyeRect.width <= 0f || eyeRect.height <= 0f) continue;
                        eyeRect.position = Vector2.Scale(eyeRect.position + Vector2.one, screenSize * 0.5f);
                        eyeRect.size = Vector2.Scale(eyeRect.size, screenSize * 0.5f);
                        eyeRects[i] = eyeRect;
                        min = Vector2.Min(min, eyeRect.min);
                        max = Vector2.Max(max, eyeRect.max);
                    }

                    // In single-pass mode, the viewport must cover both eyes
                    min = Vector2.Max(cameraData.pixelRect.min, min);
                    max = Vector2.Min(cameraData.pixelRect.max, max);
                    var clippedRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

                    // Dont render if nothing is visible
                    if (clippedRect.width <= 1f || clippedRect.height <= 1f)
                    {
                        if (ViewportRenderer.m_prewarm)
                        {
                            // Force render so that shaders are loaded
                            clippedRect = new Rect(0, 0, 1, 1);
                        }
                        else
                        {
                            return;
                        }
                    }

                    // Adjust fog density
                    var fogParams = Shader.GetGlobalVector(s_fogParams);
                    cmd.SetGlobalVector(s_fogParams, new(
                        fogParams.x * ViewportRenderer.m_fogDensityFactor,
                        fogParams.y * ViewportRenderer.m_fogDensityFactor,
                        fogParams.z, fogParams.w));
                    cmd.SetViewport(clippedRect);

                    // Get camera matrices
                    lensCamera.aspect = 1f;
                    var projection = lensCamera.projectionMatrix;
                    var view = mainCamera.worldToCameraMatrix;
                    var viewPos = view.MultiplyVector(-lensCamera.transform.position);
                    view.m03 = viewPos.x; view.m13 = viewPos.y; view.m23 = viewPos.z;

                    // Camera matrices need to be adjusted to clip rect
                    Matrix4x4 AdjustProjection(Matrix4x4 projection, Rect eyeRect)
                    {
                        var localFacing = mainCamera.transform.InverseTransformDirection(ViewportRenderer.transform.forward);
                        localFacing /= localFacing.z;
                        projection.m02 += 2f * (clippedRect.center.x - eyeRect.center.x) / clippedRect.width;
                        projection.m12 += 2f * (clippedRect.center.y - eyeRect.center.y) / clippedRect.height;
                        projection.m00 *= eyeRect.height / clippedRect.width;
                        projection.m11 *= eyeRect.height / clippedRect.height;
                        projection.m02 += projection.m00 * localFacing.x * ViewportRenderer.m_trackingFactor;
                        projection.m12 += projection.m11 * localFacing.y * ViewportRenderer.m_trackingFactor;
                        return projection;
                    }

                    // Apply camera matrices
                    if (cameraData.xr.enabled && cameraData.xr.singlePassEnabled)
                    {
                        for (var i = 0; i < 2; i++)
                        {
                            var eyeProj = AdjustProjection(projection, eyeRects[i]);
#if URP_14_OR_NEWER
                            XRBuiltinShaderConstants.UpdateBuiltinShaderConstants(view, eyeProj, true, i);
#else
                            XRBuiltinShaderConstants.UpdateBuiltinShaderConstants(
                                view, eyeProj, true, i, false, Matrix4x4.identity);
#endif
                        }
                        XRBuiltinShaderConstants.SetBuiltinShaderConstants(cmd);
                    }
                    else
                    {
                        var eyeProj = AdjustProjection(projection, eyeRects[0]);
                        cmd.SetViewProjectionMatrices(view, eyeProj);
                    }

                    // Render objects
                    base.Execute(context, ref renderingData);

                    // This needs to be stenciled to render only within the telescope lens
                    // otherwise, using fog colour should get us a reasonable result
                    //context.DrawSkybox(lensCamera);

                    // Restore camera data
                    cmd.SetGlobalVector(s_fogParams, fogParams);
                    cmd.SetViewport(new(0f, 0f, screenSize.x, screenSize.y));

                    if (cameraData.xr.enabled && cameraData.xr.singlePassEnabled)
                    {
                        XRBuiltinShaderConstants.Update(cameraData.xr, cmd, true);
                    }
                    else
                    {
                        cmd.SetViewProjectionMatrices(mainCamera.worldToCameraMatrix, mainCamera.projectionMatrix);
                    }

                    // Draw the shiny overlay mesh
                    if (ViewportRenderer.m_lensOverlay != null)
                    {
                        cmd.DrawRenderer(ViewportRenderer.m_lensOverlay, ViewportRenderer.m_lensOverlay.sharedMaterial);
                    }

                    if (ViewportRenderer.m_prewarm)
                    {
                        ViewportRenderer.CancelPrewarm();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
#endif

#if URP_17_OR_NEWER
            private readonly System.Collections.Generic.List<ShaderTagId> m_shaderTagIds;
            private readonly FilteringSettings m_filteringSettings;
            private readonly RenderStateBlock m_renderStateBlock;

            private class ViewportPassData
            {
                internal ViewportRenderer Renderer;
                internal UniversalCameraData CameraData;
                internal TextureHandle Color;
                internal RendererListHandle Objects;
                internal bool Visible;
                internal bool SinglePass;
                internal Rect ClippedRect;
                internal Vector2 ScreenSize;
                internal Matrix4x4 View;
                internal Matrix4x4 EyeProjection0;
                internal Matrix4x4 EyeProjection1;
                internal Vector4 FogParams;
                internal Matrix4x4 CameraView0, CameraView1, CameraProjection0, CameraProjection1;
                internal Matrix4x4 MainView, MainProjection;
            }

            // Render Graph version of Execute: same steps (lens stencil, adjusted viewport and projection, objects,
            // restore, overlay) in one raster pass, so it can merge with the camera's main render pass.
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var lensCamera = ViewportRenderer.m_lensCamera;
                // Cannot render without a lens camera; a lens mesh is required for sizing and stenciling
                if (lensCamera == null || ViewportRenderer.m_lensMesh == null) return;

                var cameraData = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var lightData = frameData.Get<UniversalLightData>();
                var resourceData = frameData.Get<UniversalResourceData>();

                using var builder = renderGraph.AddRasterRenderPass<ViewportPassData>("Viewport Renderer", out var passData, profilingSampler);
                passData.Renderer = ViewportRenderer;
                passData.CameraData = cameraData;
                passData.Color = resourceData.activeColorTexture;
                passData.SinglePass = cameraData.xr.enabled && cameraData.xr.singlePassEnabled;
                ComputeViewport(cameraData, passData);

                var drawSettings = RenderingUtils.CreateDrawingSettings(m_shaderTagIds, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
                var stateBlocks = new Unity.Collections.NativeArray<RenderStateBlock>(1, Unity.Collections.Allocator.Temp);
                stateBlocks[0] = m_renderStateBlock;
                var tagValues = new Unity.Collections.NativeArray<ShaderTagId>(1, Unity.Collections.Allocator.Temp);
                tagValues[0] = ShaderTagId.none;
                var listParams = new RendererListParams(renderingData.cullResults, drawSettings, m_filteringSettings)
                {
                    tagValues = tagValues,
                    stateBlocks = stateBlocks,
                    isPassTagName = false,
                };
                // Temp arrays: the render graph can create the list after this call, so they are not disposed here
                passData.Objects = renderGraph.CreateRendererList(listParams);

                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                builder.UseRendererList(passData.Objects);
                if (resourceData.mainShadowsTexture.IsValid()) builder.UseTexture(resourceData.mainShadowsTexture, AccessFlags.Read);
                if (resourceData.additionalShadowsTexture.IsValid()) builder.UseTexture(resourceData.additionalShadowsTexture, AccessFlags.Read);
                builder.AllowGlobalStateModification(true);
                if (cameraData.xr.enabled)
                {
                    // Same foveation rule as URP's own intermediate passes (DrawObjectsPass), so this pass can merge with them
                    var canFoveateIntermediatePasses = !Application.isMobilePlatform || XRSystem.GetRenderViewportScale() == 1f;
                    builder.EnableFoveatedRasterization(cameraData.xr.supportsFoveatedRendering && (canFoveateIntermediatePasses || resourceData.isActiveTargetBackBuffer));
#if URP_17_3_OR_NEWER
                    builder.SetExtendedFeatureFlags(ExtendedFeatureFlags.MultiviewRenderRegionsCompatible);
#endif
                }

                builder.SetRenderFunc(static (ViewportPassData data, RasterGraphContext context) => ExecuteRenderGraph(data, context.cmd));

                if (passData.Visible && ViewportRenderer.m_prewarm)
                {
                    ViewportRenderer.CancelPrewarm();
                }
            }

            // Same viewport and matrix computation as Execute, done while recording.
            private void ComputeViewport(UniversalCameraData cameraData, ViewportPassData data)
            {
                var mainCamera = cameraData.camera;
                var lensCamera = ViewportRenderer.m_lensCamera;
                var scaleFactor = new Vector2(ScalableBufferManager.widthScaleFactor, ScalableBufferManager.heightScaleFactor);

                var mesh = ViewportRenderer.m_lensMesh.GetComponent<MeshFilter>().sharedMesh;
                var meshTransform = ViewportRenderer.m_lensMesh.transform.localToWorldMatrix;
                var min = Vector2.positiveInfinity;
                var max = Vector2.negativeInfinity;
                Span<Rect> eyeRects = stackalloc Rect[2];
                var cameraTarget = cameraData.cameraTargetDescriptor;
                var screenSize = new Vector3(cameraTarget.width * scaleFactor.x, cameraTarget.height * scaleFactor.y);
                for (var i = 0; i < 2; i++)
                {
                    var eyeRect = GetBoundsRect(meshTransform, mesh.bounds, cameraData.GetProjectionMatrix(i) * cameraData.GetViewMatrix(i));
                    if (eyeRect.xMin >= 1f || eyeRect.yMin >= 1f || eyeRect.xMax <= -1f || eyeRect.yMax <= -1f) continue;
                    if (eyeRect.width <= 0f || eyeRect.height <= 0f) continue;
                    eyeRect.position = Vector2.Scale(eyeRect.position + Vector2.one, screenSize * 0.5f);
                    eyeRect.size = Vector2.Scale(eyeRect.size, screenSize * 0.5f);
                    eyeRects[i] = eyeRect;
                    min = Vector2.Min(min, eyeRect.min);
                    max = Vector2.Max(max, eyeRect.max);
                }

                // In single-pass mode, the viewport must cover both eyes
                min = Vector2.Max(cameraData.pixelRect.min, min);
                max = Vector2.Min(cameraData.pixelRect.max, max);
                var clippedRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

                // Dont render if nothing is visible
                data.Visible = true;
                if (clippedRect.width <= 1f || clippedRect.height <= 1f)
                {
                    if (ViewportRenderer.m_prewarm)
                    {
                        // Force render so that shaders are loaded
                        clippedRect = new Rect(0, 0, 1, 1);
                    }
                    else
                    {
                        data.Visible = false;
                        return;
                    }
                }

                // Adjust fog density
                var fogParams = Shader.GetGlobalVector(s_fogParams);
                data.FogParams = fogParams;
                data.ClippedRect = clippedRect;
                data.ScreenSize = screenSize;

                // Get camera matrices
                lensCamera.aspect = 1f;
                var projection = lensCamera.projectionMatrix;
                var view = mainCamera.worldToCameraMatrix;
                var viewPos = view.MultiplyVector(-lensCamera.transform.position);
                view.m03 = viewPos.x; view.m13 = viewPos.y; view.m23 = viewPos.z;

                // Camera matrices need to be adjusted to clip rect
                Matrix4x4 AdjustProjection(Matrix4x4 projection, Rect eyeRect)
                {
                    var localFacing = mainCamera.transform.InverseTransformDirection(ViewportRenderer.transform.forward);
                    localFacing /= localFacing.z;
                    projection.m02 += 2f * (clippedRect.center.x - eyeRect.center.x) / clippedRect.width;
                    projection.m12 += 2f * (clippedRect.center.y - eyeRect.center.y) / clippedRect.height;
                    projection.m00 *= eyeRect.height / clippedRect.width;
                    projection.m11 *= eyeRect.height / clippedRect.height;
                    projection.m02 += projection.m00 * localFacing.x * ViewportRenderer.m_trackingFactor;
                    projection.m12 += projection.m11 * localFacing.y * ViewportRenderer.m_trackingFactor;
                    return projection;
                }

                data.View = view;
                data.EyeProjection0 = AdjustProjection(projection, eyeRects[0]);
                data.EyeProjection1 = AdjustProjection(projection, eyeRects[1]);
                data.MainView = mainCamera.worldToCameraMatrix;
                data.MainProjection = mainCamera.projectionMatrix;
                if (data.SinglePass)
                {
                    data.CameraView0 = cameraData.xr.GetViewMatrix(0);
                    data.CameraView1 = cameraData.xr.GetViewMatrix(1);
                    data.CameraProjection0 = cameraData.xr.GetProjMatrix(0);
                    data.CameraProjection1 = cameraData.xr.GetProjMatrix(1);
                }
            }

            private static void ExecuteRenderGraph(ViewportPassData data, RasterCommandBuffer cmd)
            {
                var renderer = data.Renderer;
                // Same projection flip as URP's own passes: depends on whether the target is a texture or the back buffer
                var isYFlipped = data.CameraData.IsRenderTargetProjectionMatrixFlipped(data.Color);

                // Lens mesh handles writing stencil bits
                cmd.DrawRenderer(renderer.m_lensMesh, renderer.m_lensMesh.sharedMaterial);
                if (!data.Visible) return;

                cmd.SetGlobalVector(s_fogParams, new(
                    data.FogParams.x * renderer.m_fogDensityFactor,
                    data.FogParams.y * renderer.m_fogDensityFactor,
                    data.FogParams.z, data.FogParams.w));
                cmd.SetViewport(data.ClippedRect);

                // Apply camera matrices (XR views are always rendered into textures)
                if (data.SinglePass)
                {
                    XRBuiltinShaderConstants.UpdateBuiltinShaderConstants(data.View, data.EyeProjection0, true, 0);
                    XRBuiltinShaderConstants.UpdateBuiltinShaderConstants(data.View, data.EyeProjection1, true, 1);
                    XRBuiltinShaderConstants.SetBuiltinShaderConstants(cmd);
                }
                else
                {
                    RenderingUtils.SetViewAndProjectionMatrices(cmd, data.View, GL.GetGPUProjectionMatrix(data.EyeProjection0, isYFlipped), false);
                }

                // Render objects
                cmd.DrawRendererList(data.Objects);

                // Restore camera data
                cmd.SetGlobalVector(s_fogParams, data.FogParams);
                cmd.SetViewport(new(0f, 0f, data.ScreenSize.x, data.ScreenSize.y));
                if (data.SinglePass)
                {
                    XRBuiltinShaderConstants.UpdateBuiltinShaderConstants(data.CameraView0, data.CameraProjection0, true, 0);
                    XRBuiltinShaderConstants.UpdateBuiltinShaderConstants(data.CameraView1, data.CameraProjection1, true, 1);
                    XRBuiltinShaderConstants.SetBuiltinShaderConstants(cmd);
                }
                else
                {
                    RenderingUtils.SetViewAndProjectionMatrices(cmd, data.MainView, GL.GetGPUProjectionMatrix(data.MainProjection, isYFlipped), false);
                }

                // Draw the shiny overlay mesh
                if (renderer.m_lensOverlay != null)
                {
                    cmd.DrawRenderer(renderer.m_lensOverlay, renderer.m_lensOverlay.sharedMaterial);
                }
            }
#endif

            private Rect GetBoundsRect(Matrix4x4 worldMatrix, Bounds bounds, Matrix4x4 viewProj)
            {
                var min = Vector3.positiveInfinity;
                var max = Vector3.negativeInfinity;
                for (var i = 0; i < 8; i++)
                {
                    var corner = bounds.min + Vector3.Scale(bounds.size, new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1));
                    corner = worldMatrix.MultiplyPoint3x4(corner);
                    var clipPos = viewProj * new Vector4(corner.x, corner.y, corner.z, 1f);
                    var spos = (Vector3)clipPos / Mathf.Abs(clipPos.w);
                    min = Vector3.Min(min, spos);
                    max = Vector3.Max(max, spos);
                }
                if (max.z < 0f) return default;
                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
        }

        [SerializeField, Tooltip("Camera used for telescope viewport properties")]
        private Camera m_lensCamera;
        [SerializeField, Tooltip("Rendered before the telescope viewport to setup stenciling")]
        private MeshRenderer m_lensMesh;
        [SerializeField, Tooltip("Rendered after the telescope viewport as an overlay")]
        private MeshRenderer m_lensOverlay;
        [SerializeField, Tooltip("Multiplied into the fog density when rendering the telescope viewport")]
        private float m_fogDensityFactor = 1f;
        [SerializeField, Tooltip("How much the telescope should be fixed to its forward direction")]
        private float m_trackingFactor = 0.75f;

        private ViewportRenderPass m_renderPass;

        protected void OnEnable()
        {
            UpdateRegistered();
        }
        protected void OnDisable()
        {
            UpdateRegistered();
        }

        public void MarkForPrewarm()
        {
            m_prewarm = true;
            UpdateRegistered();
        }
        public void CancelPrewarm()
        {
            m_prewarm = false;
            UpdateRegistered();
        }

        private void UpdateRegistered()
        {
            var register = enabled || m_prewarm;
            if (m_isRegistered == register) return;
            m_isRegistered = register;

            if (m_isRegistered)
            {
                RenderPipelineManager.beginCameraRendering += OnBeginCamera;

                var stencilState = StencilState.defaultValue;
                stencilState.SetCompareFunction(CompareFunction.Equal);
                stencilState.SetPassOperation(StencilOp.Keep);
                stencilState.readMask = 1;
                var tags = new ShaderTagId[] { new("SRPDefaultUnlit"), new("UniversalForward"), new("UniversalForwardOnly") };
                m_renderPass = new("Viewport Renderer", tags, false, RenderPassEvent.AfterRenderingTransparents,
                    RenderQueueRange.all, m_lensCamera.cullingMask, stencilState, STENCIL_BIT)
                {
                    ViewportRenderer = this,
                };
            }
            else
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            }
        }

        private void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            cam.GetUniversalAdditionalCameraData()
                .scriptableRenderer.EnqueuePass(m_renderPass);
        }

    }
}
