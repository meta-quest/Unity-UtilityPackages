// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEditor;
using UnityEngine;

namespace Meta.Utilities.Environment
{
    [CustomEditor(typeof(LightProbeSettingsAdjustment))]
    public class LightProbeSettingsAdjustmentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            _ = DrawDefaultInspector();
            var settingsAdjustment = (LightProbeSettingsAdjustment)target;
            if (GUILayout.Button("Populate Child Renderers"))
            {
                settingsAdjustment.GetChildRenderers();
                EditorUtility.SetDirty(settingsAdjustment);
            }
        }
    }
}
