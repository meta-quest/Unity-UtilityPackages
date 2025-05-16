// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Timeline track for the rain effect
    /// </summary>
    [TrackClipType(typeof(RainAsset))]
    [TrackBindingType(typeof(RainController))]
    public class RainTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<RainMixerBehaviour>.Create(graph, inputCount);
        }
    }
}
