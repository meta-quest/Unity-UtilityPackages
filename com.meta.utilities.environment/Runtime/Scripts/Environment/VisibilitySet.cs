// Copyright (c) Meta Platforms, Inc. and affiliates.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif
namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Tags an object with a visibility set that will be managed by the global VisibilityController, allowing objects to be selectively enabled/disabled based on the currently active set
    /// </summary>
    public class VisibilitySet : MonoBehaviour
    {
        [SerializeField] private VisibilitySetData m_visibilitySetData;
        [SerializeField, Tooltip("Level this object will be set to on Awake if the set has not already been set to a specific level")] private int m_defaultLevel = 0;
        [SerializeField] private bool m_disableRenderers;
        [SerializeField] private int m_level;
        [SerializeField] private int m_maxLevel = 5;
        [SerializeField] private GameObjectGroup[] m_visibilitySets;

        [Serializable]
        private struct GameObjectGroup
        {
            public GameObject[] Objects;
        }

        private void Awake()
        {
            Assert.IsNotNull(m_visibilitySetData, "Visibility Set without VisibilitySetData");

            var defaultLevel = VisibilityController.Instance.GetCurrentVisibilitySetLevel(m_visibilitySetData, out var wasFound);
            if (!wasFound) defaultLevel = m_defaultLevel;
            VisibilityController.Instance.AddVisibilitySet(m_visibilitySetData, this, defaultLevel);
            SetVisibilityLevel(defaultLevel);
        }

        private void OnDestroy()
        {
            Assert.IsNotNull(m_visibilitySetData, "Visibility Set without VisibilitySetData");

            // Due to script ordering, visibility controller might get destroyed beforehand 
            if (VisibilityController.Instance != null)
                VisibilityController.Instance.RemoveVisibilitySet(m_visibilitySetData, this);
        }

        public void SetVisibilityLevel(int level)
        {
            Assert.AreNotEqual(m_visibilitySets.Length, 0, "Visibility Set with no GameObjects assigned");

            // Disable all objects/renderers at first and then enable the active level to allow for overlapping groups of objects
            for (var i = 0; i < m_visibilitySets.Length; i++)
            {
                foreach (var obj in m_visibilitySets[i].Objects)
                {
                    if (m_disableRenderers)
                    {
                        if (obj.TryGetComponent(out Renderer renderer))
                        {
                            renderer.enabled = false;
                        }
                    }
                    else
                    {
                        obj.SetActive(false);
                    }
                }
            }

            for (var i = 0; i < m_visibilitySets.Length; i++)
            {
                var shouldEnable = i == level || (level > i && i == m_visibilitySets.Length - 1 && level <= m_maxLevel);
                if (!shouldEnable) continue;

                foreach (var obj in m_visibilitySets[i].Objects)
                {
                    if (m_disableRenderers)
                    {
                        if (obj.TryGetComponent(out Renderer renderer))
                        {
                            renderer.enabled = true;
                        }
                    }
                    else
                    {
                        obj.SetActive(true);
                    }
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Converts an existing LODGroup component into a visibility set component with the correct settings
        /// </summary>
        [ContextMenu("Convert from lod group")]
        private void Convert()
        {
            if (!TryGetComponent(out LODGroup lodGroup))
                return;

            var lodList = new List<GameObjectGroup>();
            for (var i = 0; i < lodGroup.lodCount; i++)
            {
                var lod = lodGroup.GetLODs()[i];
                foreach (var renderer in lod.renderers)
                {
                    var renderObject = renderer.gameObject;
                    if (renderer.gameObject == gameObject)
                    {
                        var newGo = new GameObject(gameObject.name + $"_LOD{i}");
                        newGo.transform.parent = transform;
                        newGo.transform.SetSiblingIndex(i);
                        newGo.transform.localPosition = Vector3.zero;
                        newGo.transform.localRotation = Quaternion.identity;
                        newGo.transform.localScale = Vector3.one;
                        renderObject = newGo;

                        var newRenderer = newGo.AddComponent(renderer.GetType());
                        EditorUtility.CopySerialized(renderer, newRenderer);

                        if (TryGetComponent(out MeshFilter filter))
                        {
                            var newFilter = newGo.AddComponent<MeshFilter>();
                            EditorUtility.CopySerialized(filter, newFilter);
                            DestroyImmediate(filter);
                        }
                        DestroyImmediate(renderer);
                    }

                    lodList.Add(new GameObjectGroup { Objects = new GameObject[] { renderObject } });
                }
            }
            m_visibilitySets = lodList.ToArray();
            DestroyImmediate(lodGroup);
        }
#endif
    }
}
