// Copyright (c) Meta Platforms, Inc. and affiliates.
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Manages the initialization and execution of several environment systems and their integration with the render pipeline
    /// </summary>
    [ExecuteAlways]
    public class EnvironmentSystem : Singleton<EnvironmentSystem>
    {
        private static readonly int s_usePlaneClipping = Shader.PropertyToID("_UsePlaneClipping");
        private static readonly int s_clipPlane = Shader.PropertyToID("_ClipPlane");
        private static readonly int s_oceanRcpScale = Shader.PropertyToID("_OceanRcpScale");
        private static readonly int s_oceanChoppyness = Shader.PropertyToID("_OceanChoppyness");
        private static readonly int s_oceanDisplacement = Shader.PropertyToID("_OceanDisplacement");
        private static readonly int s_oceanNormal = Shader.PropertyToID("_OceanNormal");
        private static readonly int s_oceanVisAlbedo = Shader.PropertyToID("_OceanVisAlbedo");
        private static readonly int s_propertyOceanColor = Shader.PropertyToID("_OceanAlbedoColor");
        private static readonly int s_underwaterFogColor = Shader.PropertyToID("_UnderwaterFogColor");
        private static readonly int s_underwaterTint = Shader.PropertyToID("_UnderwaterTint");
        private static readonly int s_underwaterTintDistance = Shader.PropertyToID("_UnderwaterTintDistance");
        private static readonly int s_underwaterExtinction = Shader.PropertyToID("_UnderwaterExtinction");
        private static readonly int s_probeReorientation = Shader.PropertyToID("_ProbeReorientation");
        private static readonly int s_skyboxCameraPosition = Shader.PropertyToID("_SkyboxCameraPosition");

        [SerializeField] private VolumeProfile m_defaultVolumeProfile;
        [SerializeField] private Volume m_volumeA = null;
        [SerializeField] private Volume m_volumeB = null;

        [SerializeField] private OceanSimulation m_oceanSimulation;
        [SerializeField] private QuadtreeRenderer m_oceanQuadtreeRenderer;
        [SerializeField] private SunDiskRenderer m_sunDiskRenderer;

        [SerializeField] private float m_transitionTime = 1.0f;
        [SerializeField] private EnvironmentProfile m_targetProfile;

        [field: SerializeField] public SkyboxUpdater SkyboxUpdater { get; private set; }

        // Ideally these should be in the environment profiles, but making them lerp nicely isn't needed right now
        // Adding here to prove concept and act as MVP and if we need a more complex solution can be ticketed later
        [SerializeField] private WindGustsProfile m_windGustsProfile;

        [SerializeField] private float m_divingClipHeight = 10.0f;

        private MaterialPropertyBlock m_oceanPropertyBlock;
        private Plane m_oceanClippingPlane;
        [NonSerialized]
        private EnvironmentProfile m_sourceProfile, m_previousProfile, m_transitionProfile;
        private float m_lastTransitionStartTime;
        private float? m_oneOffTransitionTime;
        private CommandBuffer m_command;

        public float WindYaw { get; private set; }
        public float WindPitch { get; private set; }
        public EnvironmentProfile CurrentProfile { get; private set; }

        public Vector3 WindVector
        {
            get
            {
                // Convert wind yaw/pitch/speed to a vector using spherical coordinates
                var theta = Mathf.Deg2Rad * WindYaw;
                var phi = Mathf.Deg2Rad * WindPitch;
                var sinPhi = Mathf.Sin(phi);
                return new Vector3(sinPhi * Mathf.Cos(theta), Mathf.Cos(phi), sinPhi * Mathf.Sin(theta)) * CurrentProfile.OceanSettings.WindSpeed;
            }
        }

        public void StartWindGusts(WindGustsProfile profile)
        {
            m_windGustsProfile = profile;
        }
        public void StopWindGusts()
        {
            m_windGustsProfile = null;
        }

        // Changes the environment profile to a new one
        public void SetProfile(EnvironmentProfile profile)
        {
            m_targetProfile = profile;
        }

        public void SetOneOffTransitionTime(float time)
        {
            m_oneOffTransitionTime = time;
        }

        private float3 SampleDisplacement(float3 position) //, float time, Vector4 noiseScroll, float noiseScale, float noiseStrength, Texture2D noiseTexture)
        {
            if (m_oceanSimulation.DisplacementMap == null) return 0;

            var oceanUv = math.frac(position.xz / CurrentProfile.OceanSettings.PatchSize);
            var displacement = m_oceanSimulation.DisplacementMap.GetPixelBilinear(oceanUv.x, oceanUv.y);

            // TODO: disabled for now as it doesn't seem to correctly reproduce the shader when noise strength is > 0 and the texture sample seems very slow
            //var noiseUv = math.frac((new float2(noiseScroll.x, noiseScroll.y) * time + position.xz) / noiseScale);
            //var noiseRaw = noiseTexture.GetPixelBilinear(noiseUv.x, noiseUv.y).r;
            //var noiseValue = math.lerp(1.0f, math.clamp(noiseRaw * 1.5f, 0.0f, 1.0f), noiseStrength);
            var noiseValue = 1;

            return new float3(displacement.r, displacement.g, displacement.b) * noiseValue;
        }

        public float GetOceanHeight(float3 position)
        {
            var oceanUv = math.frac(position.xz / CurrentProfile.OceanSettings.PatchSize);
            var displacement = m_oceanSimulation.DisplacementMap.GetPixelBilinear(oceanUv.x, oceanUv.y);
            var heightUv = displacement / CurrentProfile.OceanSettings.PatchSize;
            return m_oceanSimulation.transform.position.y + m_oceanSimulation.DisplacementMap.GetPixelBilinear(oceanUv.x - heightUv.r, oceanUv.y - heightUv.b).g;
        }

        public float GetOceanHeightIterative(float3 position, int depth = 4)
        {
            // Sample displacement and check new position of current multiple times to calculate more accurate displaced height
            var height = 0.0f;
            for (var i = 0; i < depth; i++)
            {
                var displacement = SampleDisplacement(position); //, time, noiseScroll, noiseScale, noiseStrength, noiseTexture);
                position.xz -= displacement.xz / (i + 1);
                height = displacement.y;
            }
            return height + m_oceanSimulation.transform.position.y;
        }

        public void ToggleOceanPlaneClipping(bool isEnabled)
        {
            // TODO: This should use a shader keyword instead, so we're not paying the high cost of alpha-clipping/disabling early Z when not needed
            // Note: This is unused
            m_oceanPropertyBlock.SetInt(s_usePlaneClipping, isEnabled ? 1 : 0);
        }

        public void SetOceanClipPlane(Plane plane)
        {
            m_oceanClippingPlane = plane;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            m_oceanPropertyBlock = new();

            RenderPipelineManager.beginContextRendering += BeginContextRendering;
            RenderPipelineManager.endContextRendering += EndContextRendering;


#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.update += UpdateEnvironment;
#endif

            m_transitionProfile = ScriptableObject.CreateInstance<EnvironmentProfile>();
            CurrentProfile = m_targetProfile;
            m_oneOffTransitionTime = null;
            m_command = new CommandBuffer();

            Assert.IsNotNull(m_defaultVolumeProfile);
            Assert.IsNotNull(m_volumeA);
            Assert.IsNotNull(m_volumeB);
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.update -= UpdateEnvironment;
#endif

            RenderPipelineManager.beginContextRendering -= BeginContextRendering;
            RenderPipelineManager.endContextRendering -= EndContextRendering;
        }

        private void Update()
        {
            if (Application.isPlaying)
                UpdateEnvironment();
        }

        // All environment update logic should go in here. This wil update CPU state and do any rendering preparations
        private void UpdateEnvironment()
        {
            // Some sanity checks (Mostly required due to editor/playmode switching causing null issues)
            if (m_sourceProfile == null)
                m_sourceProfile = m_targetProfile;

            // Check if profile has changed since we last started a transition
            if (m_targetProfile != m_previousProfile)
            {
                m_lastTransitionStartTime = Time.time;
                m_transitionProfile.StartTransition(m_targetProfile);
                m_previousProfile = m_targetProfile;

                m_volumeB.profile = m_volumeA.profile;
                m_volumeA.profile = m_targetProfile.PostProcessProfile == null ? m_defaultVolumeProfile : m_targetProfile.PostProcessProfile;
            }

            // Update profile
            var theTransitionTime = m_oneOffTransitionTime ?? m_transitionTime;
            var t = theTransitionTime <= 0 ? 1f : Mathf.Clamp01((Time.time - m_lastTransitionStartTime) / theTransitionTime);

            if (t < 1.0f)
            {
                // While transitioning, use the transition profile
                m_transitionProfile.Lerp(m_sourceProfile, m_targetProfile, t);
                CurrentProfile = m_transitionProfile;
            }
            else
            {
                // If transition is complete, use target profile
                m_sourceProfile = m_targetProfile;
                CurrentProfile = m_targetProfile;
                m_oneOffTransitionTime = null;
            }

            WindPitch = CurrentProfile.WindPitch;
            WindYaw = CurrentProfile.WindYaw;
            if (m_windGustsProfile != null)
            {
                // Just passing in Time.time for now, this could be later altered to pull a value from timeline or the like
                WindYaw += m_windGustsProfile.WindYawOffset(Time.time);

                // keeping the wind direction inside a 0 to 360 range (the added offset is always within -180 to +180 range)
                if (WindYaw > 360f)
                {
                    WindYaw -= 360f;
                }
                if (WindYaw < 0f)
                {
                    WindYaw += 360f;
                }
            }

            if (m_oceanSimulation.isActiveAndEnabled)
            {
                m_oceanSimulation.Profile = CurrentProfile;
                m_oceanSimulation.UpdateSimulation(WindVector);
            }

            m_oceanQuadtreeRenderer.Material = CurrentProfile.OceanMaterial;
            SkyboxUpdater.UpdateSkyboxAndLighting(CurrentProfile);

            m_volumeA.weight = t;
            m_volumeB.weight = 1.0f - t;
        }

        // Actual logic related to rendering (Eg setting shader properties, command buffers etc should go here)
        private void BeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            var mainCamera = Camera.main;
            var cameraPosition = mainCamera != null ? mainCamera.transform.position : default;

            // If the ocean is disabled, don't update it, otherwise it will cause errors.
            if (m_oceanSimulation.isActiveAndEnabled)
            {
                m_oceanSimulation.BeginContextRendering();

                m_oceanPropertyBlock.SetFloat(s_oceanRcpScale, 1.0f / CurrentProfile.OceanSettings.PatchSize);
                m_oceanPropertyBlock.SetFloat(s_oceanChoppyness, CurrentProfile.OceanSettings.Choppyness);
                m_oceanPropertyBlock.SetTexture(s_oceanDisplacement, m_oceanSimulation.DisplacementMap);
                m_oceanPropertyBlock.SetTexture(s_oceanNormal, m_oceanSimulation.NormalMap);
                m_oceanPropertyBlock.SetVector(s_clipPlane,
                    Mathf.Abs(cameraPosition.y) < m_divingClipHeight
                    ? new Vector4(m_oceanClippingPlane.normal.x, m_oceanClippingPlane.normal.y, m_oceanClippingPlane.normal.z, m_oceanClippingPlane.distance)
                    : default
                );

                Shader.SetGlobalColor(s_propertyOceanColor, CurrentProfile.OceanMaterial.GetColor(s_oceanVisAlbedo));
            }

            var oceanTexelsPerMeter = m_oceanSimulation.Resolution / CurrentProfile.OceanSettings.PatchSize;
            if (m_oceanQuadtreeRenderer.isActiveAndEnabled)
                m_oceanQuadtreeRenderer.BeginContextRendering(cameras, m_oceanPropertyBlock, oceanTexelsPerMeter);

            // Draw sun disk
            if (m_sunDiskRenderer != null && m_sunDiskRenderer.isActiveAndEnabled)
            {
                foreach (var camera in cameras)
                    m_sunDiskRenderer.BeginContextRendering(CurrentProfile.SunSettings, camera);
            }

            m_command.Clear();
            m_command.SetGlobalVector(s_underwaterFogColor, CurrentProfile.FogSettings.UnderwaterFogColor.linear);
            m_command.SetGlobalVector(s_underwaterTint, CurrentProfile.FogSettings.UnderwaterTint.linear);
            m_command.SetGlobalFloat(s_underwaterTintDistance, CurrentProfile.FogSettings.UnderwaterTintDistance);
            m_command.SetGlobalVector(s_underwaterExtinction,
                -math.log((float4)(Vector4)CurrentProfile.FogSettings.UnderwaterTint.linear) /
                CurrentProfile.FogSettings.UnderwaterTintDistance
            );
            var activeProbeOrientation = ReflectionProbeOrientation.ActiveProbeOrientation;
            m_command.SetGlobalMatrix(s_probeReorientation, activeProbeOrientation != null ? activeProbeOrientation.GetReorientationMatrix() : Matrix4x4.identity);
            // Camera position does not work in Skybox materials in XR, pass it in manually here
            m_command.SetGlobalVector(s_skyboxCameraPosition, cameraPosition);
            m_command.EnableShaderKeyword("_UNDERWATER_FOG");
            context.ExecuteCommandBuffer(m_command);
        }

        private void EndContextRendering(ScriptableRenderContext context, List<Camera> list)
        {
            m_command.Clear();
            m_command.DisableShaderKeyword("_UNDERWATER_FOG");
            context.ExecuteCommandBuffer(m_command);
        }
    }
}
