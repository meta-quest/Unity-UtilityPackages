// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Stores variables related to the rain effect, and has a utility function to set them as global shader variables
    /// </summary>
    [CreateAssetMenu(fileName = "RainData", menuName = "Environment System Data/Rain Data")]
    public class RainData : ScriptableObject
    {
#pragma warning disable IDE1006 // ReSharper disable InconsistentNaming
        // Cached shader property IDs - removed texture properties
        private static readonly int s_rainWind_TilingId = Shader.PropertyToID("_RainWindTiling");
        private static readonly int s_puddleWetness_AmountId = Shader.PropertyToID("_PuddleWetnessAmount");
        private static readonly int s_windPuddle_DistortionId = Shader.PropertyToID("_WindPuddleDistortion");
        private static readonly int s_rain_TraisitionId = Shader.PropertyToID("_RainTraisition");
        private static readonly int s_puddle_TilingId = Shader.PropertyToID("_PuddleTiling");
        private static readonly int s_rainWindNormal_StrengthId = Shader.PropertyToID("_RainWindNormalStrength");
        private static readonly int s_rainWaveNormalwave_SplashsId = Shader.PropertyToID("_RainWaveNormalwaveSplashs");
        private static readonly int s_rainWind_DirectionId = Shader.PropertyToID("_RainWindDirection");
        private static readonly int s_rippleColor_StrengthId = Shader.PropertyToID("_RippleColorStrength");
        private static readonly int s_ripple_RoughnessId = Shader.PropertyToID("_RippleRoughness");
        private static readonly int s_rippleDistortion_IntensityId = Shader.PropertyToID("_RippleDistortionIntensity");
        private static readonly int s_ripplePosition_OffsetId = Shader.PropertyToID("_RipplePositionOffset");
        private static readonly int s_rippleScatter_Density_1Id = Shader.PropertyToID("_Ripple_Scatter_Density_1");
        private static readonly int s_rippleScatter_Density_2Id = Shader.PropertyToID("_Ripple_Scatter_Density_2");
        private static readonly int s_ripple_SpeedId = Shader.PropertyToID("_Ripple_Speed");
        private static readonly int s_generate_Ripples_Seeds_1Id = Shader.PropertyToID("_Generate_Ripples_Seeds_1");
        private static readonly int s_generate_Ripples_Seeds_2Id = Shader.PropertyToID("_Generate_Ripples_Seeds_2");
        private static readonly int s_rippleUV_OffsetId = Shader.PropertyToID("_RippleUVOffset");
        private static readonly int s_ripple_StrengthId = Shader.PropertyToID("_Ripple_Strength");
        private static readonly int s_ripple_Frequency_2Id = Shader.PropertyToID("_Ripple_Frequency_2");
        private static readonly int s_ripple_Frequency_1Id = Shader.PropertyToID("_Ripple_Frequency_1");
        private static readonly int s_ripple_ThicknessId = Shader.PropertyToID("_Ripple_Thickness");
        private static readonly int s_fade_MaskId = Shader.PropertyToID("_FadeMask");
        private static readonly int s_ripple_LookId = Shader.PropertyToID("_Ripple_Look");
        private static readonly int s_max_DistanceId = Shader.PropertyToID("_Max_Distance");
        private static readonly int s_minDistance_OffsetId = Shader.PropertyToID("_Min_Distance_Offset");
        private static readonly int s_powerId = Shader.PropertyToID("_Power");

        [Header("Rain Wind Settings")]
        [SerializeField]
        private Vector2 m_rainWind_Tiling = new(20, 20);

        [SerializeField] private float m_puddleWetness_Amount = 0f;
        [SerializeField] private float m_windPuddle_Distortion = 0f;
        [SerializeField] private float m_rain_Traisition = 0f;
        [SerializeField] private float m_puddle_Tiling = 1f;
        [SerializeField] private float m_rainWindNormal_Strength = 0f;
        [SerializeField] private Vector2 m_rainWaveNormalwave_Splashs = new(0, 0);
        [SerializeField] private Vector2 m_rainWind_Direction = new(0, 0);

        [Header("Rain Ripple Settings")]
        [SerializeField]
        private float m_rippleColor_Strength = 1.5f;

        [SerializeField] private float m_ripple_Roughness = 1f;
        [SerializeField] private float m_rippleDistortion_Intensity = 1f;
        [SerializeField] private float m_ripplePosition_Offset = -0.3f;
        [SerializeField] private float m_rippleScatter_Density_1 = 10f;
        [SerializeField] private float m_rippleScatter_Density_2 = 10f;
        [SerializeField] private float m_ripple_Speed = 10f;
        [SerializeField] private Vector4 m_generate_Ripples_Seeds_1 = new(8, 6, 61, 108);
        [SerializeField] private Vector4 m_generate_Ripples_Seeds_2 = new(8, 6, 61, 108);
        [SerializeField] private Vector2 m_rippleUV_Offset = new(0, 0);
        [SerializeField] private float m_ripple_Strength = 1f;
        [SerializeField] private float m_ripple_Frequency_2 = 10f;
        [SerializeField] private float m_ripple_Frequency_1 = 10f;
        [SerializeField] private float m_ripple_Thickness = 10f;
        [SerializeField] private float m_fade_Mask = 1f;
        [SerializeField] private Vector2 m_ripple_Look = new(-2.02f, 0.59f);

        [Header("Rain Fade Settings")]
        [SerializeField]
        private float m_max_Distance = 0f;

        [SerializeField] private float m_minDistance_Offset = 100f;
        [SerializeField] private float m_power = 1f;
#pragma warning restore IDE1006 // ReSharper restore InconsistentNaming

        private void OnEnable()
        {
            UpdateShaderProperties();
            _ = Shader.GetGlobalVector(s_rainWind_TilingId);
        }

        public void UpdateShaderProperties()
        {
            Shader.SetGlobalVector(s_rainWind_TilingId, m_rainWind_Tiling);
            Shader.SetGlobalFloat(s_puddleWetness_AmountId, m_puddleWetness_Amount);
            Shader.SetGlobalFloat(s_windPuddle_DistortionId, m_windPuddle_Distortion);
            Shader.SetGlobalFloat(s_rain_TraisitionId, m_rain_Traisition);
            Shader.SetGlobalFloat(s_puddle_TilingId, m_puddle_Tiling);
            Shader.SetGlobalFloat(s_rainWindNormal_StrengthId, m_rainWindNormal_Strength);
            Shader.SetGlobalVector(s_rainWaveNormalwave_SplashsId, m_rainWaveNormalwave_Splashs);
            Shader.SetGlobalVector(s_rainWind_DirectionId, m_rainWind_Direction);

            Shader.SetGlobalFloat(s_rippleColor_StrengthId, m_rippleColor_Strength);
            Shader.SetGlobalFloat(s_ripple_RoughnessId, m_ripple_Roughness);
            Shader.SetGlobalFloat(s_rippleDistortion_IntensityId, m_rippleDistortion_Intensity);
            Shader.SetGlobalFloat(s_ripplePosition_OffsetId, m_ripplePosition_Offset);
            Shader.SetGlobalFloat(s_rippleScatter_Density_1Id, m_rippleScatter_Density_1);
            Shader.SetGlobalFloat(s_rippleScatter_Density_2Id, m_rippleScatter_Density_2);
            Shader.SetGlobalFloat(s_ripple_SpeedId, m_ripple_Speed);
            Shader.SetGlobalVector(s_generate_Ripples_Seeds_1Id, m_generate_Ripples_Seeds_1);
            Shader.SetGlobalVector(s_generate_Ripples_Seeds_2Id, m_generate_Ripples_Seeds_2);
            Shader.SetGlobalVector(s_rippleUV_OffsetId, m_rippleUV_Offset);
            Shader.SetGlobalFloat(s_ripple_StrengthId, m_ripple_Strength);
            Shader.SetGlobalFloat(s_ripple_Frequency_2Id, m_ripple_Frequency_2);
            Shader.SetGlobalFloat(s_ripple_Frequency_1Id, m_ripple_Frequency_1);
            Shader.SetGlobalFloat(s_ripple_ThicknessId, m_ripple_Thickness);
            Shader.SetGlobalFloat(s_fade_MaskId, m_fade_Mask);
            Shader.SetGlobalVector(s_ripple_LookId, m_ripple_Look);

            Shader.SetGlobalFloat(s_max_DistanceId, m_max_Distance);
            Shader.SetGlobalFloat(s_minDistance_OffsetId, m_minDistance_Offset);
            Shader.SetGlobalFloat(s_powerId, m_power);
        }
    }
}