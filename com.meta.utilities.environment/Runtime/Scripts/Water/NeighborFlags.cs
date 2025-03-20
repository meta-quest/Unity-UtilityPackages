// Copyright (c) Meta Platforms, Inc. and affiliates.
using System;

/// <summary>
/// Flags used by the quadtree system to indicate directions that have a different lod level and need to use an edge-stiching mesh to avoid cracks in the quadtree surface
/// </summary>
[Flags]
public enum NeighborFlags
{
    None = 0,
    Right = 1,
    Up = 2,
    Left = 4,
    Down = 8,
    Right2 = 16,
    Up2 = 32,
    Left2 = 64,
    Down2 = 128,
}