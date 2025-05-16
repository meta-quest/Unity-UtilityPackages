// Copyright (c) Meta Platforms, Inc. and affiliates.
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Meta.Utilities.MathUtils;
using static Unity.Mathematics.math;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Calculates mipmaps for the normal map, as well as the jacobian/foam folding map, and filtered smoothness values (Stored in the alpha channel)
    /// </summary>
    [BurstCompile(FloatPrecision.Low, FloatMode.Default, CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
    public struct OceanNormalFoldingJob : IJobParallelFor
    {
        private int m_resolution;
        private float m_patchSize, m_smoothness;

        [ReadOnly]
        private NativeArray<float3> m_displacementResult;

        [WriteOnly]
        private NativeArray<int> m_normalPixels;

        [WriteOnly]
        private NativeArray<float4> m_normalFoamSmoothnessResult;

        public OceanNormalFoldingJob(int resolution, float patchSize, float smoothness, NativeArray<float3> displacementResult, NativeArray<int> normalPixels, NativeArray<float4> normalFoamSmoothnessResult)
        {
            m_resolution = resolution;
            m_patchSize = patchSize;
            m_displacementResult = displacementResult;
            m_normalPixels = normalPixels;
            m_smoothness = smoothness;
            m_normalFoamSmoothnessResult = normalFoamSmoothnessResult;
        }

        // Converts a roughness value to an average normal length, based on the distribution, using an analytical formula for a GGX distribution
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float RoughnessToNormalLength(float roughness)
        {
            if (roughness < 1e-3)
                return 1.0f;
            if (roughness >= 1.0)
                return 2.0f / 3.0f;

            var a = sqrt(saturate(1.0f - roughness * roughness));
            return (a - (1.0f - a * a) * Atanh(a)) / (a * a * a);
        }

        void IJobParallelFor.Execute(int index)
        {
            var x = index % m_resolution;
            var y = index / m_resolution;

            // Fetch the four neighboring pixels (Left/right/up/down)
            var left = m_displacementResult[Wrap(x - 1, m_resolution) + y * m_resolution];
            var right = m_displacementResult[Wrap(x + 1, m_resolution) + y * m_resolution];
            var down = m_displacementResult[x + Wrap(y - 1, m_resolution) * m_resolution];
            var up = m_displacementResult[x + Wrap(y + 1, m_resolution) * m_resolution];

            // Compute the central difference and use that for the normal
            var rcpDelta = m_resolution / m_patchSize;

            var dx = (right - left) * 0.5f * rcpDelta;
            var dz = (up - down) * 0.5f * rcpDelta;

            // Calculate normal from displacement
            var normal = normalize(float3(-dx.y, 1.0f, -dz.y));

            // Compute jacobian and store in w
            var jxx = 1.0f + dx.x;
            var jyy = 1.0f + dz.z;
            var jyx = dz.z;
            var jxy = dx.x;

            var jacobian = jxx * jyy - jxy * jyx;
            var result = float4(normal.xz * 0.5f + 0.5f, saturate(0.5f * jacobian + 0.5f), m_smoothness);

            var normalLength = RoughnessToNormalLength(Square(1.0f - m_smoothness));
            m_normalFoamSmoothnessResult[index] = float4(normal * normalLength, jacobian);

            // Pack into RGBA8
            var packedOutput = int4(round(result * 255));
            m_normalPixels[index] = packedOutput.x | packedOutput.y << 8 | packedOutput.z << 16 | packedOutput.w << 24;
        }
    }
}