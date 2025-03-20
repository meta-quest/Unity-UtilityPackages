// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEngine;

namespace Meta.Utilities.Environment
{
    public class ClothAnimator : MonoBehaviour
    {
        public Cloth Cloth;
        public SkinnedMeshRenderer ClothMesh;
        public float ClothFadeTime = 1f;
        public float FurlSpeed = 10f;
        public float FurlInput = 0;

        private float m_windCutoff = 2f;
        private float m_clothBlendValue = 0;

        private void Update()
        {
            var furlDirection = FurlInput - Input.GetAxis("Vertical");
            // print(furlDirection);
            if (m_clothBlendValue is >= 0 and <= 100)
            {
                m_clothBlendValue += furlDirection * FurlSpeed * Time.deltaTime;
                m_clothBlendValue = Mathf.Clamp(m_clothBlendValue, 0, 100);
                ClothMesh.SetBlendShapeWeight(0, m_clothBlendValue);
            }

            var blendProgress = m_clothBlendValue * 0.01f;
            GetComponent<ClothWind>().WindFactor = Mathf.Clamp01(1 - blendProgress * m_windCutoff);
        }
    }
}