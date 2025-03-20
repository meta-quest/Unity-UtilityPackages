// Copyright (c) Meta Platforms, Inc. and affiliates.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Custom editor for the rain data, handles updating shader properties setting materials as dirty when changed
    /// </summary>
    [CustomEditor(typeof(RainData))]
    public class RainDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            _ = DrawDefaultInspector();

            if (EditorGUI.EndChangeCheck())
            {
                var settings = (RainData)target;
                settings.UpdateShaderProperties();

                // Ensure the scene updates
                if (!EditorApplication.isPlaying)
                {
                    EditorUtility.SetDirty(target);
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                }
            }

            if (GUILayout.Button("Force Update Materials"))
            {
                var settings = (RainData)target;
                settings.UpdateShaderProperties();

                // Force update all materials
                foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
                {
                    EditorUtility.SetDirty(material);
                }

                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }
    }
#endif
}