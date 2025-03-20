// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEngine;
using UnityEngine.Serialization;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// This component applies buoyancy forces to iself based on the ocean simulation
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Buoyancy : MonoBehaviour
    {
        [Space(10), SerializeField]
        private MeshRenderer m_mesh;
        [SerializeField]
        private Rigidbody m_rigidbody;
        [SerializeField]
        private float m_waterHeightDivider = 2;

        [Space(10), SerializeField, Tooltip("Upwards force from water.")]
        private float m_buoyancy = 1;
        [SerializeField, Tooltip("Upwards force from water.")]
        private float m_smoothFactor = 1;

        private const float DENSITY = 9.77f;
        private const float GRAVITY = 9.8f;

        [SerializeField, Tooltip("How many nodes on either side of an axis. Each node is used to tell where on the object is underwater.")]
        private int m_scanExtent = 2;
        private int NodeCount => (m_scanExtent * 2 + 1) * (m_scanExtent * 2 + 1);

        [Space(10), SerializeField, Tooltip("Drag when fully out of water")]
        private float m_airDrag = 1;
        [SerializeField, Tooltip("Drag when fully submerged")]
        private float m_underwaterDrag = 4;

        [Space(5), SerializeField, Tooltip("Angular drag when fully out of water")]
        private float m_airAngularDrag = 1;
        [SerializeField, Tooltip("Angular drag when fully submerged")]
        private float m_underwaterAngularDrag = 4;

        [HideInInspector]
        private WaterRow[] m_waterOffsets;

        private struct WaterRow
        {
            public float[] Heights;

            public int Length => Heights.Length;

            public float this[int index]
            {
                get => Heights[index];
                set => Heights[index] = value;
            }
        }

        private void Start()
        {
            //This is here to serialize the waterHeight based on the m_scanExtent
            //TODO: move to constant or determine if OnValidate or OnSerialize is a better spot for this
            m_waterOffsets = new WaterRow[m_scanExtent * 2 + 1];

            for (var x = -m_scanExtent; x <= m_scanExtent; x++)
            {
                m_waterOffsets[x + m_scanExtent].Heights = new float[m_scanExtent * 2 + 1];
            }
        }

        private void FixedUpdate()
        {

            for (var x = -m_scanExtent; x <= m_scanExtent; x++)
            {
                for (var z = -m_scanExtent; z <= m_scanExtent; z++)
                {
                    var position = GetPosition(x, z);

                    m_waterOffsets[x + m_scanExtent][z + m_scanExtent] = GetOceanHeight(position) - position.y;

                    var force = GetForce(x, z);

                    if (force > 0)
                    {
                        m_rigidbody.AddForceAtPosition(Vector3.up * force, position);
                    }
                }
            }

            var subPercent = SubmergedPercentage();
            m_rigidbody.drag = Mathf.Lerp(m_airDrag, m_underwaterDrag, subPercent);
            m_rigidbody.angularDrag = Mathf.Lerp(m_airAngularDrag, m_underwaterAngularDrag, subPercent);
        }

        private float GetOceanHeight(Vector3 position)
        {
            return EnvironmentSystem.Instance == null ? 0 : EnvironmentSystem.Instance.GetOceanHeight(position) / m_waterHeightDivider;
        }

        private Vector3 GetPosition(float x, float z)
        {
            var extent = m_mesh.bounds.extents;
            var position = new Vector3(extent.x * (x / m_scanExtent), -extent.y, extent.z * (z / m_scanExtent));
            position = m_mesh.bounds.center + position;
            return position;
        }

        private float GetForce(int x, int z)
        {
            var oceanOffset = m_waterOffsets[x + m_scanExtent][z + m_scanExtent];
            if (oceanOffset <= 0)
            {
                return 0;
            }

            var force = m_buoyancy * DENSITY * GRAVITY / NodeCount;
            var smooth = Mathf.Clamp01(oceanOffset / m_smoothFactor);

            return smooth * force;
        }

        private float SubmergedPercentage()
        {
            var average = 0f;

            for (var i = 0; i < m_waterOffsets.Length; i++)
            {
                for (var j = 0; j < m_waterOffsets[i].Length; j++)
                {
                    average += m_waterOffsets[i][j] / m_mesh.bounds.size.y;
                }
            }
            return average / NodeCount;
        }

        private void OnDrawGizmos()
        {
            for (var x = -m_scanExtent; x <= m_scanExtent; x++)
            {
                for (var z = -m_scanExtent; z <= m_scanExtent; z++)
                {
                    var position = GetPosition(x, z);
                    var force = 0;// GetForce(position, x, z);

                    var oceanOffset = m_waterOffsets != null && m_waterOffsets.Length != 0 && m_waterOffsets[x + m_scanExtent].Length != 0
                        ? m_waterOffsets[x + m_scanExtent][z + m_scanExtent]
                        : (GetOceanHeight(position) - position.y);

                    if (force > 0)
                    {
                        Gizmos.DrawCube(position + Vector3.up * force, Vector3.one * 0.1f);
                        Gizmos.DrawLine(position, position + Vector3.up * force);

                        Gizmos.color = Color.red;
                    }
                    else
                    {
                        Gizmos.color = Color.white;
                    }

                    Gizmos.DrawCube(position, Vector3.one * 0.05f);

                    Gizmos.color = Color.green;

                    //Draw the offset from the bottom of the AABB to the waterheight
                    Gizmos.DrawCube(position + Vector3.up * oceanOffset / 2, Vector3.one * 0.2f + Vector3.up * oceanOffset);

                    Gizmos.color = Color.white;
                }
            }
        }
    }
}
