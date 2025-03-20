// Copyright (c) Meta Platforms, Inc. and affiliates.
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Meta.Utilities.MathUtils;
using static Unity.Mathematics.math;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Performs the final pass over the FFT data to compute the final displacement values
    /// </summary>
    [BurstCompile(FloatPrecision.Low, FloatMode.Default, CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
    public struct OceanFFTFinalJob : IJobParallelFor
    {
        private int m_resolution;
        private int m_passIndex;
        private float m_choppyness;

        [ReadOnly]
        private NativeArray<(int2, float2)> m_butterflyLookupTable;

        [ReadOnly]
        private NativeArray<float2> m_heightSource;

        [ReadOnly]
        private NativeArray<float4> m_displacementSource;

        [WriteOnly]
        private NativeArray<float3> m_displacementResult;

        [WriteOnly]
        private NativeArray<half4> m_displacementPixels;

        public OceanFFTFinalJob(int resolution, int passIndex, float choppyness, NativeArray<(int2, float2)> butterflyLookupTable, NativeArray<float2> heightSource, NativeArray<float4> displacementSource, NativeArray<float3> displacementResult, NativeArray<half4> displacementPixels)
        {
            m_resolution = resolution;
            m_passIndex = passIndex;
            m_choppyness = choppyness;
            m_butterflyLookupTable = butterflyLookupTable;
            m_heightSource = heightSource;
            m_displacementSource = displacementSource;
            m_displacementResult = displacementResult;
            m_displacementPixels = displacementPixels;
        }

        void IJobParallelFor.Execute(int index)
        {
            var x = index % m_resolution;
            var y = index / m_resolution;

            var bftIdx = y + m_passIndex * m_resolution;
            var (indices, weights) = m_butterflyLookupTable[bftIdx];

            var sign = ((x + y) & 1) == 0 ? 1 : -1;

            var heightResult = cadd(m_heightSource[x + indices.x * m_resolution], cmul(m_heightSource[x + indices.y * m_resolution], weights));

            var displacementA = m_displacementSource[x + indices.x * m_resolution];
            var displacementB = m_displacementSource[x + indices.y * m_resolution];
            var dispResult = float4(cadd(displacementA.xy, cmul(displacementB.xy, weights)), cadd(displacementA.zw, cmul(displacementB.zw, weights)));
            var displacement = float3(-dispResult.xz * m_choppyness, heightResult.x).xzy * sign;

            m_displacementResult[index] = displacement;
            m_displacementPixels[index] = half4(float4(displacement, 0.0f));
        }
    }
}