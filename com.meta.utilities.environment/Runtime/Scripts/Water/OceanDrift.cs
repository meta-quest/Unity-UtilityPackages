// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Moves an object over time according to an offset and curve; used for simple fake ocean drifting animations
    /// </summary>
    public class OceanDrift : MonoBehaviour
    {
        [SerializeField] private Vector3 m_offset;
        [SerializeField] private AnimationCurve m_speedCurve;
        [SerializeField] private float m_duration;

        private Vector3 m_originalPosition;
        private float m_time;

        private void Start()
        {
            m_originalPosition = transform.position;
        }

        private void Update()
        {
            if (m_time < m_duration)
            {
                m_time += Time.deltaTime;
                var position = transform.position;
                var newPosition = m_originalPosition + m_offset * m_speedCurve.Evaluate(m_time / m_duration);
                transform.position = new Vector3(newPosition.x, position.y, newPosition.z);
            }
        }
    }
}
