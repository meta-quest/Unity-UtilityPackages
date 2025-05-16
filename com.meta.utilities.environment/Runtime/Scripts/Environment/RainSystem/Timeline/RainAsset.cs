// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEngine;
using UnityEngine.Playables;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Handles timeline integration for the Rain effect
    /// </summary>
    public class RainAsset : PlayableAsset
    {
        public RainBehaviour Template;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<RainBehaviour>.Create(graph, Template);
            return playable;
        }
    }
}
