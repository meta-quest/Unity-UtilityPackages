// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.Tutorial.Framework.Hub.Interfaces;
using Meta.Tutorial.Framework.Hub.Pages.Markdown;
using Meta.Tutorial.Framework.Hub.UIComponents;
using Meta.Tutorial.Framework.Hub.Utilities;
using UnityEditor;
using UnityEngine;

namespace Meta.Tutorial.Framework.Hub.Pages
{
    [CustomEditor(typeof(MetaHubFeedbackPage))]
    public class MetaHubFeedbackPageEditor : Editor, IOverrideSize
    {
        /// <inheritdoc cref="IOverrideSize.OverrideWidth"/>
        public float OverrideWidth { get; set; } = -1;

        /// <inheritdoc cref="IOverrideSize.OverrideHeight"/>
        public float OverrideHeight { get; set; } = -1;

        private Vector2 m_scrollPos = Vector2.zero;

        private GUIStyle m_textStyle;

        private bool m_isInitialized = false;
        private GUIStyle TextStyle
            => m_textStyle ??= new GUIStyle(Styles.DefaultTextStyle)
            {
                padding = new RectOffset(20, 20, 0, 0),
            };

        private GUIStyle TitleStyle
        {
            get
            {
                var style = new GUIStyle(TextStyle)
                {
                    fontSize = MarkdownUtils.HeaderSize(0),
                };
                style.padding.bottom = 10;
                return style;
            }
        }

        private GUIStyle HeaderStyle
        {
            get
            {
                var style = new GUIStyle(TitleStyle)
                {
                    fontSize = MarkdownUtils.HeaderSize(1),
                };
                return style;
            }
        }

        private GUIStyle m_buttonStyle;
        private GUIStyle ButtonStyle
                => m_buttonStyle ??= new GUIStyle(GUI.skin.button)
                {
                    fontSize = 14,
                    padding = new RectOffset(10, 10, 5, 5),
                };

        private MetaHubFeedbackPage m_metaHubFeedbackPage;
        private MetaHubFeedbackPage Target
            => m_metaHubFeedbackPage ??= target as MetaHubFeedbackPage;

        public override void OnInspectorGUI()
        {
            Initialize();
            m_scrollPos = GUILayout.BeginScrollView(m_scrollPos);

            EditorGUILayout.LabelField("FEEDBACK", TitleStyle);
            EditorGUILayout.LabelField(
                "To help us make our Showcases and Samples better and better serve you, your feedback is very welcome!",
                TextStyle);

            if (!string.IsNullOrEmpty(Target.FeedbackContext.GitHubUrl))
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.LabelField("GitHub", HeaderStyle);
                EditorGUILayout.LabelField(
                    "If you encountered any issues or have any questions you can reach us on GitHub.",
                    TextStyle);
                GUILayout.BeginHorizontal(TextStyle);
                var gitHubButtonContent = new GUIContent("Open GitHub Issue");
                var gitHubBtnSize = ButtonStyle.CalcSize(gitHubButtonContent);
                if (GUILayout.Button(gitHubButtonContent, ButtonStyle, GUILayout.Width(gitHubBtnSize.x), GUILayout.Height(gitHubBtnSize.y)))
                {
                    Telemetry.OnFeedbackClicked(Target.ProjectName, "GitHub");
                    Application.OpenURL(Target.FeedbackContext.GitHubUrl + "/issues/new");
                }
                GUILayout.EndHorizontal();
                EditorGUILayout.Space(20);
            }

            if (Target.FeedbackContext.ShowMQDHLink)
            {
                EditorGUILayout.LabelField("Other Feedback", HeaderStyle);
                EditorGUILayout.LabelField(
                    "You can also provide feedback on topics such as friction points, potential improvements, or ideas for samples through the Meta Quest Developer Hub (MQDH).",
                    TextStyle);
                GUILayout.BeginHorizontal(TextStyle);
                var mqdhBtnContent = new GUIContent("Send Feedback on MQDH");
                var mqdhBtnSize = ButtonStyle.CalcSize(mqdhBtnContent);
                if (GUILayout.Button(
                        mqdhBtnContent, ButtonStyle, GUILayout.Width(mqdhBtnSize.x), GUILayout.Height(mqdhBtnSize.y)))
                {
                    Telemetry.OnFeedbackClicked(Target.ProjectName, "MQDH");
                    MQDHFeedbackUtils.SubmitFeedback();
                }

                GUILayout.EndHorizontal();
                EditorGUILayout.Space(20);
            }


            GUILayout.EndScrollView();
        }

        private void Initialize()
        {
            if (m_isInitialized)
            {
                return;
            }

            _ = (MetaHubFeedbackPage)target;
            // Telemetry.OnPageLoaded(page.TelemetryContext, page);
            m_isInitialized = true;
        }
    }
}