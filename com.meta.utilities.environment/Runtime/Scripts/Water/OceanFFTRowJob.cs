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
    /// Processes the rows of the FFT
    /// </summary>
    [BurstCompile(FloatPrecision.Low, FloatMode.Default, CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
    public struct OceanFFTRowJob : IJobParallelFor
    {
        private int m_resolution;
        private int m_passCount;

        [ReadOnly]
        private NativeArray<(int2, float2)> m_butterflyLookupTable;

        [NativeDisableParallelForRestriction]
        private NativeArray<float2> m_heightBufferA;

        [NativeDisableParallelForRestriction]
        private NativeArray<float4> m_displacementBufferA;

        [NativeDisableParallelForRestriction]
        private NativeArray<float2> m_heightBufferB;

        [NativeDisableParallelForRestriction]
        private NativeArray<float4> m_displacementBufferB;

        public OceanFFTRowJob(int resolution, int passCount, NativeArray<(int2, float2)> butterflyLookupTable, NativeArray<float2> heightSource, NativeArray<float4> displacementSource, NativeArray<float2> heightResult, NativeArray<float4> displacementResult)
        {
            m_resolution = resolution;
            m_passCount = passCount;
            m_butterflyLookupTable = butterflyLookupTable;
            m_heightBufferA = heightSource;
            m_displacementBufferA = displacementSource;
            m_heightBufferB = heightResult;
            m_displacementBufferB = displacementResult;
        }

        void IJobParallelFor.Execute(int y)
        {
            for (var passIndex = 0; passIndex < m_passCount; passIndex++)
            {
                var bufferFlip = (passIndex & 1) == 0;
                var heightSource = bufferFlip ? m_heightBufferA : m_heightBufferB;
                var heightResult = bufferFlip ? m_heightBufferB : m_heightBufferA;

                var displacementSource = bufferFlip ? m_displacementBufferA : m_displacementBufferB;
                var displacementResult = bufferFlip ? m_displacementBufferB : m_displacementBufferA;

                for (var x = 0; x < m_resolution; x++)
                {
                    var index = y * m_resolution + x;
                    var bftIdx = x + passIndex * m_resolution;
                    var (indices, weights) = m_butterflyLookupTable[bftIdx];

                    heightResult[index] = cadd(heightSource[indices.x + y * m_resolution], cmul(heightSource[indices.y + y * m_resolution], weights));

                    var displacementA = displacementSource[indices.x + y * m_resolution];
                    var displacementB = displacementSource[indices.y + y * m_resolution];
                    displacementResult[index] = float4(cadd(displacementA.xy, cmul(displacementB.xy, weights)), cadd(displacementA.zw, cmul(displacementB.zw, weights)));
                }
            }
        }
    }
}