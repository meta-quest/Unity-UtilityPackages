// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.Tutorial.Framework.Hub.Contexts;
using Meta.Tutorial.Framework.Hub.Interfaces;
using UnityEngine;

namespace Meta.Tutorial.Framework.Hub.Pages.Markdown
{
    public class MetaHubFeedbackPage : ScriptableObject, IPageInfo
    {
        private TutorialFeedbackContext m_context;
        public string Name => m_context.Title;
        public string Context => m_context.Name;
        public int Priority => m_context.Priority;
        public string HierarchyName => Name;
        public string ProjectName => m_context?.ProjectName;

        public TutorialFeedbackContext FeedbackContext => m_context;

        public void OverrideContext(TutorialFeedbackContext feedbackContext)
        {
            m_context = feedbackContext;
        }
    }
}