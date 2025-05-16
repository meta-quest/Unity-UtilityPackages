// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Renders the sun disk geometry and moon disk if enabled
    /// </summary>
    [ExecuteAlways]
    public class SunDiskRenderer : MonoBehaviour
    {
        [SerializeField] private Mesh m_mesh;
        [SerializeField] private float m_angularDiameterScale = 2f;

        private MaterialPropertyBlock m_propertyBlock;

        private void OnEnable()
        {
            m_propertyBlock = new();
        }

        public void BeginContextRendering(SunSettings settings, Camera camera)
        {
            // Render primary body (Always enabled, but could easily be controlled with a bool)
            if (settings.SunDiskMaterial != null) // Avoid rendering with a pink error shader
            {
                var forward = Quaternion.Euler(settings.Rotation) * -Vector3.forward;

                // Convert angular diameter to solid angle which is used as the final scale factor of the mesh
                var scale = 2.0f * Mathf.Tan(0.5f * settings.AngularDiameter * m_angularDiameterScale * Mathf.Deg2Rad);

                // Matrix is simply the camera position plus a normalized forward vector. Distance offset is done in shader
                var localToWorld = Matrix4x4.TRS(camera.transform.position + forward, Quaternion.LookRotation(forward), new Vector3(scale, scale, 1.0f));
                Graphics.DrawMesh(m_mesh, localToWorld, settings.SunDiskMaterial, 0, camera, 0, m_propertyBlock);
            }

            // Render secondary body if enabled
            if (settings.RenderSecondaryCelestialObject && settings.SecondaryCelestialObjectMaterial != null)  // Avoid rendering with a pink error shader
            {
                var forward = Quaternion.Euler(settings.SecondaryCelestialObjectRotation) * -Vector3.forward;

                // Convert angular diameter to solid angle which is used as the final scale factor of the mesh
                var scale = 2.0f * Mathf.Tan(0.5f * settings.SecondaryCelestialObjectAngularDiameter * Mathf.Deg2Rad);

                // Matrix is simply the camera position plus a normalized forward vector. Distance offset is done in shader
                var localToWorld = Matrix4x4.TRS(camera.transform.position + forward, Quaternion.LookRotation(forward), new Vector3(scale, scale, 1.0f));
                Graphics.DrawMesh(m_mesh, localToWorld, settings.SecondaryCelestialObjectMaterial, 0, camera, 0, m_propertyBlock);
            }
        }
    }
}
