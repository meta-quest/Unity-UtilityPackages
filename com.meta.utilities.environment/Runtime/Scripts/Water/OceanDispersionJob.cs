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
    /// Updates the dispersion values of the ocean for the current time/ocean parameters in preparation for the FFT
    /// </summary>
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
    public struct OceanDispersionJob : IJobParallelFor
    {
        private int m_log2Resolution, m_resolutionMask;
        private float m_time, m_halfResolutionF;

        [ReadOnly]
        private NativeArray<float> m_dispersionTable;

        [ReadOnly]
        private NativeArray<float4> m_spectrum;

        [WriteOnly]
        private NativeArray<float2> m_heightBuffer;

        [WriteOnly]
        private NativeArray<float4> m_displacementBuffer;

        public OceanDispersionJob(NativeArray<float> dispersionTable, NativeArray<float4> spectrum, NativeArray<float2> heightBuffer, NativeArray<float4> displacementBuffer, int resolution, float time)
        {
            m_dispersionTable = dispersionTable;
            m_spectrum = spectrum;
            m_heightBuffer = heightBuffer;
            m_displacementBuffer = displacementBuffer;
            m_time = time;
            m_log2Resolution = floorlog2(resolution);
            m_halfResolutionF = resolution >> 1;
            m_resolutionMask = (1 << m_log2Resolution) - 1;
        }

        void IJobParallelFor.Execute(int index)
        {
            // Contains some equations from https://people.computing.clemson.edu/~jtessen/reports/papers_files/coursenotes2004.pdf, though some have been rearranged for better performance

            // Eq 43
            var wkt = m_dispersionTable[index] * m_time;

            // Eq 36
            var h0 = m_spectrum[index];
            float2 direction; sincos(wkt, out direction.y, out direction.x);
            var h = h0.xy * direction.x + h0.zw * direction.y;
            m_heightBuffer[index] = h;

            // Eq 44
            var k = float2(index & m_resolutionMask, index >> m_log2Resolution) - m_halfResolutionF;
            k *= rsqrt(max(1f, dot(k, k)));
            var d = conj(h.yx).xyxy * k.xxyy;
            m_displacementBuffer[index] = d;
        }
    }
}