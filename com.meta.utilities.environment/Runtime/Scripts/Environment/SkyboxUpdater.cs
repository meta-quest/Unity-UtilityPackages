// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEngine;
using UnityEngine.Rendering;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Controls updating the skybox and related environment variables such as the sun
    /// </summary>
    public class SkyboxUpdater : MonoBehaviour
    {
        [SerializeField]
        private bool m_isEnabled = true;

        [SerializeField]
        private bool m_useGradientLighting = false;

        [SerializeField]
        private bool m_useEnvironmentDataLighting = false;

        [SerializeField]
        private float m_environmentReflectionsUpdateInterval = 5;

        [SerializeField]
        private ReflectionProbe m_reflectionProbe;

        private bool m_checkForTimeToResetToDefaultInterval = false;

        private float m_timeToResetToDefaultInterval;
        private float m_lastUpdateTime;
        private float m_defaultEnvironmentReflectionsUpdateInterval;
        private bool m_noTimeSlicingForNextUpdate = false;

        private Vector3 m_originalPosition;

        public bool EnableProbeUpdates { get; set; } = true;

        private void Awake()
        {
            m_defaultEnvironmentReflectionsUpdateInterval = m_environmentReflectionsUpdateInterval;
            if (m_reflectionProbe)
            {
                m_originalPosition = m_reflectionProbe.transform.position;
            }
        }

        public void MoveProbePosition(Transform newPos)
        {
            if (m_reflectionProbe)
            {
                //Only using the x and z components of the input position, since we would always want water reflections to be rendered from the water surface height
                m_reflectionProbe.transform.position = new Vector3(newPos.position.x, m_originalPosition.y, newPos.position.z);
            }
        }

        public void ResetProbePosition()
        {
            if (m_reflectionProbe)
            {
                m_reflectionProbe.transform.position = m_originalPosition;
            }
        }

        public void UpdateSkyboxAndLighting(EnvironmentProfile profile)
        {
            // Update Gradient Lighting if being used
            // Check if we need to change ambient lighting condition
            if (m_useEnvironmentDataLighting && profile.EnvironmentData.Length > 0)
            {
                if (RenderSettings.ambientMode != AmbientMode.Skybox)
                {
                    RenderSettings.ambientMode = AmbientMode.Skybox;
                }

                //DynamicGI.SetEnvironmentData(profile.EnvironmentData);
                RenderSettings.ambientProbe = profile.AmbientProbe;
            }
            else if (m_useGradientLighting)
            {
                if (RenderSettings.ambientMode != AmbientMode.Trilight)
                {
                    RenderSettings.ambientMode = AmbientMode.Trilight;
                }
                RenderSettings.ambientSkyColor = profile.GradientAmbientSettings.SkyColor;
                RenderSettings.ambientEquatorColor = profile.GradientAmbientSettings.EquatorColor;
                RenderSettings.ambientGroundColor = profile.GradientAmbientSettings.GroundColor;
            }
            else if (!m_useGradientLighting && RenderSettings.ambientMode == AmbientMode.Trilight)
            {
                RenderSettings.ambientMode = AmbientMode.Skybox;
            }

            // Update Sun
            var light = RenderSettings.sun;
            if (light)
            {
                light.intensity = profile.SunSettings.Intensity;

                // Update Fog
                // note: despite UI saying filter in the api it's still refered to as Color. 
                light.color = profile.SunSettings.Filter;
                light.transform.rotation = Quaternion.Euler(profile.SunSettings.Rotation);
            }

            // Change fog color and other render settings
            RenderSettings.fogColor = profile.FogSettings.FogColor;
            RenderSettings.fogDensity = profile.FogSettings.Density;

            // Update skybox (Do this last, incase sun/fog affect result)
            RenderSettings.skybox = profile.SkyboxMaterial;

            if (!m_isEnabled)
                return;

            // Need to do this constantly since reflectionProbe.texture can be null if it is not created
            if (Application.isPlaying)
            {
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = m_reflectionProbe.texture;
            }

            if (Time.time > m_lastUpdateTime + m_environmentReflectionsUpdateInterval)
            {
                // Render reflection probe in play mode, since it does not auto-update..
                if (Application.isPlaying && EnableProbeUpdates)
                {
                    // This check probably isn't required since there seems to be neglibile cost to DynamicGI.UpdateEnvironment anyway when using gradient lighting, but just to be safe
                    if (!m_useGradientLighting && !m_useEnvironmentDataLighting)
                    {
                        DynamicGI.UpdateEnvironment();
                    }
                    if (m_noTimeSlicingForNextUpdate)
                    {
                        m_reflectionProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                        m_noTimeSlicingForNextUpdate = false;
                    }
                    else
                    {
                        m_reflectionProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
                    }
                    _ = m_reflectionProbe.RenderProbe();
                }

                m_lastUpdateTime = Time.time;
            }

            if (m_checkForTimeToResetToDefaultInterval && Time.time > m_timeToResetToDefaultInterval)
            {
                ResetReflectionsUpdateIntervalToDefault();
                m_checkForTimeToResetToDefaultInterval = false;
            }
        }

        public void EnableReflectionUpdates()
        {
            m_isEnabled = true;
        }

        public void DisableReflectionUpdates()
        {
            m_isEnabled = false;
        }

        public void UseGradientEnvironmentLighting()
        {
            m_useGradientLighting = true;
            RenderSettings.ambientMode = AmbientMode.Trilight;
        }

        public void UseSkyboxEnvironmentLighting()
        {
            m_useGradientLighting = false;
            RenderSettings.ambientMode = AmbientMode.Skybox;
        }

        public void SetReflectionsUpdateInterval(float newInterval)
        {
            m_environmentReflectionsUpdateInterval = newInterval;
        }

        public void ResetReflectionsUpdateIntervalToDefault()
        {
            m_environmentReflectionsUpdateInterval = m_defaultEnvironmentReflectionsUpdateInterval;
        }

        [ContextMenu("Force reflection update in next frame")]
        public void ForceReflectionUpdateNextFrame()
        {
            m_lastUpdateTime = Time.time - m_environmentReflectionsUpdateInterval;
            m_noTimeSlicingForNextUpdate = true;
        }

        public void ResetReflectionsUpdateIntervalToDefaultAfterDelay(float delay)
        {
            m_timeToResetToDefaultInterval = Time.time + delay;
            m_checkForTimeToResetToDefaultInterval = true;
        }
    }
}
