// Copyright (c) Meta Platforms, Inc. and affiliates.
using System;
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Empty Scriptable Object to manage object visibility
    /// </summary>
    [CreateAssetMenu(menuName = "Data/Visibility Set")]
    public class VisibilitySetData : ScriptableObject
    {
        [field: SerializeField] public Tuple<VisibilitySetData, int>[] Levels { get; private set; } = Array.Empty<Tuple<VisibilitySetData, int>>();
    }
}
