// Copyright (c) Meta Platforms, Inc. and affiliates.
using System.Collections;
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Manages reflection probes on the ship
    /// </summary>
    public class ShipReflectionProbesController : MonoBehaviour
    {
        [SerializeField] private ReflectionProbe[] m_shipReflectionProbes;
        [SerializeField] private bool m_updateProbesOnAwake = true;

        [SerializeField] private ReflectionProbeOrientation m_reflectionProbeOrientation;

        private void Awake()
        {
            if (m_updateProbesOnAwake)
                RenderAllProbesAllFacesAtOnce();
        }

        [ContextMenu("Render All Probes (All Faces At Once)")]
        public void RenderAllProbesAllFacesAtOnce()
        {
            foreach (var rp in m_shipReflectionProbes)
            {
                if (rp.enabled)
                {
                    //Generally should avoid using no time slicing on probes to avoid hitches, but since this should only happen on teleport should be fine
                    //If this is causing noticeable hitches then revert to the commented out version
                    //rp.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
                    rp.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                    _ = rp.RenderProbe();
                }
            }
            if (m_reflectionProbeOrientation != null)
                m_reflectionProbeOrientation.ResetBakeOrientation();
        }

        [ContextMenu("Render All Probes (Individual Faces)")]
        public void RenderAllProbesTimeSliced()
        {
            foreach (var rp in m_shipReflectionProbes)
            {
                if (rp.enabled)
                {
                    rp.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
                    _ = rp.RenderProbe();
                }
            }
            if (m_reflectionProbeOrientation != null)
                m_reflectionProbeOrientation.ResetBakeOrientation();
        }

        public void RenderAllProbesAllFacesAtOnceNextFrame()
        {
            _ = StartCoroutine(nameof(WaitFrameBeforeRendering));
        }

        private IEnumerator WaitFrameBeforeRendering()
        {
            yield return new WaitForEndOfFrame();
            RenderAllProbesAllFacesAtOnce();
        }

    }
}
