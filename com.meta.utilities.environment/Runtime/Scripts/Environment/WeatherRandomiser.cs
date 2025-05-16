// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace Meta.Utilities.Environment
{
    public class WeatherRandomiser : MonoBehaviour
    {
        [SerializeField, AutoSet] private EnvironmentSystem m_environmentSystem;
        [SerializeField] private EnvironmentProfile[] m_environmentProfiles;
        [SerializeField] private float m_minTransitionDelay, m_maxTransitionDelay;
        [SerializeField] private bool m_ordered;
        private int m_next = 0;
        private float m_time;
        private float m_timer;

        private void Start()
        {
            SetTime();
        }

        private void Update()
        {
            if (m_timer > m_time)
            {
                SetTime();
                m_environmentSystem.SetProfile(m_environmentProfiles[m_next]);
                m_next = m_ordered ? m_next + 1 : Random.Range(0, m_environmentProfiles.Length);
                if (m_next >= m_environmentProfiles.Length)
                    m_next = 0;
            }
            m_timer += Time.deltaTime;
        }

        private void SetTime()
        {
            m_time = Random.Range(m_minTransitionDelay, m_maxTransitionDelay);
            m_timer = 0;
        }
    }
}
