// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Stores variables for underwater rendering.
    /// </summary>
    [CreateAssetMenu(fileName = "UnderwaterData", menuName = "Environment System Data/Underwater Data")]
    public class UnderwaterEnvironmentData : ScriptableObject
    {
        [Header("Global Settings")]
        public bool UseUnderwaterFog = true;

        [Header("Base Caustic Properties")]
        public float CausticScale = 1f;
        public float CausticSpeed = 1f;
        public float CausticTimeModulation = 0.001f;
        public float CausticEmissiveIntensity = 0.1f;

        [Header("Caustic Distortion")]
        public float DistortionIntensity = 0.1f;
        public Vector2 DistortionScale = new(1.0f, 1.0f);
        public Vector2 DistortionSpeed = new(0.1f, 0.1f);

        private void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                var controllers = FindObjectsOfType<UnderwaterEnvironmentController>();
                foreach (var controller in controllers)
                {
                    if (controller.Parameters == this)
                    {
                        controller.UpdateCausticParameters();
                    }
                }
            };
#endif
        }
    }
}