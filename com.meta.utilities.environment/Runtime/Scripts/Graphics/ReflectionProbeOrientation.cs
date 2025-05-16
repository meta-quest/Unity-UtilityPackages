// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Support for custom reflection probe orientation (to compensate for boat movement)
    /// </summary>
    [ExecuteAlways]
    public class ReflectionProbeOrientation : MonoBehaviour
    {
        private Matrix4x4 m_bakeOrientation;

        private void Awake()
        {
            ResetBakeOrientation();
        }

        private void OnEnable()
        {
            Debug.Assert(ActiveProbeOrientation == null,
                "Two ReflectionProbeOrientation instances are active", this);
            ActiveProbeOrientation = this;
        }
        private void OnDisable()
        {
            if (ActiveProbeOrientation == this)
                ActiveProbeOrientation = null;
        }

        public void ResetBakeOrientation()
        {
            m_bakeOrientation = transform.localToWorldMatrix;
        }

        public Matrix4x4 GetReorientationMatrix() => transform.worldToLocalMatrix * m_bakeOrientation;

        public static ReflectionProbeOrientation ActiveProbeOrientation { get; private set; }
    }
}
