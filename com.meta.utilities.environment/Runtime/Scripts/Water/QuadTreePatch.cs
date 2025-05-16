// Copyright (c) Meta Platforms, Inc. and affiliates.
namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Stores data for a quadTree patch
    /// </summary>
    public readonly struct QuadTreePatch
    {
        public readonly int X, Y, Level;

        public QuadTreePatch(int x, int y, int level)
        {
            X = x;
            Y = y;
            Level = level;
        }
    }
}