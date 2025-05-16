// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEngine;

namespace Meta.Utilities.Environment
{
    public class ClothWind : MonoBehaviour
    {
        public Cloth[] ClothObjects;
        public float BaseStrength = 5;
        public float TurbulenceAmount = 0;
        public float WindFactor = 0;//modify base wind by value 0-1

        private void FixedUpdate()
        {
            foreach (var c in ClothObjects)
            {
                c.randomAcceleration = transform.right.normalized * -TurbulenceAmount;
                c.externalAcceleration = transform.right.normalized * -(BaseStrength * WindFactor);
            }
        }

    }
}