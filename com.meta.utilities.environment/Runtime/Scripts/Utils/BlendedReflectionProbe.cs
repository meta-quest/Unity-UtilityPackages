// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Blends a reflection probe between two different cubemap textures in realtime
    /// </summary>
    [RequireComponent(typeof(ReflectionProbe)), ExecuteAlways]
    public class BlendedReflectionProbe : MonoBehaviour
    {
        [SerializeField] private bool m_doBlending = false;
        private bool m_blendingAlreadySetup = false;
        [SerializeField] private Texture m_cubemapSrc;
        [SerializeField] private Texture m_cubemapDst;
        [SerializeField, Range(0, 1)] private float m_blend;
        [SerializeField, AutoSet(typeof(ReflectionProbe))] private ReflectionProbe m_probe;


        private RenderTexture m_probeRenderTexture;
        private bool m_probeDirty;

        private float m_blendStartTime;
        private float m_blendDuration;
        private bool m_blendTransitionHappening;

        public Texture CubemapSrc
        {
            get => m_cubemapSrc;
            set
            {
                m_cubemapSrc = value;
                m_probeDirty = true;
            }
        }

        public Texture CubemapDst
        {
            get => m_cubemapDst;
            set
            {
                m_cubemapDst = value;
                m_probeDirty = true;
            }
        }

        public float Blend
        {
            get => m_blend;
            set
            {
                if (m_blend != value)
                {
                    m_blend = value;
                    m_probeDirty = true;
                }
            }
        }

        private void Start()
        {
            m_probeRenderTexture = new RenderTexture(m_probe.resolution, m_probe.resolution, 0)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Cube,
                format = RenderTextureFormat.ARGBHalf
            };
            _ = m_probeRenderTexture.Create();
            if (m_doBlending)
            {
                SetupBlending();
            }
        }

        private void Update()
        {
            if (!m_cubemapDst || !m_cubemapSrc || !m_doBlending)
            {
                return;
            }
            if (m_blendTransitionHappening)
            {
                m_blend = Mathf.Lerp(0f, 1f, (Time.time - m_blendStartTime) / m_blendDuration);
                if (m_blend >= 1f)
                {
                    m_blend = 1f;
                    m_blendTransitionHappening = false;
                }
                m_probeDirty = true;
            }
            if (m_doBlending && !m_blendingAlreadySetup)
            {
                SetupBlending();
            }
            if (m_probeDirty)
            {
                UpdateProbe();
                m_probeDirty = false;
            }
        }

        [ContextMenu("Enable cubemap blending override for this reflection probe")]
        public void EnableBlending()
        {
            m_doBlending = true;
            SetupBlending();
        }

        private void SetupBlending()
        {
            if (m_blendingAlreadySetup)
            {
                return;
            }
            m_probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
            m_probe.customBakedTexture = m_probeRenderTexture;
            m_blendingAlreadySetup = true;
            //STRETCH: Add options to stop blending and revert this reflection probe back to baked or realtime states
            //Not needed for the basic storm setup but would be nice to have for using this blending functionality more widely
            UpdateProbe();
        }

        public void StartBlendOverTime(float blendTime)
        {
            m_blendStartTime = Time.time;
            m_blendDuration = blendTime;
            m_blendTransitionHappening = true;
        }


        private void OnValidate()
        {
            m_probeDirty = true;
        }

        private void UpdateProbe()
        {
            if (m_cubemapSrc is not null && m_cubemapDst is not null)
            {
                _ = ReflectionProbe.BlendCubemap(m_cubemapSrc, m_cubemapDst, m_blend, m_probeRenderTexture);
            }
        }
    }
}
