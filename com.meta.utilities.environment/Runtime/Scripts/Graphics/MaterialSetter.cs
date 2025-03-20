// Copyright (c) Meta Platforms, Inc. and affiliates.
using System;
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Sets a renderers material using a mask
    /// </summary>
    public class MaterialSetter : MonoBehaviour
    {
        [SerializeField] private MeshRenderer m_meshRenderer;
        [SerializeField] private MaterialMask m_mask;

        //TODO: find a way to let a person select potentially multiple materials to be set, that can allow infinite materials in the MeshRenderer
        [Flags]
        public enum MaterialMask
        {
            A = 1 << 1,
            B = 1 << 2,
            C = 1 << 3,
            D = 1 << 4,
            E = 1 << 5,
            F = 1 << 6
        }

        public void SetTargetMaterials(MaterialMask mask)
        {
            m_mask = mask;
        }

        public void SetMaterial(Material newMaterial)
        {
            var mask = (int)m_mask;

            for (var i = 0; i < m_meshRenderer.materials.Length; i++)
            {
                if ((mask & (1 << i)) == 0)
                {
                    m_meshRenderer.materials[i] = newMaterial; //TODO change this to material (Need to intake multiple materials)
                }
            }
        }
    }
}
