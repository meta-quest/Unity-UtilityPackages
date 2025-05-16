// Copyright (c) Meta Platforms, Inc. and affiliates.
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Keeps track of all objects belonging to a specific active visibility set
    /// </summary>
    public class ActiveVisibilitySetLevelData
    {
        public int CurrentLevel { get; private set; }
        private List<VisibilitySet> m_visibilitySets = new();

        public ActiveVisibilitySetLevelData(int currentLevel) => CurrentLevel = currentLevel;

        public void AddVisibilitySet(VisibilitySet visibilitySet)
        {
            if (!m_visibilitySets.Contains(visibilitySet))
                m_visibilitySets.Add(visibilitySet);
        }

        public bool ContainsVisibilitySet(VisibilitySet visibilitySet)
        {
            return m_visibilitySets.Contains(visibilitySet);
        }

        public void RemoveVisibilitySet(VisibilitySet visibilitySet)
        {
            var wasRemoved = m_visibilitySets.Remove(visibilitySet);
            Assert.IsTrue(wasRemoved);
        }

        public void SetLevel(int level)
        {
            CurrentLevel = level;

            foreach (var visibilitySet in m_visibilitySets)
            {
                Assert.IsNotNull(visibilitySet);
                visibilitySet.SetVisibilityLevel(CurrentLevel);
            }
        }
    }
}
