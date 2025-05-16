// Copyright (c) Meta Platforms, Inc. and affiliates.
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Burst job for frustum culling and subdividing the levels of a quadtree
    /// </summary>
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = false)]
    public struct CheckQuadtreeJob : IJob
    {
        private float m_cullingBoundScale, m_maxHeight, m_lodThreshold, m_size;
        private int m_lodLevels, m_lodMapWidth;
        private float3 m_center, m_viewPosition;

        [ReadOnly]
        private NativeArray<float4> m_frustumPlanes;

        [WriteOnly]
        private NativeList<QuadTreePatch> m_visiblePatches;

        [WriteOnly]
        private NativeArray<float> m_subdivisionMap;

        public CheckQuadtreeJob(float cullingBoundScale, float maxHeight, float lodThreshold, float size, int lodLevels, int lodMapWidth, float3 center, float3 viewPosition, NativeArray<float4> frustumPlanes, NativeList<QuadTreePatch> visiblePatches, NativeArray<float> subdivisionMap)
        {
            m_cullingBoundScale = cullingBoundScale;
            m_maxHeight = maxHeight;
            m_lodThreshold = lodThreshold;
            m_size = size;
            m_lodLevels = lodLevels;
            m_lodMapWidth = lodMapWidth;
            m_center = center;
            m_viewPosition = viewPosition;
            m_frustumPlanes = frustumPlanes;
            m_visiblePatches = visiblePatches;
            m_subdivisionMap = subdivisionMap;
        }

        // Start the job by checking the first level of the quadtree
        void IJob.Execute()
        {
            CheckQuadtree(0, 0, 0, 0, false);
        }

        // Tests an AABB against an array of planes and returns whether it is outside any of them
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool TestPlanesAABB(NativeArray<float4> planes, float3 center, float3 extents)
        {
            for (var i = 0; i < 6; i++)
            {
                var plane = planes[i];
                var point = center + select(-extents, extents, plane.xyz >= 0.0f);
                if (dot(point, plane.xyz) + plane.w < 0)
                    return false;
            }

            return true;
        }

        // Checks the level of a quadtree, and recursively checks levels until reaching the max level
        private void CheckQuadtree(int x, int y, int level, int lod, bool hasRendered)
        {
            var size = m_size / (1 << level);

            float3 center;
            center.y = 0;
            center.x = m_center.x - 0.5f * m_size + (x + 0.5f) * size;
            center.z = m_center.z - 0.5f * m_size + (y + 0.5f) * size;

            var delta = center - m_viewPosition;
            var sqDist = dot(delta, delta);
            var targetSize = size * m_lodThreshold;

            // If at the desired subdivision for this distance, perform a frustum check and add to a rendering list if visible
            if (!hasRendered && sqDist >= targetSize * targetSize)
            {
                // Frustum cull check
                var extents = new float3(size * m_cullingBoundScale, m_maxHeight, size * m_cullingBoundScale) * 0.5f;
                if (TestPlanesAABB(m_frustumPlanes, center, extents))
                {
                    m_visiblePatches.Add(new QuadTreePatch(x, y, level));
                    hasRendered = true;
                }
            }

            // If we're not at the max subdiv level, subdivide and check the four leaf nodes
            if (level < m_lodLevels)
            {
                var shouldSubdivide = sqDist < targetSize * targetSize;
                if (shouldSubdivide && !hasRendered)
                    lod++;

                CheckQuadtree(x * 2 + 0, y * 2 + 0, level + 1, lod, hasRendered);
                CheckQuadtree(x * 2 + 1, y * 2 + 0, level + 1, lod, hasRendered);
                CheckQuadtree(x * 2 + 0, y * 2 + 1, level + 1, lod, hasRendered);
                CheckQuadtree(x * 2 + 1, y * 2 + 1, level + 1, lod, hasRendered);
            }
            else
            {
                var index = y * m_lodMapWidth + x;
                m_subdivisionMap[index] = lod;

                if (!hasRendered)
                {
                    // Frustum cull check
                    var extents = new float3(size * m_cullingBoundScale, m_maxHeight, size * m_cullingBoundScale) * 0.5f;
                    if (TestPlanesAABB(m_frustumPlanes, center, extents))
                    {
                        m_visiblePatches.Add(new QuadTreePatch(x, y, level));
                    }
                }
            }
        }
    }
}