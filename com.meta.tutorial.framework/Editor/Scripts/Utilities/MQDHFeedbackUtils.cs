// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace Meta.Tutorial.Framework.Hub.Utilities
{
    public static class MQDHFeedbackUtils
    {
        private const string MQDH_URL = "odh://feedback-hub";
        private const string SHOW_FEEDBACK_FORM = "showSubmitFeedback";
        private const string PLATFORM_ID_KEY = "platformID";
        private const string PLATFORM_ID = "1249239062924997"; // Unity
        private const string CATEGORY_ID_KEY = "categoryID";
        private const string SAMPLES_SHOWCASES_CATEGORY_ID = "28860876916836882";

        private static string GetMqdhDeeplink(string toolCategory)
        {
            var parameters = new List<string>()
            {
                SHOW_FEEDBACK_FORM,
                PLATFORM_ID_KEY + "=" + PLATFORM_ID,
            };

            if (!string.IsNullOrEmpty(toolCategory))
            {
                parameters.Add(CATEGORY_ID_KEY + "=" + toolCategory);
            }

            return MQDH_URL + "?" + string.Join("&", parameters);
        }

        public static void SubmitFeedback()
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = GetMqdhDeeplink(SAMPLES_SHOWCASES_CATEGORY_ID);
                process.StartInfo.UseShellExecute = true;
                _ = process.Start();
            }
            catch (Win32Exception)
            {
                if (EditorUtility.DisplayDialog("Install Meta Quest Developer Hub",
                        "Meta Quest Developer Hub is not installed on this machine.", "Get Meta Quest Developer Hub", "Cancel"))
                {
                    Application.OpenURL(
                        "https://developers.meta.com/horizon/documentation/unity/ts-odh-getting-started/");
                }
            }
        }
    }
}