// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Contains parameters for controlling the wind effect on shaders such as foliage
    /// </summary>
    [CreateAssetMenu(fileName = "WindData", menuName = "Environment System Data/Wind Data")]
    public class WindData : ScriptableObject
    {
#pragma warning disable IDE1006 // ReSharper disable InconsistentNaming
        [Header("General")]
        [SerializeField] private float _Random_Offset = 0f;
        [SerializeField] private Vector3 _Wind_Direction = Vector3.right;
        [SerializeField] private float _Wind_Intensity = 1f;
        [SerializeField] private float _Lead_Amount = 0f;

        [Header("Primary Wind")]
        [SerializeField] private float _Primary_Wind_Speed = 1f;
        [SerializeField] private float _Primary_Frequency = 1f;
        [SerializeField] private float _Primary_Amplitude = 1f;

        [Header("Secondary Wind")]
        [SerializeField] private float _Secondary_Wind_Speed = 1f;
        [SerializeField] private float _Secondary_Frequency = 1f;
        [SerializeField] private float _Secondary_Amplitude = 1f;

        [Header("Verticle Leaf")]
        [SerializeField] private float _Verticle_Leaf_Speed = 1f;
        [SerializeField] private float _Verticle_Leaf_Frequency = 1f;
        [SerializeField] private float _Verticle_Leaf_Amplitude = 1f;

        [Header("Wind Trunk")]
        [SerializeField] private float _Trunk_Wind_Speed = 1f;
        [SerializeField] private float _Trunk_Frequency = 1f;
        [SerializeField] private float _Trunk_Amplitude = 1f;
        [SerializeField] private float _Trunk_Wind_Intensity = 1f;
#pragma warning restore IDE1006 // ReSharper restore InconsistentNaming

        // Properties to access the private fields
        public float RandomOffset => _Random_Offset;
        public Vector3 WindDirection => _Wind_Direction;
        public float WindIntensity => _Wind_Intensity;
        public float LeadAmount => _Lead_Amount;

        public float PrimaryWindSpeed => _Primary_Wind_Speed;
        public float PrimaryFrequency => _Primary_Frequency;
        public float PrimaryAmplitude => _Primary_Amplitude;

        public float SecondaryWindSpeed => _Secondary_Wind_Speed;
        public float SecondaryFrequency => _Secondary_Frequency;
        public float SecondaryAmplitude => _Secondary_Amplitude;

        public float VerticleLeafSpeed => _Verticle_Leaf_Speed;
        public float VerticleLeafFrequency => _Verticle_Leaf_Frequency;
        public float VerticleLeafAmplitude => _Verticle_Leaf_Amplitude;

        public float TrunkWindSpeed => _Trunk_Wind_Speed;
        public float TrunkFrequency => _Trunk_Frequency;
        public float TrunkAmplitude => _Trunk_Amplitude;
        public float TrunkWindIntensity => _Trunk_Wind_Intensity;
    }
}