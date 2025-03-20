// Copyright (c) Meta Platforms, Inc. and affiliates.
using System.Collections;
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Provides a very simple buoyancy model based on a single sample point and offset, with a built in delayed sinking behaviour
    /// </summary>
    public class SimpleBouyancy : MonoBehaviour
    {
        [SerializeField] private float m_offset;
        [SerializeField] private int m_heightIterations = 4;

        [SerializeField] private float m_sinkDuration = 1.0f;
        [SerializeField] private float m_sinkOffset = 1.0f;

        public void Sink()
        {
            _ = StartCoroutine(SinkInternal());
        }

        private IEnumerator SinkInternal()
        {
            var timer = 0.0f;
            var initialOffset = m_offset;
            while (timer < m_sinkDuration)
            {
                yield return null;
                timer += Time.deltaTime;
                m_offset = Mathf.Lerp(initialOffset, initialOffset - m_sinkOffset, timer / m_sinkDuration);
            }
        }

        private void Update()
        {
            var position = transform.position;
            position.y = EnvironmentSystem.Instance.GetOceanHeightIterative(position, m_heightIterations) + m_offset;
            transform.position = position;
        }

        private void OnDrawGizmosSelected()
        {
            var envSystem = FindObjectOfType<EnvironmentSystem>();

            if (envSystem != null)
            {
                for (var x = -10; x <= 10; x++)
                {
                    for (var z = -10; z <= 10; z++)
                    {
                        var position = transform.position + new Vector3(x, 0, z);
                        position.y = EnvironmentSystem.Instance.GetOceanHeightIterative(position, m_heightIterations);
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawCube(position, Vector3.one * 0.1f);
                    }
                }
            }
        }
    }
}
