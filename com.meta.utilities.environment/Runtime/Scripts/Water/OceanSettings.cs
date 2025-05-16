// Copyright (c) Meta Platforms, Inc. and affiliates.
using System;
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Stores all settings related to the ocean state
    /// </summary>
    [Serializable]
    public class OceanSettings
    {
        [field: SerializeField, Range(0.0f, 64), Tooltip("Speed of the wind in meters/sec. Affects wave size and speed")]
        public float WindSpeed { get; private set; } = 8.0f;

        [field: SerializeField, Range(0, 1), Tooltip("How aligned the waves are to the wind")]
        public float Directionality { get; private set; } = 0.875f;

        [field: SerializeField, Range(0.0f, 1.0f), Tooltip("Choppyness of waves")]
        public float Choppyness { get; private set; } = 1.0f;

        [field: SerializeField, Tooltip("World Space size of the FFT patch. Lower values mean more detail, but more repetition")]
        public float PatchSize { get; private set; } = 64;

        [field: SerializeField, Range(0.0f, 1.0f), Tooltip("Fades out waves smaller than this")]
        public float MinWaveSize { get; private set; } = 0.001f;

        [field: Header("Advanced")]
        [field: SerializeField, Min(0), Tooltip("Gravity in m/s, affects wave size to speed ratio")]
        public float Gravity { get; private set; } = 9.81f;

        [field: SerializeField, Tooltip("Controls how long before the simulation loops over itself")]
        public float SequenceLength { get; private set; } = 200.0f;

        [field: SerializeField, Tooltip("Speed control for simulation, should be left at 1 except for debug purposes")]
        public float TimeScale { get; private set; } = 1.0f;

        public void Lerp(OceanSettings a, OceanSettings b, float t)
        {
            WindSpeed = Mathf.Lerp(a.WindSpeed, b.WindSpeed, t);
            Directionality = Mathf.Lerp(a.Directionality, b.Directionality, t);
            Choppyness = Mathf.Lerp(a.Choppyness, b.Choppyness, t);
            PatchSize = Mathf.Lerp(a.PatchSize, b.PatchSize, t);
            MinWaveSize = Mathf.Lerp(a.MinWaveSize, b.MinWaveSize, t);
            Gravity = Mathf.Lerp(a.Gravity, b.Gravity, t);
            SequenceLength = Mathf.Lerp(a.SequenceLength, b.SequenceLength, t);
            TimeScale = Mathf.Lerp(a.TimeScale, b.TimeScale, t);
        }
    }
}