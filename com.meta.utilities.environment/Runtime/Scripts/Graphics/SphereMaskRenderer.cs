// Copyright (c) Meta Platforms, Inc. and affiliates.
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Custom sphere masking effect rendering for when the player is outside of bounds
    /// </summary>
    [ExecuteAlways]
    public class SphereMaskRenderer : MonoBehaviour
    {
        private static readonly int s_propSpherePosition = Shader.PropertyToID("_SpherePosition");
        private static readonly int s_propIntensity = Shader.PropertyToID("_Intensity");
        private static readonly int s_propDistanceMinMax = Shader.PropertyToID("_DistanceMinMax");

        [SerializeField]
        private Material m_screenMaterial;

        [SerializeField, Range(0f, 1f)]
        private float m_intensity = 1f;

        [SerializeField]
        private Vector2 m_maskingMinMax = new(2f, 4f);

        [SerializeField]
        private Vector2 m_fogMinMax = new(4f, 20f);

        public float Intensity
        {
            get => s_enabled ? m_intensity : 0;
            set => m_intensity = value;
        }

        private static bool s_enabled = true;
        public static ref bool Enabled => ref s_enabled;

        public class SphereMaskRenderPass : ScriptableRenderPass
        {
            private SphereMaskRenderer m_renderer;

            public SphereMaskRenderPass(SphereMaskRenderer renderer)
            {
                m_renderer = renderer;
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                ref var cameraData = ref renderingData.cameraData;
                if (cameraData.cameraType is not CameraType.Game and not CameraType.SceneView) return;

                var cmd = CommandBufferPool.Get("SphereMask");
                cmd.SetGlobalVector(s_propSpherePosition, m_renderer.transform.position);
                cmd.SetGlobalFloat(s_propIntensity, m_renderer.Intensity);
                cmd.SetGlobalVector(s_propDistanceMinMax, new Vector4(
                    m_renderer.m_maskingMinMax.x, m_renderer.m_maskingMinMax.y,
                    m_renderer.m_fogMinMax.x, m_renderer.m_fogMinMax.y
                ));
                CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle);
                CoreUtils.DrawFullScreen(cmd, m_renderer.m_screenMaterial);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

        }


        private SphereMaskRenderPass m_renderPass;

        private bool m_wasActive;

        private static bool s_initialDynamicResEnabled;
        private static bool s_initialSkyboxUpdaterEnabled;
        private static int s_sphereMaskActiveCount = 0;

        protected void OnEnable()
        {
            m_renderPass = new(this);
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }
        protected void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        }

        private void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            var isActive = m_screenMaterial != null && Intensity > 0f;
            if (Application.isPlaying && m_wasActive != isActive)
            {
                if (m_wasActive)
                {
                    // Only reenable dynamic res if all instances are disabled
                    if (--s_sphereMaskActiveCount == 0)
                    {
                        NotifySphereMaskDisabling();
                    }
                }
                m_wasActive = isActive;
                if (isActive)
                {
                    // Disable dynamic res as long as any SphereMaskRenderers are enabled
                    if (s_sphereMaskActiveCount++ == 0)
                    {
                        NotifySphereMaskEnabling();
                    }
                }
            }
            if (isActive)
            {
                cam.GetUniversalAdditionalCameraData()
                    .scriptableRenderer.EnqueuePass(m_renderPass);
            }
        }

        // Depth copy is not compatible with dynamic res or lightprobe updates,
        // both cause backbuffer texture resizes. We need to disable them
        // whenever SphereMaskRenderer gets enabled.
        private void NotifySphereMaskEnabling()
        {
            s_initialDynamicResEnabled = OVRManager.instance.enableDynamicResolution;
            if (s_initialDynamicResEnabled)
            {
                OVRManager.instance.enableDynamicResolution = false;
                // Depth sampling does not support dynamic res, we need to force native render scale
                XRSettings.renderViewportScale = 1f;
                ScalableBufferManager.ResizeBuffers(1f, 1f);
            }
            s_initialSkyboxUpdaterEnabled = EnvironmentSystem.Instance.SkyboxUpdater.EnableProbeUpdates;
            EnvironmentSystem.Instance.SkyboxUpdater.EnableProbeUpdates = false;
        }

        private void NotifySphereMaskDisabling()
        {
            OVRManager.instance.enableDynamicResolution = s_initialDynamicResEnabled;
            EnvironmentSystem.Instance.SkyboxUpdater.EnableProbeUpdates = s_initialSkyboxUpdaterEnabled;
        }

    }
}
