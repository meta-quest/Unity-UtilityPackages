// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEngine;
using UnityEngine.Rendering;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Top level object for storing all data related to environment state such as post processing profile, ocean settings, sun settings, skybox etc.
    /// </summary>
    [CreateAssetMenu(menuName = "Data/Environment Profile")]
    public class EnvironmentProfile : ScriptableObject
    {
        [field: SerializeField] public VolumeProfile PostProcessProfile { get; private set; } = null;

        [field: SerializeField]
        public OceanSettings OceanSettings { get; private set; } = new();

        [field: SerializeField, Tooltip("The material used for rendering")]
        public Material OceanMaterial { get; private set; } = null;

        [field: SerializeField]
        public Material SkyboxMaterial { get; private set; } = null;

        [field: SerializeField]
        public SunSettings SunSettings { get; private set; } = new();

        [field: SerializeField]
        public FogSettings FogSettings { get; private set; } = new();

        [field: SerializeField, Range(0.0f, 360.0f), Tooltip("Yaw angle of wind vector")] public float WindYaw { get; private set; } = 0.0f;
        [field: SerializeField, Range(0.0f, 360.0f), Tooltip("Pitch angle of wind vector")] public float WindPitch { get; private set; } = 0.0f;

        [field: SerializeField, Tooltip("Only used when Environment Lighting is set to Gradient mode")]
        public GradientAmbientSettings GradientAmbientSettings { get; private set; } = new();

        [field: SerializeField]
        public float[] EnvironmentData { get; set; }

        [field: SerializeField]
        public SphericalHarmonicsL2 AmbientProbe { get; set; }

        // Used to track changes in inspector
        public int Version { get; set; }

        // Called at the start of a transition to perform any potentailly expensive steps or initialization that we don't want to do every frame
        public void StartTransition(EnvironmentProfile targetProfile)
        {
            if (OceanMaterial == null)
                OceanMaterial = new Material(targetProfile.OceanMaterial);

            if (SkyboxMaterial == null)
                SkyboxMaterial = new Material(targetProfile.SkyboxMaterial);

            // Keywords are not handled by material.lerp, so force any keywords immediately to the new value
            OceanMaterial.shaderKeywords = targetProfile.OceanMaterial.shaderKeywords;
            SkyboxMaterial.shaderKeywords = targetProfile.SkyboxMaterial.shaderKeywords;

            // Textures are not handled by material.lerp, so force any textures immediately to the new value
            var shader = targetProfile.SkyboxMaterial.shader;
            var count = shader.GetPropertyCount();
            for (var i = 0; i < count; i++)
            {
                var propertyType = shader.GetPropertyType(i);
                if (propertyType != ShaderPropertyType.Texture)
                    continue;

                var propertyId = shader.GetPropertyNameId(i);
                var texture = targetProfile.SkyboxMaterial.GetTexture(propertyId);

                if (SkyboxMaterial.HasTexture(propertyId))
                    SkyboxMaterial.SetTexture(propertyId, texture);
            }

            SunSettings.StartTransition(targetProfile);
        }

        // Calls into all the child objects which handle their respective transitions/lerps
        public void Lerp(EnvironmentProfile previousProfile, EnvironmentProfile targetProfile, float t)
        {
            OceanSettings.Lerp(previousProfile.OceanSettings, targetProfile.OceanSettings, t);
            OceanMaterial.Lerp(previousProfile.OceanMaterial, targetProfile.OceanMaterial, t);
            SkyboxMaterial.Lerp(previousProfile.SkyboxMaterial, targetProfile.SkyboxMaterial, t);
            SunSettings.Lerp(previousProfile.SunSettings, targetProfile.SunSettings, t);
            FogSettings.Lerp(previousProfile.FogSettings, targetProfile.FogSettings, t);
            WindPitch = Mathf.Lerp(previousProfile.WindPitch, targetProfile.WindPitch, t);
            WindYaw = Mathf.Lerp(previousProfile.WindYaw, targetProfile.WindYaw, t);
            GradientAmbientSettings.Lerp(previousProfile.GradientAmbientSettings, targetProfile.GradientAmbientSettings, t);

            if (previousProfile.EnvironmentData.Length == targetProfile.EnvironmentData.Length)
            {
                if (EnvironmentData == null || EnvironmentData.Length != previousProfile.EnvironmentData.Length)
                {
                    EnvironmentData = new float[previousProfile.EnvironmentData.Length];
                }

                for (var i = 0; i < EnvironmentData.Length; i++)
                {
                    EnvironmentData[i] = Mathf.Lerp(previousProfile.EnvironmentData[i], targetProfile.EnvironmentData[i], t);
                }
            }

            AmbientProbe = previousProfile.AmbientProbe * (1 - t) + targetProfile.AmbientProbe * t;

        }
    }
}