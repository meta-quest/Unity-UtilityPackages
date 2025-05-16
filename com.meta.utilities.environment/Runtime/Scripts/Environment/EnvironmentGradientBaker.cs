// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// This can be used to generate an array of environment data/spherical harmonics for an environment profile from the current skybox.
    /// </summary>
    public class EnvironmentGradientBaker : MonoBehaviour
    {
        [ColorUsage(true, true)]
        public Color SkyColor;
        [ColorUsage(true, true)]
        public Color EquatorColor;
        [ColorUsage(true, true)]
        public Color GroundColor;

        [SerializeField] private EnvironmentProfile[] m_profilesToBake;
        [SerializeField] private ReflectionProbe m_probe;

        [ContextMenu("Bake Gradients")]
        private void BakeGradients()
        {
            _ = StartCoroutine(BakeGradients_Internal());
        }

        // From: https://github.com/pieroaccardi/Unity_SphericalHarmonics_Tools/blob/master/Assets/SH/Scripts/SphericalHarmonics.cs
        public static TextureFormat ConvertFormat(RenderTextureFormat inputFormat)
        {
            TextureFormat outputFormat;
            switch (inputFormat)
            {
                case RenderTextureFormat.ARGB32:
                    outputFormat = TextureFormat.RGBA32;
                    break;

                case RenderTextureFormat.ARGBHalf:
                    outputFormat = TextureFormat.RGBAHalf;
                    break;

                case RenderTextureFormat.ARGBFloat:
                    outputFormat = TextureFormat.RGBAFloat;
                    break;

                default:
                    var formatString = System.Enum.GetName(typeof(RenderTextureFormat), inputFormat);
                    var formatINT = (int)System.Enum.Parse(typeof(TextureFormat), formatString);
                    outputFormat = (TextureFormat)formatINT;
                    break;
            }

            return outputFormat;
        }

        // Convert a RenderTexture to a Cubemap
        public static Cubemap RenderTextureToCubemap(RenderTexture input)
        {
            if (input.dimension != UnityEngine.Rendering.TextureDimension.Cube)
            {
                Debug.LogWarning("Input render texture dimension must be cube");
                return null;
            }

            if (input.width != input.height)
            {
                Debug.LogWarning("Input render texture must be square");
                return null;
            }

            var output = new Cubemap(input.width / 16, TextureFormat.RGBAFloat, false);
            var tmpFace = new Texture2D(input.width / 16, input.height / 16, output.format, false);

            var active = RenderTexture.active;

            for (var face = 0; face < 6; ++face)
            {
                Graphics.SetRenderTarget(input, 4, (CubemapFace)face);
                tmpFace.ReadPixels(new Rect(0, 0, input.width / 16, input.height / 16), 0, 0);
                output.SetPixels(tmpFace.GetPixels(), (CubemapFace)face, 0);

            }
            output.Apply();

            DestroyImmediate(tmpFace);
            RenderTexture.active = active;

            return output;
        }

        private IEnumerator BakeGradients_Internal()
        {
            // Simple coroutine to readback the texels of the skybox and assign them to the environment data
            yield return null;

            var environmentSystem = GetComponent<EnvironmentSystem>();
            var skyboxUpdater = GetComponent<SkyboxUpdater>();

            foreach (var profile in m_profilesToBake)
            {
                environmentSystem.SetProfile(profile);

                skyboxUpdater.UpdateSkyboxAndLighting(profile);

                yield return new WaitForSeconds(0.2f);

                var cubemapRT = m_probe.realtimeTexture;
                var width = cubemapRT.width;
                var height = cubemapRT.height;

                var textures = new Texture2D[6];
                for (var i = 0; i < 6; i++)
                {
                    Graphics.SetRenderTarget(cubemapRT, 0, (CubemapFace)i);
                    textures[i] = new Texture2D(width, height, TextureFormat.ARGB32, false);
                    textures[i].ReadPixels(new Rect(0, 0, width, height), 0, 0);
                }

                // Use the sky material ground color for this
                if (profile.SkyboxMaterial.HasColor("_Ground_Color"))
                {
                    profile.GradientAmbientSettings.GroundColor = profile.SkyboxMaterial.GetColor("_Ground_Color");
                    GroundColor = profile.GradientAmbientSettings.GroundColor;
                }

                var skyColor = Color.black;
                var count = 0;

                for (var x = 0; x < width; x++)
                {
                    for (var y = 0; y < height; y++)
                    {
                        skyColor += textures[(int)CubemapFace.PositiveY].GetPixel(x, y);
                        count++;
                    }
                }

                skyColor /= count;

                SkyColor = skyColor;
                profile.GradientAmbientSettings.SkyColor = skyColor;

                var equatorColor = Color.black;
                count = 0;
                for (var x = 0; x < width; x++)
                {
                    for (var y = height / 2 + 1; y < height / 2 + height / 16; y++)
                    {
                        equatorColor += textures[(int)CubemapFace.PositiveX].GetPixel(x, y);
                        equatorColor += textures[(int)CubemapFace.NegativeX].GetPixel(x, y);
                        equatorColor += textures[(int)CubemapFace.PositiveZ].GetPixel(x, y);
                        equatorColor += textures[(int)CubemapFace.NegativeZ].GetPixel(x, y);
                        count += 4;
                    }
                }
                equatorColor /= count;
                EquatorColor = equatorColor;

                profile.GradientAmbientSettings.EquatorColor = equatorColor;

                const int KCUBESIZE = 8 * 8;
                const int KENVIRONMENTDATASIZE = KCUBESIZE * 6 * 4;

                var cubemap = RenderTextureToCubemap(cubemapRT);
                var envData = new float[KENVIRONMENTDATASIZE];

                for (var c = 0; c < 6; ++c) // cube has 6 sides.
                {
                    for (var i = 0; i < KCUBESIZE * 4; i += 4)
                    {
                        var index = c * KCUBESIZE * 4;

                        var color = cubemap.GetPixel((CubemapFace)c, i % 8, i / 8, 0);

                        // Fill with default values.
                        envData[index + i + 0] = color.r;
                        envData[index + i + 1] = color.g;
                        envData[index + i + 2] = color.b;
                        envData[index + i + 3] = color.a;
                    }
                }

                profile.EnvironmentData = envData;

                DynamicGI.UpdateEnvironment();

                yield return new WaitForSeconds(1);

                profile.AmbientProbe = RenderSettings.ambientProbe;

#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(profile);
#endif

                yield return new WaitForSeconds(0.2f);
            }
        }
    }
}
