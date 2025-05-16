// Copyright (c) Meta Platforms, Inc. and affiliates.
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Calculates the mipmaps for the combined normal/foam/smoothness texture, and applies custom filtering to the smoothness channel
    /// </summary>
    [BurstCompile(FloatPrecision.Low, FloatMode.Default, CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance)]
    public struct MipFilterJob : IJobParallelFor
    {
        private int m_resolution, m_mipOffset;

        [ReadOnly]
        private NativeArray<float4> m_source;

        [ReadOnly]
        private NativeArray<float> m_lengthToRoughness;

        [WriteOnly]
        private NativeArray<float4> m_destination;

        [WriteOnly, NativeDisableParallelForRestriction]
        private NativeArray<int> m_normalPixels;

        public MipFilterJob(NativeArray<float4> source, NativeArray<float4> destination, NativeArray<int> normalPixels, NativeArray<float> lengthToRoughness, int resolution, int mipOffset)
        {
            m_source = source;
            m_destination = destination;
            m_normalPixels = normalPixels;
            m_lengthToRoughness = lengthToRoughness;
            m_resolution = resolution;
            m_mipOffset = mipOffset;
        }

        // Converts an average normal length to an equivalent smoothness value
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float NormalLengthToSmoothness(float normalLength)
        {
            var lengthToRoughnessResolution = 256;
            var halfTexelSize = 0.5f / lengthToRoughnessResolution;
            var t = Mathf.InverseLerp(2.0f / 3.0f, 1.0f, normalLength);
            var uv = Mathf.Lerp(halfTexelSize, 1.0f - halfTexelSize, t);

            var texel = uv * lengthToRoughnessResolution - 0.5f;
            var coord = Mathf.Floor(texel);
            var interp = texel - coord;
            var val0 = m_lengthToRoughness[Mathf.Clamp((int)coord, 0, lengthToRoughnessResolution - 1)];
            var val1 = m_lengthToRoughness[Mathf.Clamp((int)coord + 1, 0, lengthToRoughnessResolution - 1)];
            var roughness = Mathf.Lerp(val0, val1, interp);
            return 1.0f - Mathf.Sqrt(roughness);
        }

        // Fetches four pixels from the above mip level and calculates an average normal length and a filtered smoothness value
        void IJobParallelFor.Execute(int index)
        {
            var x = index % m_resolution * 2;
            var y = index / m_resolution * 2;

            // Gather the 4 pixels that cover this mip's texel footprint
            var bl = m_source[x + 0 + (y + 0) * m_resolution * 2];
            var br = m_source[x + 1 + (y + 0) * m_resolution * 2];
            var tl = m_source[x + 0 + (y + 1) * m_resolution * 2];
            var tr = m_source[x + 1 + (y + 1) * m_resolution * 2];

            // average the values and write out the averaged value to the normal array. (Note this is not normalized, which is important to allow the
            // filtered normal length to propogate all the way down the mip chain. Normalized values are written to m_normalPixels)
            var result = (bl + br + tl + tr) * 0.25f;
            m_destination[index] = result;

            // Filter the averaged normal length
            var normal = result.xyz;
            var normalLength = length(normal);
            var smoothness = NormalLengthToSmoothness(normalLength);

            // Pack and write out the final normal, foam and smoothness values into a RGBA8 texture (Single int)
            result = float4(normalize(normal).xz * 0.5f + 0.5f, saturate(0.5f * result.w + 0.5f), smoothness);
            var packedOutput = int4(round(result * 255));
            m_normalPixels[m_mipOffset + index] = packedOutput.x | packedOutput.y << 8 | packedOutput.z << 16 | packedOutput.w << 24;
        }
    }
}
