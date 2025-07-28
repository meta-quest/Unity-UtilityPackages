// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.Tutorial.Framework.Hub.Utilities;
using UnityEditor;
using UnityEngine;

namespace Meta.Tutorial.Framework.Hub.Contexts
{

    [CustomEditor(typeof(TutorialFeedbackContext))]
    public class TutorialFeedbackContextEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var context = target as TutorialFeedbackContext;
#if META_EDIT_TUTORIALS
            base.OnInspectorGUI();
            EditorGUILayout.Space();
#endif
            if (GUILayout.Button("Open Tutorial Hub"))
            {
                Telemetry.OnOpenTutorialButton(context.TelemetryContext, context.ProjectName, context.Title);
                context.ShowDefaultWindow();
            }
            EditorGUILayout.Space();

            var pages = context.CreatePageReferences();
            if (pages.Length > 0)
            {
                foreach (var page in pages)
                {
                    page.Page.OnGUI();
                }
            }
        }
    }
}