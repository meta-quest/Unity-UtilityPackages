// Copyright (c) Meta Platforms, Inc. and affiliates.
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Updates a flat quadtree and submits visible patches for rendering
    /// </summary>
    [ExecuteAlways]
    public class QuadtreeRenderer : MonoBehaviour
    {
        private static readonly int s_oceanSqDistanceLimit = Shader.PropertyToID("_OceanSqDistanceLimit");
        private const int MAXINSTANCESPERDRAW = 128;
        private const int INDEXBUFFERCOUNT = 16;

        [SerializeField, Tooltip("Disable quadtree updates for debugging")]
        private bool m_updateQuadtree = true;

        [field: SerializeField]
        public Material Material { get; set; }

        [SerializeField, Tooltip("World Size of the Ocean")]
        private float m_size = 1024;

        [SerializeField, Tooltip("Number of vertices per patch")]
        private int m_vertexCount = 32;

        [SerializeField, Range(0, 12), Tooltip("Number of lods used for distant water")]
        private int m_lodLevels = 4;

        [SerializeField, Tooltip("Max height of ocean displacement")]
        private float m_maxHeight = 32;

        [SerializeField, Tooltip("Scales bounds of a patch for culling to avoid highly-displaced vertices getting culled")]
        private float m_cullingBoundScale = 1.5f;

        [SerializeField] private float m_lodThreshold = 1.5f;

        [SerializeField] private float m_skirtingSize = 10000f;

        private Mesh m_mesh;
        private Mesh m_skirtingMesh;

        // Used to track changes in inspector
        private int m_cachedVersion = -1;
        public int Version { get; set; }

        private Plane[] m_frustumPlanes = new Plane[6];
        private NativeArray<float4> m_frustumPlanesNative;
        private Matrix4x4[][] m_matrixMeshPairs = new Matrix4x4[INDEXBUFFERCOUNT][]; // Matrix array for each index
        private int[] m_instanceLodCounters = new int[INDEXBUFFERCOUNT]; // Stores current counter for each index buffer list
        private NativeArray<float> m_subdivisionMap;
        private NativeList<QuadTreePatch> m_visiblePatches;

        private int LodMapWidth => 1 << m_lodLevels; // 2^lodLevels
        private int LodMapResolution => 1 << (m_lodLevels << 1); // (2^x)^2

        private Vector3 m_currentGridCenter;

        public void BeginContextRendering(List<Camera> cameras, MaterialPropertyBlock propertyBlock, float displacementTexelsPerMeter)
        {
            // Sanity check due to editor vs playmode issues
            if (Material == null)
                return;

            // if settings have changed, re-initialize
            if (m_cachedVersion != Version)
            {
                Initialize();
                m_cachedVersion = Version;
            }

            var distanceLimit = m_size / 2f - m_size / m_vertexCount / 2;
            propertyBlock.SetFloat(s_oceanSqDistanceLimit, distanceLimit * distanceLimit);

            foreach (var camera in cameras)
            {
                // Only render in scene+game+reflection view
                if (camera.cameraType is not CameraType.Game and not CameraType.SceneView and not CameraType.Reflection)
                    continue;

                if (m_updateQuadtree)
                {
                    // Snap the origin of the quadtree to the vertex size of the largest subdivision to prevent sliding vertices
                    // Assume 3rd subdiv level, eg full grid divided into 4, then each of those divided into 4 again since this is the min lod level unless camera is very far away, and sliding will likely not be noticable
                    // However since our quad pattern repeats once every two patches, multiply this by 2
                    var cellSize = m_size / m_vertexCount / 2;
                    m_currentGridCenter = new Vector3(MathUtils.Snap(camera.transform.position.x, cellSize), 0.0f, MathUtils.Snap(camera.transform.position.z, cellSize));

                    GeometryUtility.CalculateFrustumPlanes(camera, m_frustumPlanes);

                    // Copy into native array
                    for (var i = 0; i < m_frustumPlanes.Length; i++)
                    {
                        var plane = m_frustumPlanes[i];
                        m_frustumPlanesNative[i] = new float4(plane.normal, plane.distance);
                    }

                    for (var i = 0; i < 16; i++)
                        m_instanceLodCounters[i] = 0;

                    m_visiblePatches.Clear();

                    Profiler.BeginSample("Quadtree Check");
                    new CheckQuadtreeJob(m_cullingBoundScale, m_maxHeight, m_lodThreshold, m_size, m_lodLevels, LodMapWidth, m_currentGridCenter, camera.transform.position, m_frustumPlanesNative, m_visiblePatches, m_subdivisionMap).Run();
                    Profiler.EndSample();

                    Profiler.BeginSample("Quadtree Render");
                    foreach (var patch in m_visiblePatches)
                    {
                        RenderQuadtree(patch.Level, patch.X, patch.Y);
                    }
                    Profiler.EndSample();
                }

                for (var i = 0; i < m_matrixMeshPairs.Length; i++)
                {
                    var count = m_instanceLodCounters[i];
                    if (count == 0)
                        continue;

                    Graphics.DrawMeshInstanced(m_mesh, i, Material, m_matrixMeshPairs[i], count, propertyBlock, ShadowCastingMode.Off, false, gameObject.layer, camera, LightProbeUsage.Off);
                }

                // If a skirting range is required
                if (m_skirtingSize > m_size)
                {
                    var rp = new RenderParams(Material);
                    rp.camera = camera;
                    rp.matProps = propertyBlock;
                    rp.shadowCastingMode = ShadowCastingMode.Off;
                    rp.layer = gameObject.layer;
                    rp.receiveShadows = false;
                    rp.lightProbeUsage = LightProbeUsage.Off;

                    //Updated Graphics.DrawMesh to newer Graphics.RenderMesh. Graphics.DrawMesh is obsolete per the Unity documentation
                    Graphics.RenderMesh(rp, m_skirtingMesh, 0, Matrix4x4.TRS(m_currentGridCenter, Quaternion.Euler(0f, 0f, 0f), Vector3.one));
                    Graphics.RenderMesh(rp, m_skirtingMesh, 0, Matrix4x4.TRS(m_currentGridCenter, Quaternion.Euler(0f, 90f, 0f), Vector3.one));
                    Graphics.RenderMesh(rp, m_skirtingMesh, 0, Matrix4x4.TRS(m_currentGridCenter, Quaternion.Euler(0f, 180f, 0f), Vector3.one));
                    Graphics.RenderMesh(rp, m_skirtingMesh, 0, Matrix4x4.TRS(m_currentGridCenter, Quaternion.Euler(0f, 270f, 0f), Vector3.one));
                }
            }
        }

        private void Initialize()
        {
            var size = m_vertexCount + 1;
            var vertices = new Vector3[size * size];
            var xDelta = 1f / m_vertexCount;
            var yDelta = 1f / m_vertexCount;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var index = x + y * size;

                    Vector3 vertex;
                    vertex.x = x * xDelta - 0.5f;
                    vertex.y = 0;
                    vertex.z = y * yDelta - 0.5f;
                    vertices[index] = vertex;
                }
            }

            m_mesh = new Mesh
            {
                name = "Ocean Quad",
                vertices = vertices,
                subMeshCount = 16,
            };

            const int SKIRTINGSEGMENTS = 3;
            var skirtingVerts = new Vector3[SKIRTINGSEGMENTS * 2];
            var skirtingIndices = new ushort[6 * (SKIRTINGSEGMENTS - 1)];
            var quadIndices = new ushort[6] { 0, 1, 3, 0, 3, 2, };
            for (var i = 0; i < SKIRTINGSEGMENTS; ++i)
            {
                var direction = new Vector3((float)i / (SKIRTINGSEGMENTS - 1) * 2f - 1f, 0f, 1f);
                skirtingVerts[i * 2 + 0] = direction * m_size / 2f;
                skirtingVerts[i * 2 + 1] = direction * m_skirtingSize / 2f;
            }
            for (var i = 0; i < skirtingIndices.Length; i++)
            {
                skirtingIndices[i] = (ushort)(i / 6 * 2 + quadIndices[i % 6]);
            }
            m_skirtingMesh = new Mesh() { name = "Skirting", vertices = skirtingVerts, };
            m_skirtingMesh.SetTriangles(skirtingIndices, 0, true);

            // Create a mesh for each possible quadtree orientation
            for (var i = 0; i < 16; i++)
            {
                var indices = GetIndexBuffer((NeighborFlags)i, size);
                m_mesh.SetTriangles(indices, i, false);
            }

            m_mesh.RecalculateBounds();
            m_mesh.Optimize();

            m_subdivisionMap = new NativeArray<float>(LodMapResolution, Allocator.Persistent);
        }

        private void OnEnable()
        {
            m_cachedVersion = -1;

            for (var i = 0; i < 16; i++)
            {
                m_matrixMeshPairs[i] = new Matrix4x4[MAXINSTANCESPERDRAW];
            }

            m_frustumPlanesNative = new NativeArray<float4>(6, Allocator.Persistent);
            m_visiblePatches = new(Allocator.Persistent);
        }

        private void OnDisable()
        {
            m_frustumPlanesNative.Dispose();
            m_subdivisionMap.Dispose();
            m_visiblePatches.Dispose();
        }

        private void RenderQuadtree(int lod, int x0, int y0)
        {
            var stepSize = 1 << (m_lodLevels - lod); // 2^(x-y)
            var x = x0 * stepSize;
            var y = y0 * stepSize;

            var flags = NeighborFlags.None;
            var right = x < LodMapWidth - stepSize ? m_subdivisionMap[x + stepSize + y * LodMapWidth] : lod;
            if (right < lod)
                flags |= NeighborFlags.Right;

            var up = y < LodMapWidth - stepSize ? m_subdivisionMap[x + (y + stepSize) * LodMapWidth] : lod;
            if (up < lod)
                flags |= NeighborFlags.Up;

            var left = x >= stepSize ? m_subdivisionMap[x - stepSize + y * LodMapWidth] : lod;
            if (left < lod)
                flags |= NeighborFlags.Left;

            var down = y >= stepSize ? m_subdivisionMap[x + (y - stepSize) * LodMapWidth] : lod;
            if (down < lod)
                flags |= NeighborFlags.Down;

            var index = (int)flags;
            var count = m_instanceLodCounters[index];

            if (count >= MAXINSTANCESPERDRAW)
            {
                Debug.LogError("Reached max instance per draw count. Size needs to be increased");
            }
            else
            {
                var patchSize = m_size / (1 << lod);

                Vector3 center;
                center.y = 0;
                center.x = m_currentGridCenter.x - 0.5f * m_size + (x0 + 0.5f) * patchSize;
                center.z = m_currentGridCenter.z - 0.5f * m_size + (y0 + 0.5f) * patchSize;

                m_matrixMeshPairs[index][count] = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one * patchSize);
                m_instanceLodCounters[index] = ++count;
            }
        }

        private static List<ushort> GetIndexBuffer(NeighborFlags flags, int size)
        {
            // Indices need some special handling
            var indices = new List<ushort>();

            // Iterate four cells at a time, to simplify creation logic
            for (var y = 0; y < (size - 1) / 2; y++)
            {
                for (var x = 0; x < (size - 1) / 2; x++)
                {
                    var index = (x + y * size) * 2;

                    // Flags for this current patch of 4 quads
                    var patchFlags = NeighborFlags.None;
                    if (flags.HasFlag(NeighborFlags.Left) && x == 0) patchFlags |= NeighborFlags.Left;
                    if (flags.HasFlag(NeighborFlags.Right) && x == (size - 1) / 2 - 1) patchFlags |= NeighborFlags.Right;
                    if (flags.HasFlag(NeighborFlags.Down) && y == 0) patchFlags |= NeighborFlags.Down;
                    if (flags.HasFlag(NeighborFlags.Up) && y == (size - 1) / 2 - 1) patchFlags |= NeighborFlags.Up;

                    var edges = s_edgeIndices[(int)patchFlags];
                    foreach (var edge in edges)
                    {
                        indices.Add((ushort)(index + edge[0] + edge[1] * size));
                        indices.Add((ushort)(index + edge[2] + edge[3] * size));
                        indices.Add((ushort)(index + edge[4] + edge[5] * size));
                    }

                }
            }

            return indices;
        }

        // Each index is a pair of three x/y offsets, indexed by the neighbor flags
        private static readonly int[][][] s_edgeIndices =
        {
			// Each inner int[] is three vector2's, representing the horizontal and vertical offsets 
			// Coordinates 
			// None 0
			new int[][]
            {
                new int[]{0, 0, 0, 1, 1, 1 },
                new int[]{1, 1, 1, 0, 0, 0 },

                new int[]{1, 1, 2, 1, 2, 0 },
                new int[]{2, 0, 1, 0, 1, 1 },

                new int[]{0, 2, 1, 2, 1, 1 },
                new int[]{1, 1, 0, 1, 0, 2 },

                new int[]{1, 1, 1, 2, 2, 2 },
                new int[]{2, 2, 2, 1, 1, 1 },
            },

			// Right 1
			new int[][]
            {
                new int[]{0, 0, 0, 1, 1, 1 },
                new int[]{1, 1, 1, 0, 0, 0 },

                new int[]{0, 2, 1, 2, 1, 1 },
                new int[]{1, 1, 0, 1, 0, 2 },

                new int[]{1, 0, 1, 1, 2, 0 },
                new int[]{2, 0, 1, 1, 2, 2 },
                new int[]{1, 1, 1, 2, 2, 2 },
            },
			
			// Up 2
			new int[][]
            {
                new int[]{0, 0, 0, 1, 1, 1 },
                new int[]{1, 1, 1, 0, 0, 0 },

                new int[]{1, 1, 2, 1, 2, 0 },
                new int[]{2, 0, 1, 0, 1, 1 },

                new int[]{1, 1, 0, 2, 2, 2 },
                new int[]{2, 2, 2, 1, 1, 1 },

                new int[]{0, 1, 0, 2, 1, 1 },
            },

			// Up Right 3
			new int[][]
            {
                new int[]{0, 0, 0, 1, 1, 1 },
                new int[]{1, 1, 1, 0, 0, 0 },

                new int[]{1, 0, 1, 1, 2, 0 },
                new int[]{2, 0, 1, 1, 2, 2 },

                new int[]{1, 1, 0, 2, 2, 2 },
                new int[]{0, 1, 0, 2, 1, 1 },
            },

			// Left 4
			new int[][]
            {
                new int[]{1, 1, 1, 0, 0, 0 },

                new int[]{1, 1, 2, 1, 2, 0 },
                new int[]{2, 0, 1, 0, 1, 1 },

                new int[]{0, 0, 0, 2, 1, 1 },
                new int[]{1, 1, 0, 2, 1, 2 },

                new int[]{1, 1, 1, 2, 2, 2 },
                new int[]{2, 2, 2, 1, 1, 1 },
            },

			// Left and Right? 5
			new int[][] { },

			// Upper left 6
			new int[][]
            {
                new int[]{0, 0, 0, 2, 1, 1 },
                new int[]{1, 1, 1, 0, 0, 0 },

                new int[]{1, 0, 1, 1, 2, 0 },
                new int[]{2, 0, 1, 1, 2, 1 },

                new int[]{1, 1, 0, 2, 2, 2 },
                new int[]{2, 2, 2, 1, 1, 1 },
            },

			// Up Left Right 7 
			new int[][]
            {
                new int[]{0, 0, 0, 1, 1, 1 },
                new int[]{1, 1, 1, 0, 0, 0 },

                new int[]{1, 1, 2, 1, 2, 0 },
                new int[]{2, 0, 1, 0, 1, 1 },

                new int[]{0, 1, 0, 2, 1, 1 },
                new int[]{1, 1, 0, 2, 2, 2 },
                new int[]{2, 2, 2, 1, 1, 1 },
            },

			// Down 8
			new int[][]
            {
                new int[]{0, 0, 0, 1, 1, 1 },
                new int[]{2, 0, 0, 0, 1, 1 },
                new int[]{1, 1, 2, 1, 2, 0 },
                new int[]{0, 2, 1, 2, 1, 1 },
                new int[]{1, 1, 0, 1, 0, 2 },
                new int[]{1, 1, 1, 2, 2, 2 },
                new int[]{2, 2, 2, 1, 1, 1 },
            },

			// Lower Right 9
			new int[][]
            {
                new int[]{1, 1, 0, 0, 0, 1 },
                new int[]{0, 1, 0, 2, 1, 1 },
                new int[]{1, 1, 0, 2, 1, 2 },
                new int[]{1, 2, 2, 2, 1, 1 },
                new int[]{1, 1, 2, 2, 2, 0 },
                new int[]{2, 0, 0, 0, 1, 1 },
            },

			// Up Down 10
			new int[][] { },

			// Up Down Right 11
			new int[][]
            {
                new int[]{0, 0, 0, 1, 1, 1 },
                new int[]{0, 0, 1, 1, 2, 0 },

                new int[]{2, 0, 1, 1, 2, 2 },
                new int[]{1, 1, 0, 2, 2, 2 },

                new int[]{0, 1, 0, 2, 1, 1 },
            },

			// Down Left 12
			new int[][]
            {
                new int[]{0, 0, 0, 2, 1, 1 },
                new int[]{1, 1, 0, 2, 1, 2 },
                new int[]{1, 2, 2, 2, 1, 1 },
                new int[]{2, 2, 2, 1, 1, 1 },
                new int[]{2, 1, 2, 0, 1, 1 },
                new int[]{1, 1, 2, 0, 0, 0 },
            },

			// Down Left Right 13
			new int[][]
            {
                new int[]{0, 0, 1, 1, 2, 0 },
                new int[]{0, 0, 0, 2, 1, 1 },
                new int[]{2, 0, 1, 1, 2, 2 },
                new int[]{1, 1, 0, 2, 1, 2 },
                new int[]{1, 1, 1, 2, 2, 2 },
            },

			// Up Down Left 14
			new int[][]
            {
                new int[]{0, 0, 0, 2, 1, 1 },
                new int[]{0, 0, 1, 1, 2, 0 },
                new int[]{1, 1, 0, 2, 2, 2 },
                new int[]{2, 0, 1, 1, 2, 1 },
                new int[]{2, 2, 2, 1, 1, 1 },
            },

			// All 15
			new int[][]
            {
                new int[]{0, 0, 1, 1, 2, 0 },
                new int[]{0, 0, 0, 2, 1, 1 },
                new int[]{1, 1, 0, 2, 2, 2 },
                new int[]{0, 2, 1, 1, 2, 2 },
            }
    };
    }
}