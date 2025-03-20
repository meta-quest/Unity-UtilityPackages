// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Stores data for wind gusts
    /// </summary>
    [CreateAssetMenu(menuName = "Data/Wind Gusts Profile")]
    public class WindGustsProfile : ScriptableObject
    {
        [field: SerializeField, Range(0.0f, 180.0f), Tooltip("Max Wind Deflection")] public float MaxWindDeflection { get; private set; } = 180.0f;
        [field: SerializeField, Range(0.0f, 60.0f), Tooltip("Gust Cycle Time")] public float GustCycleTime { get; private set; } = 20.0f;

        [field: SerializeField]
        public AnimationCurve WindYawOffsetCurve { get; private set; } = new();

        public float WindYawOffset(float gameTime)
        {
            return WindYawOffsetCurve.Evaluate(gameTime / GustCycleTime - Mathf.Floor(gameTime / GustCycleTime)) * MaxWindDeflection;
        }
    }
}
