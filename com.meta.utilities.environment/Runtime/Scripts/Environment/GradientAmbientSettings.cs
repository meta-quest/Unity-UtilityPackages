// Copyright (c) Meta Platforms, Inc. and affiliates.
using System;
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Settings related to gradient lighting for an environment
    /// </summary>
    [Serializable]
    public class GradientAmbientSettings
    {
        [field: SerializeField, Tooltip("Sky Color when using Gradient Environment Lighting"), ColorUsage(true, true)] public Color SkyColor { get; set; }
        [field: SerializeField, Tooltip("Equator Color when using Gradient Environment Lighting"), ColorUsage(true, true)] public Color EquatorColor { get; set; }
        [field: SerializeField, Tooltip("Ground Color when using Gradient Environment Lighting"), ColorUsage(true, true)] public Color GroundColor { get; set; }

        public void Lerp(GradientAmbientSettings gradientAmbientSettings1, GradientAmbientSettings gradientAmbientSettings2, float t)
        {
            SkyColor = Color.Lerp(gradientAmbientSettings1.SkyColor, gradientAmbientSettings2.SkyColor, t);
            EquatorColor = Color.Lerp(gradientAmbientSettings1.EquatorColor, gradientAmbientSettings1.EquatorColor, t);
            GroundColor = Color.Lerp(gradientAmbientSettings1.GroundColor, gradientAmbientSettings2.GroundColor, t);
        }

    }
}
