// Copyright (c) Meta Platforms, Inc. and affiliates.
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Contains settings for the sun color, rotation, disk rendering, and secondary (moon) disk rendering
    /// </summary>
    [Serializable]
    public class SunSettings
    {
        [field: SerializeField, Tooltip("intensity of the directional light")]
        public float Intensity { get; private set; } = 2;

        [field: SerializeField, Tooltip("The color tint of the directional light")]
        public Color Filter { get; private set; } = Color.white;

        [field: SerializeField, Tooltip("The rotation of the directiona light")]
        public Vector3 Rotation { get; private set; } = new(50f, -30f, 0f);

        [field: SerializeField, Tooltip("Material used to render sun/moon disk")]
        public Material SunDiskMaterial { get; private set; }

        [field: SerializeField, Tooltip("Size of Sun Disk in degrees (Sun in real life is ~0.53)")]
        public float AngularDiameter { get; set; } = 5.3f;

        [field: SerializeField, Tooltip("Whether to render a secondary Celestial Object (Eg moon during daytime), note this won't cast light in the scene")]
        public bool RenderSecondaryCelestialObject = false;

        [field: SerializeField, Tooltip("Rotation of Secondary Celestial Object")]
        public Vector3 SecondaryCelestialObjectRotation { get; private set; } = new(70f, 30f, 0f);

        [field: SerializeField, Tooltip("Material used to render secondary celestial object disk")]
        public Material SecondaryCelestialObjectMaterial { get; private set; }

        [field: SerializeField, Tooltip("Size of Secondary Celestial Object Disk in degrees (Sun in real life is ~0.53)")]
        public float SecondaryCelestialObjectAngularDiameter { get; private set; } = 5.3f;

        // Called when a transition starts. Used to update any texture references
        private void InitializeTransition(Material source, Material dest)
        {
            // Hanlde cases where previous or current sun or moon might not exist
            if (source == null || dest == null)
                return;

            var count = source.shader.GetPropertyCount();
            for (var i = 0; i < count; i++)
            {
                var propertyType = source.shader.GetPropertyType(i);
                if (propertyType != ShaderPropertyType.Texture)
                    continue;

                var propertyId = source.shader.GetPropertyNameId(i);
                var texture = source.GetTexture(propertyId);

                if (dest.HasTexture(propertyId))
                    dest.SetTexture(propertyId, texture);
            }
        }

        public void StartTransition(EnvironmentProfile targetProfile)
        {
            if (targetProfile.SunSettings.SunDiskMaterial != null && SunDiskMaterial == null)
                SunDiskMaterial = new Material(targetProfile.SunSettings.SunDiskMaterial);

            InitializeTransition(targetProfile.SunSettings.SunDiskMaterial, SunDiskMaterial);

            if (targetProfile.SunSettings.SecondaryCelestialObjectMaterial != null && SecondaryCelestialObjectMaterial == null)
                SecondaryCelestialObjectMaterial = new Material(targetProfile.SunSettings.SecondaryCelestialObjectMaterial);

            InitializeTransition(targetProfile.SunSettings.SecondaryCelestialObjectMaterial, SecondaryCelestialObjectMaterial);
        }

        public void Lerp(SunSettings a, SunSettings b, float t)
        {
            Intensity = Mathf.Lerp(a.Intensity, b.Intensity, t);
            Filter = Color.Lerp(a.Filter, b.Filter, t);
            Rotation = Quaternion.Lerp(Quaternion.Euler(a.Rotation), Quaternion.Euler(b.Rotation), t).eulerAngles;
            AngularDiameter = Mathf.Lerp(a.AngularDiameter, b.AngularDiameter, t);

            if (SunDiskMaterial != null && a.SunDiskMaterial != null && b.SunDiskMaterial != null)
                SunDiskMaterial.Lerp(a.SunDiskMaterial, b.SunDiskMaterial, t);

            // Secondary celestial object
            RenderSecondaryCelestialObject = t < 0.5f ? a.RenderSecondaryCelestialObject : b.RenderSecondaryCelestialObject;
            SecondaryCelestialObjectRotation = Quaternion.Lerp(Quaternion.Euler(a.SecondaryCelestialObjectRotation), Quaternion.Euler(b.SecondaryCelestialObjectRotation), t).eulerAngles;

            if (SecondaryCelestialObjectMaterial != null && a.SecondaryCelestialObjectMaterial != null && b.SecondaryCelestialObjectMaterial != null)
                SecondaryCelestialObjectMaterial.Lerp(a.SecondaryCelestialObjectMaterial, b.SecondaryCelestialObjectMaterial, t);

            SecondaryCelestialObjectAngularDiameter = Mathf.Lerp(a.SecondaryCelestialObjectAngularDiameter, b.SecondaryCelestialObjectAngularDiameter, t);
        }
    }
}
