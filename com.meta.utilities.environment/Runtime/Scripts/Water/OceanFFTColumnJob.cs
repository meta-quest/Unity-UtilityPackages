// Copyright (c) Meta Platforms, Inc. and affiliates.
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Meta.Utilities.MathUtils;
using static Unity.Mathematics.math;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Processes the columns of the FFT
    /// </summary>
    [BurstCompile(FloatPrecision.Low, FloatMode.Default, CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
    public struct OceanFFTColumnJob : IJobParallelFor
    {
        private int m_resolution;
        private int m_passCount;

        [ReadOnly]
        private NativeArray<(int2, float2)> m_butterflyLookupTable;

        [ReadOnly]
        private NativeArray<float2> m_heightSource;

        [ReadOnly]
        private NativeArray<float4> m_displacementSource;

        [WriteOnly, NativeDisableParallelForRestriction]
        private NativeArray<float2> m_heightResult;

        [WriteOnly, NativeDisableParallelForRestriction]
        private NativeArray<float4> m_displacementResult;

        public OceanFFTColumnJob(int resolution, int passCount, NativeArray<(int2, float2)> butterflyLookupTable, NativeArray<float2> heightSource, NativeArray<float4> displacementSource, NativeArray<float2> heightResult, NativeArray<float4> displacementResult)
        {
            m_resolution = resolution;
            m_passCount = passCount;
            m_butterflyLookupTable = butterflyLookupTable;
            m_heightSource = heightSource;
            m_displacementSource = displacementSource;
            m_heightResult = heightResult;
            m_displacementResult = displacementResult;
        }

        void IJobParallelFor.Execute(int x)
        {
            Span<float2> heightSource = stackalloc float2[m_resolution];
            Span<float4> displacementSource = stackalloc float4[m_resolution];
            Span<float2> heightResult = stackalloc float2[m_resolution];
            Span<float4> displacementResult = stackalloc float4[m_resolution];

            // Copy data into dense array
            for (var y = 0; y < m_resolution; y++)
            {
                heightSource[y] = m_heightSource[x + y * m_resolution];
                displacementSource[y] = m_displacementSource[x + y * m_resolution];
            }

            // Evaluate requested passes
            for (var passIndex = 0; passIndex < m_passCount; passIndex++)
            {
                for (var y = 0; y < m_resolution; y++)
                {
                    var bftIdx = y + passIndex * m_resolution;
                    var (indices, weights) = m_butterflyLookupTable[bftIdx];

                    heightResult[y] = cadd(heightSource[indices.x], cmul(heightSource[indices.y], weights));

                    var displacementA = displacementSource[indices.x];
                    var displacementB = displacementSource[indices.y];
                    displacementResult[y] = float4(cadd(displacementA.xy, cmul(displacementB.xy, weights)), cadd(displacementA.zw, cmul(displacementB.zw, weights)));
                }
                var heightSwap = heightSource;
                heightSource = heightResult;
                heightResult = heightSwap;
                var displacementSwap = displacementSource;
                displacementSource = displacementResult;
                displacementResult = displacementSwap;
            }

            // Copy back into sparse array
            for (var y = 0; y < m_resolution; y++)
            {
                m_heightResult[x + y * m_resolution] = heightSource[y];
                m_displacementResult[x + y * m_resolution] = displacementSource[y];
            }
        }
    }
}