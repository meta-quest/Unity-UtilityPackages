// Copyright (c) Meta Platforms, Inc. and affiliates.
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Meta.Utilities.MathUtils;
using static Unity.Mathematics.math;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Stores buffers related to ocean simulation and schedules updates of the ocean using burst jobs
    /// </summary>
    [ExecuteAlways]
    public class OceanSimulation : MonoBehaviour
    {
        private static readonly int s_smoothnessClose = Shader.PropertyToID("_Smoothness_Close");

        [field: SerializeField] public EnvironmentProfile Profile { get; set; }

        [SerializeField, Tooltip("Enables updates in editor, MAY CRASH UNITY")]
        private bool m_enableEditorUpdates = false;

        [SerializeField, Pow2(512), Tooltip("Resolution of the simulation, higher values give more detail, but are more expensive")]
        private int m_resolution = 128;

        public static OceanSimulation Instance { get; private set; }

        private const int BATCHCOUNT = 64;

        private NativeArray<float4> m_displacementBufferA, m_displacementBufferB, m_spectrum;
        private NativeArray<(int2, float2)> m_butterflyLookupTable;
        private NativeArray<float3> m_displacementResult;
        private NativeArray<float2> m_heightBufferA, m_heightBufferB;
        private NativeArray<float> m_dispersionTable;

        private NativeArray<float4>[] m_normalResult;

        public Texture2D DisplacementMap { get; private set; }
        public Texture2D NormalMap { get; private set; }
        public int Resolution => m_resolution;

        private JobHandle m_jobHandle;
        private bool m_hasPendingJobs;

        // Tracked so that changes can be recalculated
        private int m_cachedResolution;
        private int m_cachedSettingsVersion = -1;
        private EnvironmentProfile m_cachedProfile;
        private Vector3 m_cachedWindVector;

        private NativeArray<float> m_lengthToRoughness;

        private void GenerateLengthToRoughness()
        {
            // Generate a table that converts from shortened normal length to smoothness, used for smoothness mip filtering
            var lengthToRoughnessResolution = 256;
            m_lengthToRoughness = new NativeArray<float>(lengthToRoughnessResolution, Allocator.Persistent);
            for (var i = 0; i < lengthToRoughnessResolution; i++)
            {
                var uv = i / (lengthToRoughnessResolution - 1.0f);
                var target = Mathf.Lerp(2.0f / 3.0f, 1.0f, uv);

                var t = 0.0f;
                var minDelta = float.MaxValue;

                var steps = 256;
                for (var j = 0; j < steps; j++)
                {
                    var xi = j / (steps - 1.0f);
                    var currentLength = RoughnessToNormalLength(xi);

                    var delta = Mathf.Abs(currentLength - target);
                    if (delta < minDelta)
                    {
                        minDelta = delta;
                        t = xi;
                    }
                }

                m_lengthToRoughness[i] = t;
            }
        }

        // Converts a roughness value to an average normal length, based on the distribution, using an analytical formula for a GGX distribution
        // Note this is duplicated inside the burst job, to work around some issues with calling static functions from inside burst
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float RoughnessToNormalLength(float roughness)
        {
            if (roughness < 1e-3)
                return 1.0f;
            if (roughness >= 1.0)
                return 2.0f / 3.0f;

            var a = Mathf.Sqrt(Mathf.Clamp01(1.0f - roughness * roughness));
            return (a - (1.0f - a * a) * Atanh(a)) / (a * a * a);
        }

        // Rebuilds all the tables
        public void Initialize()
        {
            m_dispersionTable = new NativeArray<float>(m_resolution * m_resolution, Allocator.Persistent);
            m_spectrum = new NativeArray<float4>(m_resolution * m_resolution, Allocator.Persistent);

            DisplacementMap = new Texture2D(m_resolution, m_resolution, TextureFormat.RGBAHalf, true);
            NormalMap = new Texture2D(m_resolution, m_resolution, TextureFormat.RGBA32, true, true) { filterMode = FilterMode.Trilinear };

            ComputeButterflyLookupTable();

            var length = m_resolution * m_resolution;
            m_heightBufferA = new NativeArray<float2>(length, Allocator.Persistent);
            m_heightBufferB = new NativeArray<float2>(length, Allocator.Persistent);
            m_displacementBufferA = new NativeArray<float4>(length, Allocator.Persistent);
            m_displacementBufferB = new NativeArray<float4>(length, Allocator.Persistent);
            m_displacementResult = new NativeArray<float3>(length, Allocator.Persistent);

            // Normal mips are generated manually so that smoothness filtering can be applied, so we need to store the intermediate results in a series of arrays
            var mipCount = (int)Mathf.Log(m_resolution, 2) + 1;
            m_normalResult = new NativeArray<float4>[mipCount];

            for (var i = 0; i < mipCount; i++)
            {
                var mipSize = m_resolution >> i;
                m_normalResult[i] = new NativeArray<float4>(mipSize * mipSize, Allocator.Persistent);
            }

            // Save the new resolution, so we can track changes
            m_cachedResolution = m_resolution;

            GenerateLengthToRoughness();
        }

        public void Cleanup()
        {
            m_dispersionTable.Dispose();
            m_spectrum.Dispose();
            m_butterflyLookupTable.Dispose();

            m_heightBufferA.Dispose();
            m_heightBufferB.Dispose();
            m_displacementBufferA.Dispose();
            m_displacementBufferB.Dispose();
            m_displacementResult.Dispose();

            for (var i = 0; i < m_normalResult.Length; i++)
                m_normalResult[i].Dispose();

            m_normalResult = null;

            m_lengthToRoughness.Dispose();

            DestroyImmediate(DisplacementMap);
            DestroyImmediate(NormalMap);
        }

        private void OnEnable()
        {
            Initialize();

            Instance = this;
            m_cachedSettingsVersion = -1;
            m_cachedResolution = 0;
            m_cachedProfile = null;
        }

        private void OnDisable()
        {
            m_jobHandle.Complete();
            Cleanup();
        }

        public void UpdateSimulation(Vector3 windVector)
        {
            if (Profile == null)
            {
                Debug.LogWarning("Ocean Simulation has no profile assigned");
                return;
            }

            // Ensure the previous frame has completed processing
            m_jobHandle.Complete();

            // If resolution has changed, re-initialize
            var resolutionChanged = m_resolution != m_cachedResolution;
            if (resolutionChanged)
            {
                Cleanup();
                Initialize();
            }

            // In editor, force a recalculation when parameters are changed, as automation recalculations may be disabled as they cause crashes
            var forceUpdate = false;

            // Recalculate spectrum if profile, patch size or resolution has changed
            if (m_cachedSettingsVersion != Profile.Version || resolutionChanged || m_cachedProfile != Profile || m_cachedWindVector != windVector)
            {
                var horizontalWindVector = Vector3.ProjectOnPlane(windVector, Vector3.up);
                var windSpeed = horizontalWindVector.magnitude;
                var windDirection = new Vector2(horizontalWindVector.x, horizontalWindVector.z).normalized;

                var spectrumJob = new OceanSpectrumJob(Profile.OceanSettings.Directionality, Profile.OceanSettings.Gravity, windSpeed, Profile.OceanSettings.MinWaveSize, Profile.OceanSettings.PatchSize, Profile.OceanSettings.SequenceLength, m_resolution, windDirection, m_dispersionTable, m_spectrum);
                m_jobHandle = spectrumJob.Schedule(m_resolution * m_resolution, 64, m_jobHandle);
                m_cachedSettingsVersion = Profile.Version;
                m_cachedProfile = Profile;
                m_cachedWindVector = windVector;
                forceUpdate = true;
            }

            // Don't recalculate if editor updates are disabled and no chages have been made to avoid crasehes
            if (!forceUpdate && !Application.isPlaying && !m_enableEditorUpdates)
                return;

            // Calculate the iFFT
            var length = Square(m_resolution);

            var dispersion = new OceanDispersionJob(m_dispersionTable, m_spectrum, m_heightBufferB, m_displacementBufferB, m_resolution, Time.time * Profile.OceanSettings.TimeScale);

            m_jobHandle = dispersion.Schedule(length, BATCHCOUNT, m_jobHandle);

            var passes = (int)Mathf.Log(m_resolution, 2);

            Span<NativeArray<float2>> heightBuffers = stackalloc NativeArray<float2>[2] { m_heightBufferA, m_heightBufferB };
            Span<NativeArray<float4>> displacementBuffers = stackalloc NativeArray<float4>[2] { m_displacementBufferA, m_displacementBufferB };

            var j = 0;
            {
                // Gather across rows
                var fftJob = new OceanFFTRowJob(m_resolution, passes, m_butterflyLookupTable,
                    heightBuffers[~j & 1], displacementBuffers[~j & 1],
                    heightBuffers[j & 1], displacementBuffers[j & 1]);
                m_jobHandle = fftJob.Schedule(m_resolution, 1, m_jobHandle);
                j += passes;
            }

            {
                // Gather across columns
                var bufferFlip = j % 2 == 0;
                var fftJob = new OceanFFTColumnJob(m_resolution, passes - 1, m_butterflyLookupTable,
                    heightBuffers[~j & 1], displacementBuffers[~j & 1],
                    heightBuffers[j & 1], displacementBuffers[j & 1]);
                m_jobHandle = fftJob.Schedule(m_resolution, 1, m_jobHandle);
                j += 1;
            }

            {
                var displacementPixels = DisplacementMap.GetRawTextureData<half4>();
                // Final pass, slightly different logic is used
                var finalJob = new OceanFFTFinalJob(m_resolution, passes - 1, Profile.OceanSettings.Choppyness, m_butterflyLookupTable,
                    heightBuffers[~j & 1], displacementBuffers[~j & 1],
                    m_displacementResult, displacementPixels);
                m_jobHandle = finalJob.Schedule(length, BATCHCOUNT, m_jobHandle);
            }

            var smoothness = Profile.OceanMaterial.GetFloat(s_smoothnessClose);// TODO: Implement
            var normalPixels = NormalMap.GetRawTextureData<int>();
            var normalFoldJob = new OceanNormalFoldingJob(m_resolution, Profile.OceanSettings.PatchSize, smoothness, m_displacementResult, normalPixels, m_normalResult[0]);
            m_jobHandle = normalFoldJob.Schedule(length, BATCHCOUNT, m_jobHandle);

            // Generate normal/foam/smoothness mips and filter the smoothness based on the normal
            for (var i = 1; i < m_normalResult.Length; i++)
            {
                var mipResolution = m_resolution >> i;
                var pixelCount = (4 * m_resolution * m_resolution - 1) / 3;
                var mipCount = m_normalResult.Length;
                var endMipOffset = ((1 << (2 * (mipCount - i))) - 1) / 3;
                var mipOffset = pixelCount - endMipOffset;

                var mipFilterJob = new MipFilterJob(m_normalResult[i - 1], m_normalResult[i], normalPixels, m_lengthToRoughness, mipResolution, mipOffset);
                m_jobHandle = mipFilterJob.Schedule(mipResolution * mipResolution, BATCHCOUNT, m_jobHandle);
            }

            // Kick off jobs as soon as possible
            JobHandle.ScheduleBatchedJobs();
            m_hasPendingJobs = true;
        }

        public void BeginContextRendering()
        {
            // Ignore multiple calls to BeginContextRendering
            if (m_hasPendingJobs)
            {
                // Ensure all jobs have completed
                m_jobHandle.Complete();
                DisplacementMap.Apply();
                NormalMap.Apply(false, false);

                m_hasPendingJobs = false;
            }
        }

        private int BitReverse(int i)
        {
            return (int)(reversebits((uint)i) >> (lzcnt(m_resolution) + 1));
        }

        private void ComputeButterflyLookupTable()
        {
            var passes = (int)Mathf.Log(m_resolution, 2);
            m_butterflyLookupTable = new(m_resolution * passes, Allocator.Persistent);

            for (var i = 0; i < passes; i++)
            {
                var nBlocks = (int)Mathf.Pow(2, passes - 1 - i);
                var nHInputs = (int)Mathf.Pow(2, i);

                for (var j = 0; j < nBlocks; j++)
                {
                    for (var k = 0; k < nHInputs; k++)
                    {
                        int i1, i2, j1, j2;
                        if (i == 0)
                        {
                            i1 = j * nHInputs * 2 + k;
                            i2 = j * nHInputs * 2 + nHInputs + k;
                            j1 = BitReverse(i1);
                            j2 = BitReverse(i2);
                        }
                        else
                        {
                            i1 = j * nHInputs * 2 + k;
                            i2 = j * nHInputs * 2 + nHInputs + k;
                            j1 = i1;
                            j2 = i2;
                        }

                        var wr = Mathf.Cos(2.0f * Mathf.PI * (k * nBlocks) / m_resolution);
                        var wi = Mathf.Sin(2.0f * Mathf.PI * (k * nBlocks) / m_resolution);

                        var offset1 = i1 + i * m_resolution;
                        m_butterflyLookupTable[offset1] = (new(j1, j2), new(wr, wi));

                        var offset2 = i2 + i * m_resolution;
                        m_butterflyLookupTable[offset2] = (new(j1, j2), new(-wr, -wi));
                    }
                }
            }
        }

#if UNITY_EDITOR
        // Can be used to generate a normal map based on current ocean settings, for shader usage etc
        [ContextMenu("Save Normal Map")]
        private void SaveNormalMap()
        {
            var path = UnityEditor.EditorUtility.SaveFilePanel("Save File", Application.dataPath, "Ocean Normal", "png");

            m_jobHandle.Complete();

            var material = new Material(Shader.Find("Hidden/Ocean Normal Blit"));
            var temp = RenderTexture.GetTemporary(NormalMap.width, NormalMap.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(NormalMap, temp, material);

            var tempTexture = temp.ToTexture2D();
            RenderTexture.ReleaseTemporary(temp);
            var pngBytes = tempTexture.EncodeToPNG();

            UnityEngine.Windows.File.WriteAllBytes(path, pngBytes);
            UnityEditor.AssetDatabase.Refresh();
        }
#endif
    }
}