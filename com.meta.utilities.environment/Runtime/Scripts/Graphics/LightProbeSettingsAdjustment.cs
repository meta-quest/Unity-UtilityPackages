// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Adjusts light probe usage on child renderers
    /// </summary>
    public class LightProbeSettingsAdjustment : MonoBehaviour
    {
        [Tooltip("Optional: Populate this array on awake.  It is recommended to not use this unless neccessary!")]
        [SerializeField] private bool m_autopopulateOnAwake = false;
        [SerializeField] private bool m_useProbesByDefault = true;
        [SerializeField] private Renderer[] m_childRenderers;

        public void Awake()
        {
            if (m_autopopulateOnAwake)
            {
                GetChildRenderers();
            }
            UseProbes(m_useProbesByDefault);
        }
        private void UseProbes(bool useProbes)
        {
            if (m_childRenderers.Length == 0)
            {
                Debug.Log("LightProbeSettingsAdjustment component on " + gameObject + " has no renderers to reference, populate the array in editor.\nGetChildRenderers() can be called at runtime but it is not recommended for performance");
            }
            foreach (var renderer in m_childRenderers)
            {
                if (renderer is not null)
                {
                    //Keeping this purely an on/off option for now since Android URP since LPPV isn't supported at this time
                    renderer.lightProbeUsage = useProbes ? UnityEngine.Rendering.LightProbeUsage.BlendProbes : UnityEngine.Rendering.LightProbeUsage.Off;
                }
            }
        }

        [ContextMenu("Turn on light probe sampling")]
        public void BlendProbesOn()
        {
            UseProbes(true);
        }

        [ContextMenu("Turn off light probe sampling")]
        public void BlendProbesOff()
        {
            UseProbes(false);
        }

        public void GetChildRenderers()
        {
            m_childRenderers = GetComponentsInChildren<Renderer>();
        }
    }
}
