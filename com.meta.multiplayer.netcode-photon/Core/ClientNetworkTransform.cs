// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Meta.Utilities;
using Unity.Netcode.Components;
using UnityEngine;

namespace Meta.Multiplayer.Core
{
    /// <summary>
    /// Used for syncing a transform with client side changes. This includes host.
    /// Pure server as owner isn't supported by this. Please use NetworkTransform
    /// for transforms that'll always be owned by the server.
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        public bool IgnoreUpdates { get; set; }

        protected void UpdateCanCommit() => CanCommitToTransform = NetworkObject.IsOwner;

        protected Action m_resetToAuthoritativeAction;
        protected void ResetToAuthoritativeState() => m_resetToAuthoritativeAction?.Invoke();

        public override void OnNetworkSpawn()
        {
            m_resetToAuthoritativeAction =
                this.GetMethod<Action>("ResetInterpolatedStateToCurrentAuthoritativeState");

            base.OnNetworkSpawn();
            UpdateCanCommit();

            if (CanCommitToTransform)
            {
                // workaround for issue
                // https://github.com/Unity-Technologies/com.unity.netcode.gameobjects/issues/1560#issuecomment-1013217835
                var cache = ScaleThreshold;
                ScaleThreshold = -1;

                var position = InLocalSpace ? transform.localPosition : transform.position;
                var rotation = InLocalSpace ? transform.localRotation : transform.rotation;
                var scale = transform.localScale;
                Teleport(position, rotation, scale);

                ScaleThreshold = cache;
            }
        }

        protected override void Update()
        {
            UpdateCanCommit();

            var cacheLocalPosition = transform.localPosition;
            var cacheLocalRotation = transform.localRotation;
            base.Update();

            if (IgnoreUpdates)
            {
                transform.localPosition = cacheLocalPosition;
                transform.localRotation = cacheLocalRotation;
                ResetToAuthoritativeState();
            }

            if (NetworkManager != null && (NetworkManager.IsConnectedClient || NetworkManager.IsListening))
            {
                if (CanCommitToTransform)
                {
                    TryCommitTransformToServer(transform, NetworkManager.LocalTime.Time);
                }
            }
        }

        public void ForceSync()
        {
            var thresholds = (PositionThreshold, RotAngleThreshold, ScaleThreshold);
            (PositionThreshold, RotAngleThreshold, ScaleThreshold) = (-1, -1, -1);

            TryCommitTransformToServer(transform, NetworkManager.LocalTime.Time);

            (PositionThreshold, RotAngleThreshold, ScaleThreshold) = thresholds;
        }


        public override void OnGainedOwnership()
        {
            UpdateCanCommit();
            base.OnGainedOwnership();
        }

        public override void OnLostOwnership()
        {
            UpdateCanCommit();
            base.OnLostOwnership();
        }

        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
