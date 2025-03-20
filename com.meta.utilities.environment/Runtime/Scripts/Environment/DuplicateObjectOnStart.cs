// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace Meta.Utilities.Environment
{
    public class DuplicateObjectOnStart : MonoBehaviour
    {
        [SerializeField] private int m_copies;
        private void Start()
        {
            enabled = false;
            Destroy(this);
            for (var i = 0; i < m_copies; i++)
            {
                _ = Instantiate(gameObject);
            }
        }
    }
}
