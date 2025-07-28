// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.Tutorial.Framework.Hub.Utilities;
using UnityEditor;
using UnityEngine;

#if META_EDIT_TUTORIALS
using System.Linq;
using Meta.Tutorial.Framework.Hub.Pages.Markdown;
#endif

namespace Meta.Tutorial.Framework.Hub.Contexts
{

    [CustomEditor(typeof(TutorialMarkdownContext))]
    public class TutorialMarkdownContextEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var context = target as TutorialMarkdownContext;
#if META_EDIT_TUTORIALS
            base.OnInspectorGUI();

            EditorGUILayout.Space();
            if (GUILayout.Button(new GUIContent("Reload Page",
                    "Reload the content of the page after modifying the .md file")))
            {
                context.ReloadPage();
            }

            // layout the buttons horizontally
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Toggle in context"))
            {
                context.ToggleAllPageConfigsToAppearInContext();
                EditorUtility.SetDirty(context);
                AssetDatabase.SaveAssetIfDirty(context);
            }
            if (GUILayout.Button("Toggle as children"))
            {
                context.ToggleAllPageConfigsToAppearAsChildren();
                EditorUtility.SetDirty(context);
                AssetDatabase.SaveAssetIfDirty(context);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            if (GUILayout.Button("Generate"))
            {
                // Remove all previous pages related to this context
                var allPages = Resources.FindObjectsOfTypeAll(typeof(MetaHubMarkdownPage));
                var pagesForContext = allPages.Where(page => (page as MetaHubMarkdownPage)?.ContextRef == context);
                foreach (var page in pagesForContext)
                {
                    var path = AssetDatabase.GetAssetPath(page);
                    if (!string.IsNullOrEmpty(path))
                    {
                        AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(page));
                    }
                }
                context.CreatePageReferences(true);
            }
            EditorGUILayout.Space();
#endif
            if (GUILayout.Button("Open Tutorial Hub"))
            {
                Telemetry.OnOpenTutorialButton(context.TelemetryContext, context.ProjectName, context.Title);
                context.ShowDefaultWindow();
            }
            EditorGUILayout.Space();
        }
    }
}