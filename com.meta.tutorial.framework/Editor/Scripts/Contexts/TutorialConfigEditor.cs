// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.Tutorial.Framework.Hub.Utilities;
using Meta.Tutorial.Framework.Windows;
using UnityEditor;
using UnityEngine;

namespace Meta.Tutorial.Framework.Hub.Contexts
{

    [CustomEditor(typeof(TutorialConfig))]
    public class TutorialConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var context = target as TutorialConfig;
#if META_EDIT_TUTORIALS
            base.OnInspectorGUI();
            EditorGUILayout.Space();

            if (GUILayout.Button("Generate All"))
            {
                context.GenerateAllSubContexts();
            }
#endif
            if (GUILayout.Button("Open Tutorial Hub"))
            {
                Telemetry.OnOpenTutorialButton(context.TelemetryContext, context.Name, context.name);
                TutorialFrameworkHub.ShowWindow();
            }
            EditorGUILayout.Space();
        }
    }
}