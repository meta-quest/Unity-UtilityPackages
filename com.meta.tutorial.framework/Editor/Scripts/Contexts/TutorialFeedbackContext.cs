// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.Tutorial.Framework.Hub.Attributes;
using Meta.Tutorial.Framework.Hub.Pages;
using Meta.Tutorial.Framework.Hub.Pages.Markdown;
using Meta.Tutorial.Framework.Windows;

namespace Meta.Tutorial.Framework.Hub.Contexts
{
    [MetaHubContext(TutorialFrameworkHub.CONTEXT)]
#if META_EDIT_TUTORIALS
    [CreateAssetMenu(fileName = "TutorialFeedback", menuName = "Meta Tutorial Hub/Tutorial Feedback Context", order = 3)]
#endif
    public class TutorialFeedbackContext : BaseTutorialHubContext
    {
        public string GitHubUrl;
        public bool ShowMQDHLink = true;

        public override PageReference[] CreatePageReferences(bool forceCreate = false)
        {
            var feedbackPage = CreateInstance<MetaHubFeedbackPage>();
            feedbackPage.OverrideContext(this);
            var soPage = new ScriptableObjectPage(feedbackPage, TutorialName, Title, Priority, ShowBanner ? Banner : null);

            return new[]
            {
                new PageReference()
                {
                    Page = soPage,
                    Info = soPage
                }
            };
        }
    }
}