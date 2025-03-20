// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEditor;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Shows a material editor when the quadtree renderer component is visible
    /// </summary>
    [CustomEditor(typeof(QuadtreeRenderer))]
    public class QuadtreeRendererEditor : UnityEditor.Editor
    {
        private MaterialEditor m_materialEditor;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (var changed = new EditorGUI.ChangeCheckScope())
            {
                base.OnInspectorGUI();

                if (changed.changed)
                {
                    var environmentProfile = target as QuadtreeRenderer;
                    environmentProfile.Version++;
                }
            }

            _ = serializedObject.ApplyModifiedProperties();

            // Draw a material editor, like a regular mesh
            var materialProperty = serializedObject.FindProperty("<Material>k__BackingField");
            if (materialProperty.objectReferenceValue != null && m_materialEditor == null)
            {
                // Create a new instance of the default MaterialEditor
                m_materialEditor = (MaterialEditor)CreateEditor(materialProperty.objectReferenceValue);
            }

            if (m_materialEditor != null)
            {
                // Draw the material's foldout and the material shader field
                // Required to call _materialEditor.OnInspectorGUI ();
                m_materialEditor.DrawHeader();

                //  We need to prevent the user to edit Unity default materials
                var isDefaultMaterial = !AssetDatabase.GetAssetPath(materialProperty.objectReferenceValue).StartsWith("Assets");
                using (new EditorGUI.DisabledGroupScope(isDefaultMaterial))
                {
                    // Draw the material properties
                    // Works only if the foldout of _materialEditor.DrawHeader () is open
                    m_materialEditor.OnInspectorGUI();
                }
            }
        }
    }
}