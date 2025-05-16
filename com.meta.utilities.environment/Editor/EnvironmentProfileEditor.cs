// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEditor;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Tracks inspector changes to the EnvironmentProfile. Any changes will increase the "version" which is 
    /// tracked by runtime components to recalculate values when the environment profile changes.
    /// </summary>
    [CustomEditor(typeof(EnvironmentProfile))]
    public class EnvironmentProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (var changed = new EditorGUI.ChangeCheckScope())
            {
                base.OnInspectorGUI();

                if (changed.changed)
                {
                    var environmentProfile = target as EnvironmentProfile;
                    environmentProfile.Version++;
                }
            }

            _ = serializedObject.ApplyModifiedProperties();
        }
    }
}