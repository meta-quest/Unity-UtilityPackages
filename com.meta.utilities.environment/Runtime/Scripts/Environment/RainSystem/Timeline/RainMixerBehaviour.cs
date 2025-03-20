// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEngine;
using UnityEngine.Playables;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Timeline mixer behaviour for the rain effect
    /// </summary>
    public class RainMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var inputCount = playable.GetInputCount();

            for (var i = 0; i < inputCount; i++)
            {
                var inputWeight = playable.GetInputWeight(i);
                if (inputWeight > 0.01f)
                {
                    var inputPlayable = (ScriptPlayable<RainBehaviour>)playable.GetInput(i);
                    _ = inputPlayable.GetBehaviour();

                    Shader.SetGlobalFloat("_RainTraisition", inputWeight * 2 - 1);
                }
            }
        }
    }
}