// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEditor;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Custom editor for the UnderwaterEnvironmentController to group and display settings in a more user-friendly way
    /// </summary>
    [CustomEditor(typeof(UnderwaterEnvironmentController))]
    public class UnderwaterEnvironmentControllerEditor : UnityEditor.Editor
    {
        private SerializedObject m_settingsObject;
        private bool m_showGlobalSettings = true;
        private bool m_showCausticParameters = true;
        private bool m_showDistortionParameters = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            _ = EditorGUILayout.PropertyField(serializedObject.FindProperty("m_parameters"));

            var controller = (UnderwaterEnvironmentController)target;
            var settings = controller.Parameters;

            if (settings != null)
            {
                EditorGUI.BeginChangeCheck();

                m_settingsObject = new SerializedObject(settings);
                m_settingsObject.Update();

                EditorGUILayout.Space(10);

                // Global Settings Section
                m_showGlobalSettings = EditorGUILayout.Foldout(m_showGlobalSettings, "Global Settings", true);
                if (m_showGlobalSettings)
                {
                    EditorGUI.indentLevel++;
                    _ = EditorGUILayout.PropertyField(m_settingsObject.FindProperty("useUnderwaterFog"));
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(5);

                // Base Caustic Parameters Section
                m_showCausticParameters = EditorGUILayout.Foldout(m_showCausticParameters, "Base Caustic Properties", true);
                if (m_showCausticParameters)
                {
                    EditorGUI.indentLevel++;
                    _ = EditorGUILayout.PropertyField(m_settingsObject.FindProperty("causticScale"));
                    _ = EditorGUILayout.PropertyField(m_settingsObject.FindProperty("causticSpeed"));
                    _ = EditorGUILayout.PropertyField(m_settingsObject.FindProperty("causticTimeModulation"));
                    _ = EditorGUILayout.PropertyField(m_settingsObject.FindProperty("causticEmissiveIntensity"));
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(5);

                // Distortion Parameters Section
                m_showDistortionParameters = EditorGUILayout.Foldout(m_showDistortionParameters, "Caustic Distortion", true);
                if (m_showDistortionParameters)
                {
                    EditorGUI.indentLevel++;
                    _ = EditorGUILayout.PropertyField(m_settingsObject.FindProperty("distortionIntensity"));
                    _ = EditorGUILayout.PropertyField(m_settingsObject.FindProperty("distortionScale"));
                    _ = EditorGUILayout.PropertyField(m_settingsObject.FindProperty("distortionSpeed"));
                    EditorGUI.indentLevel--;
                }

                if (EditorGUI.EndChangeCheck())
                {
                    _ = m_settingsObject.ApplyModifiedProperties();
                    controller.UpdateCausticParameters();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Assign an Underwater Environment Settings asset to edit parameters.", MessageType.Info);
            }

            _ = serializedObject.ApplyModifiedProperties();
        }
    }
}
