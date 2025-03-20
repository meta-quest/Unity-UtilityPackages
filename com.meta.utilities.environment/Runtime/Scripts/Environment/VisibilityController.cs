// Copyright (c) Meta Platforms, Inc. and affiliates.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Manages the visibility system for controlling both lod levels and general activation of objects within scenes
    /// 
    /// Visibilty sets are defined using VisibilitySetData scriptable objects and then assigned via the VisibilitySet 
    /// component to individual game objects. VisibilitySetData data contains references to other sets and level's 
    /// related sets should use while it is active
    /// 
    /// Examples include when the player is in the ship cabin while the door is closed vs open, as well as the different 
    /// deck locations. This allows us to precisely control what lods are used and what game objects are active at certain
    /// locations and situations
    /// </summary>
    public class VisibilityController : Singleton<VisibilityController>
    {
        private readonly Dictionary<VisibilitySetData, ActiveVisibilitySetLevelData> m_visibilitySets = new();

        [SerializeField, Tooltip("Default visibility levels that get applied on load, and when areas transition, so that all areas can be given a sensible default value")] private VisibilitySetData m_defaultSet = null;

        [SerializeField] private VisibilitySetData m_activeSet;

        private bool m_prewarmPhase = true;

        public VisibilitySetData ActiveVisibilitySet
        {
            get => m_activeSet;
            set
            {
                // If there was an active visibility set, set its level back to 0
                if (m_activeSet != null && m_defaultSet != null)
                {
                    SetVisibilityLevelInternal(m_defaultSet);
                }

                // Now set the new level
                m_activeSet = value;
                SetVisibilityLevelInternal(m_activeSet);
            }
        }

        private void Start()
        {
            // Wait a frame to activate sets to make sure that any Start()/Awake() calls are still completed before objects are deactivated (prevent potential hitches later on)
            _ = StartCoroutine(DeferredStart());
        }

        private IEnumerator DeferredStart()
        {
            yield return null;
            m_prewarmPhase = false;

            // Set specified levels to default values
            if (m_defaultSet != null) ActiveVisibilitySet = m_defaultSet;
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                if (m_activeSet == null) m_activeSet = m_defaultSet;
                if (m_activeSet != null)
                {
                    SetVisibilityLevelInternal(m_activeSet);
                }
            }
        }

        public void AddVisibilitySet(VisibilitySetData data, VisibilitySet set, int defaultLevel)
        {
            if (!m_visibilitySets.TryGetValue(data, out var list))
            {
                // If creating a new list, set a default level
                list = new(defaultLevel);
                m_visibilitySets.Add(data, list);
            }

            list.AddVisibilitySet(set);
        }

        public void RemoveVisibilitySet(VisibilitySetData data, VisibilitySet set)
        {
            var containsKey = m_visibilitySets.TryGetValue(data, out var list);
            Assert.IsTrue(containsKey);
            list.RemoveVisibilitySet(set);
        }

        public int GetCurrentVisibilitySetLevel(VisibilitySetData visibilitySet, out bool wasFound)
        {
            // During prewarming phase force all sets to use a level of 0 to ensure all gameobjects are properly activated during loading
            if (m_prewarmPhase)
            {
                wasFound = true;
                return 0;
            }

            // Try to get active level first
            if (m_visibilitySets.TryGetValue(visibilitySet, out var data))
            {
                wasFound = true;
                return data.CurrentLevel;
            }

            if (m_defaultSet != null)
            {
                // If active level does not specify, fall back to default
                // TODO: Hash set or something with constant time lookup?
                var list = m_defaultSet.Levels;

                for (var i = 0; i < list.Length; i++)
                {
                    var element = list[i];
                    if (element.Item1 != visibilitySet)
                        continue;

                    wasFound = false;
                    return element.Item2;
                }
            }

            // Return finest lod otherwise
            wasFound = false;
            return 0;
        }

        private void SetVisibilityLevelInternal(VisibilitySetData visibilitySet)
        {
            if (visibilitySet == null) return;


            if (m_visibilitySets.TryGetValue(visibilitySet, out var list))
            {
                list.SetLevel(0);
            }

            foreach (var setLevel in visibilitySet.Levels)
            {
                if (m_visibilitySets.TryGetValue(setLevel.Item1, out list))
                    list.SetLevel(setLevel.Item2);
            }
        }
    }
}
