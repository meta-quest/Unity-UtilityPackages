// Copyright (c) Meta Platforms, Inc. and affiliates.
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Computes the initial ocean spectrum using a phillips spectrum, based on "Simulating Ocean Water" by Jerry Tessendorf
    /// https://people.computing.clemson.edu/~jtessen/reports/papers_files/coursenotes2004.pdf
    /// </summary>

    // Setting FloatMode.fast here causes unity to crash for some reason
    [BurstCompile(FloatPrecision.Low, FloatMode.Default, CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
    public struct OceanSpectrumJob : IJobParallelFor
    {
        private float m_directionality, m_gravity, m_windSpeed, m_minWaveLength, m_patchSize, m_sequenceLength;
        private int m_resolution;
        private float2 m_windDirection;

        [WriteOnly]
        private NativeArray<float> m_dispersionTable;

        [WriteOnly]
        private NativeArray<float4> m_spectrum;

        public OceanSpectrumJob(float directionality, float gravity, float windSpeed, float minWaveLength, float patchSize, float sequenceLength, int resolution, float2 windDirection, NativeArray<float> dispersionTable, NativeArray<float4> spectrum)
        {
            m_directionality = directionality;
            m_gravity = gravity;
            m_windSpeed = windSpeed;
            m_minWaveLength = minWaveLength;
            m_patchSize = patchSize;
            m_sequenceLength = sequenceLength;
            m_resolution = resolution;
            m_windDirection = windDirection;
            m_dispersionTable = dispersionTable;
            m_spectrum = spectrum;
        }

        private readonly float PhillipsSpectrum(float2 k, float kLength)
        {
            // Amplitude normalization
            var fftNorm = pow(m_resolution, -0.25f);
            var philNorm = E / m_patchSize;
            var a = MathUtils.Square(fftNorm * philNorm);

            var maxWaveHeight = MathUtils.Square(m_windSpeed) / m_gravity;
            var kdotw = dot(k, m_windDirection);
            var phillips = a * exp(-1 / pow(kLength * maxWaveHeight, 2)) / pow(kLength, 4) * pow(kdotw, 6);

            // Remove small wavelengths
            phillips *= exp(-pow(kLength * m_minWaveLength, 2));

            phillips = sqrt(phillips);

            // Move waves along wind direction
            if (kdotw < 0.0f)
                phillips *= -sqrt(1.0f - m_directionality);

            return phillips;
        }

        void IJobParallelFor.Execute(int index)
        {
            var n = index % m_resolution - m_resolution / 2;
            var m = index / m_resolution - m_resolution / 2;

            if (n == 0 && m == 0)
            {
                m_spectrum[index] = 0;
                return;
            }

            var k = 2.0f * PI * float2(n, m) / m_patchSize;
            var rcpKLength = rsqrt(dot(k, k));
            var kLength = rcp(rcpKLength);

            // Eq 31
            var w = sqrt(m_gravity * kLength);

            // Eq 35
            var w0 = 2.0f * PI / m_sequenceLength;
            m_dispersionTable[index] = floor(w / w0) * w0;

            // Gaussian random numbers
            var random = Random.CreateFromIndex((uint)index);
            var u = random.NextFloat4();

            var r = sqrt(-2.0f * log(u.xz));
            var theta = 2.0f * PI * u.yw;

            var xi = float4(r.x * sin(theta.x), r.x * cos(theta.x), r.y * sin(theta.y), r.y * cos(theta.y));

            // Eq 42
            var kDir = k * rcpKLength;
            var s = 1.0f / sqrt(2.0f) * xi * float2(PhillipsSpectrum(kDir, kLength), PhillipsSpectrum(-kDir, kLength)).xxyy * float4(1, 1, 1, -1);

            // Transform into the representation required by OceanDispersionJob
            s = s.xyxy + float4(s.zw, -s.zw);
            s.zw = float2(s.w, -s.z);

            m_spectrum[index] = s;
        }
    }
}