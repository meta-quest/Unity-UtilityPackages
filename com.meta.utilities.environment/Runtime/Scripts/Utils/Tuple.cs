// Copyright (c) Meta Platforms, Inc. and affiliates.
using System;
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Serializable version of a simple tuple.
    /// </summary>
    /// <typeparam name="T">First item type</typeparam>
    /// <typeparam name="K">Second item type</typeparam>
    [Serializable]
    public struct Tuple<T, K>
    {
        // These should really start at 0, but this matches C# System.Tuple behaviour. Note these need to have a getter and setter to be serialized by Unity
        [field: SerializeField] public T Item1 { get; private set; }
        [field: SerializeField] public K Item2 { get; private set; }

        public Tuple(T item1, K item2)
        {
            Item1 = item1;
            Item2 = item2;
        }
    }
}
