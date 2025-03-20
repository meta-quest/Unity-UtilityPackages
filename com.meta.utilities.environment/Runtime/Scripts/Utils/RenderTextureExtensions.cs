// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Meta.Utilities.Environment
{
    public static class RenderTextureExtensions
    {
        public static void Clear(this RenderTexture renderTexture, bool clearDepth, bool clearColor, Color backgroundColor, float depth = 1.0f)
        {
            Graphics.SetRenderTarget(renderTexture);
            GL.Clear(clearDepth, clearColor, backgroundColor, depth);
        }

        public static Texture2D ToTexture2D(this RenderTexture renderTexture)
        {
            var texture = new Texture2D(renderTexture.width, renderTexture.height, renderTexture.graphicsFormat, renderTexture.useMipMap ? TextureCreationFlags.MipChain : TextureCreationFlags.None);

            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            RenderTexture.active = null;

            return texture;
        }

        /// <summary>
        /// Convenience function to ensure a RenderTexture is always created. (Eg when using GetTemporary and ComputeShaders)
        /// </summary>
        /// <param name="renderTexture">The RenderTexture to Create</param>
        /// <returns>The created RenderTExture</returns>
        public static RenderTexture Created(this RenderTexture renderTexture)
        {
            if (!renderTexture.IsCreated())
            {
                _ = renderTexture.Create();
            }

            return renderTexture;
        }

        /// <summary>
        /// Compares a RenderTexture to a descriptor, and re-creates it if any parameterse change.
        /// </summary>
        /// <param name="renderTexture"></param>
        /// <param name="descriptor"></param>
        public static void CheckRenderTexture(ref RenderTexture renderTexture, RenderTextureDescriptor descriptor)
        {
            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(descriptor);
            }
            else if (!renderTexture.descriptor.Equals(descriptor))
            {
                renderTexture.Release();
                renderTexture.descriptor = descriptor;
                _ = renderTexture.Create();
            }
        }
    }
}