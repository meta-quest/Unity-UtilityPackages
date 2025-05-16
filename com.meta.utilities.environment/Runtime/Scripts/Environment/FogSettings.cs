// Copyright (c) Meta Platforms, Inc. and affiliates.
using System;
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Stores settings related to fog in the environment profile.
    /// </summary>
    [Serializable]
    public class FogSettings
    {
        [field: SerializeField, Tooltip("The Color of Fog in the lighting panel")] public Color FogColor { get; private set; }
        [field: SerializeField, Min(0.0f), Tooltip("Exponential density of the fog")] public float Density = 0.01f;

        [field: SerializeField, Tooltip("The Color of Fog in the lighting panel")] public Color UnderwaterFogColor { get; private set; } = new Color(0.1f, 0.2f, 0.35f, 1.0f);
        [field: SerializeField, Tooltip("Tint of underwater objects")] public Color UnderwaterTint { get; private set; } = new Color(0.1f, 0.6f, 0.4f, 1.0f);
        [field: SerializeField, Min(0.0f), Tooltip("Distance at which an object will be fully tinted by the above color")] public float UnderwaterTintDistance = 64;

        public void Lerp(FogSettings fogSettings1, FogSettings fogSettings2, float t)
        {
            FogColor = Color.Lerp(fogSettings1.FogColor, fogSettings2.FogColor, t);
            Density = Mathf.Lerp(fogSettings1.Density, fogSettings2.Density, t);
            UnderwaterFogColor = Color.Lerp(fogSettings1.UnderwaterFogColor, fogSettings2.UnderwaterFogColor, t);
            UnderwaterTint = Color.Lerp(fogSettings1.UnderwaterTint, fogSettings2.UnderwaterTint, t);
            UnderwaterTintDistance = Mathf.Lerp(fogSettings1.UnderwaterTintDistance, fogSettings2.UnderwaterTintDistance, t);
        }
    }
}
