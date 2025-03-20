// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Syncs up the cabin window material the directional lighting
    /// </summary>
    public class CabinWindowLightLinker : MonoBehaviour
    {

        [Tooltip("Reference to the renderer for the cabin window")]
        [SerializeField] private Renderer m_windowRenderer;
        [Tooltip("The material index for the emissive material on the cabin window renderer")]
        [SerializeField] private int m_windowMaterialIndex = 0;
        [SerializeField] private string m_materialColorName = "_EmissiveColor";
        [SerializeField] private Color m_baseColor = Color.blue;
        [Tooltip("If set, this will read the color of this light to set the emissive color instead off by default, useful if you want the lightning flashes to change color for some reason")]
        [SerializeField] private bool m_useLightColor = false;
        [SerializeField] private Light m_directionalLight;
        [Range(0f, 10f)]
        [Tooltip("Multiply the light value by this when calculating how much to increase the window material emission")]
        [SerializeField] private float m_lightValueMultiplier = 5f;
        [Range(0f, 100f)]
        [Tooltip("Minimum amount that the base color is multiplied by (multiplying color into HDR range will give emission effect")]
        [SerializeField] private float m_minHDRMultiplier = 1f;
        [Range(0f, 100f)]
        [Tooltip("Maximum amount that the base color is multiplied by (multiplying color into HDR range will give emission effect")]
        [SerializeField] private float m_maxHDRMultiplier = 50f;
        private float m_multiplier;


        private void Update()
        {
            m_multiplier = Mathf.Clamp(m_directionalLight.intensity * m_lightValueMultiplier, m_minHDRMultiplier, m_maxHDRMultiplier) * m_lightValueMultiplier;
            if (m_useLightColor)
            {
                var color = new Color(m_directionalLight.color.r * m_multiplier, m_directionalLight.color.g * m_multiplier, m_directionalLight.color.b * m_multiplier);
                m_windowRenderer.materials[m_windowMaterialIndex].SetColor(m_materialColorName, color);
            }
            else
            {
                var color = new Color(m_baseColor.r * m_multiplier, m_baseColor.g * m_multiplier, m_baseColor.b * m_multiplier);
                m_windowRenderer.materials[m_windowMaterialIndex].SetColor(m_materialColorName, color);
            }
        }
    }
}
