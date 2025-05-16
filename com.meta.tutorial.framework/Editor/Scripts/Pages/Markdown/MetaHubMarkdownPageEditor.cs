// Copyright (c) Meta Platforms, Inc. and affiliates.

#define SEGMENTS_MD

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Meta.Tutorial.Framework.Hub.Interfaces;
using Meta.Tutorial.Framework.Hub.Pages.Images;
using Meta.Tutorial.Framework.Hub.Parsing;
using Meta.Tutorial.Framework.Hub.UIComponents;
using Meta.Tutorial.Framework.Hub.Utilities;
using Meta.Tutorial.Framework.Hub.Windows;
using UnityEditor;
using UnityEngine;

namespace Meta.Tutorial.Framework.Hub.Pages.Markdown
{
    /// <summary>
    /// Custom editor for <see cref="MetaHubMarkdownPage"/>.
    /// </summary>
    [CustomEditor(typeof(MetaHubMarkdownPage))]
    public class MetaHubMarkdownPageEditor : Editor, IOverrideSize, IWindowUpdater
    {
        private enum ContentType
        {
            Block,
            List,
        }

        private GUIStyle m_linkStyle;
        private GUIStyle m_normalTextStyle;
        private GUIStyle m_imageLabelStyle;
        private GUIStyle m_listTextStyle;
        private static Dictionary<string, EmbeddedImage> s_cachedImages = new();

#if META_EDIT_TUTORIALS
        [MenuItem("Meta/Tutorial Hub/Edit Tutorials/Clear Image Cache", priority = 2)]
#endif
        public static void ClearCache()
        {
            s_cachedImages.Clear();
        }

        private Vector2 m_scrollView;

        /// <inheritdoc cref="IOverrideSize.OverrideWidth"/>
        public float OverrideWidth { get; set; } = -1;

        /// <inheritdoc cref="IOverrideSize.OverrideHeight"/>
        public float OverrideHeight { get; set; } = -1;

        // private readonly Regex m_imageRegex = new(@"!\[(.*?)\]\((.*?)\)", RegexOptions.Compiled);
        // private readonly Regex m_splitRegex = new(@"(!\[.*?\]\(.*?\))|(https?://[^\s]+)", RegexOptions.Compiled);
        private readonly Regex m_orderedContent = new(@"^(\s*)(?:(\d+(?:\.\d*)+)|[-•\*]) (.*)", RegexOptions.Compiled | RegexOptions.Multiline);
        private readonly Regex m_orderedCount = new(@"(\d)+", RegexOptions.Compiled | RegexOptions.Multiline);
        private readonly Regex m_quotedBlockRegex = new(@"^(\s*)>\s*(.*)", RegexOptions.Compiled | RegexOptions.Multiline);
        private readonly Regex m_indentationRegex = new(@"^(\s*)(.*)", RegexOptions.Compiled | RegexOptions.Multiline);

        private const float PADDING = Styles.Markdown.PADDING;

        private float RenderedWindowWidth => (OverrideWidth > 0 ? OverrideWidth : EditorGUIUtility.currentViewWidth) - PADDING;

        private List<Action> m_drawingCallbacks = new();
        private bool m_repaint;
        private bool m_isFirstText;
        private Texture2D m_emptyTexture;
        private Dictionary<string, EmbeddedImage> m_embeddedImages = new();
        private bool m_registeredToUpdate = false;

        /// <summary>
        /// The Parent window using this page.
        /// </summary>
        protected EditorWindow ParentWindow { get; private set; }

        private Texture2D EmptyTexture
        {
            get
            {
                if (m_emptyTexture == null)
                {
                    m_emptyTexture = new Texture2D(8, 8);
                }
                return m_emptyTexture;
            }
        }

        /// <inheritdoc cref="Editor.OnInspectorGUI()"/>
        public override void OnInspectorGUI()
        {
            if (m_drawingCallbacks.Count == 0)
            {
                Initialize();

                if (m_drawingCallbacks.Count == 0)
                {
                    base.OnInspectorGUI();
                }
            }

            for (var i = 0; i < m_drawingCallbacks.Count; i++)
            {
                m_drawingCallbacks[i].Invoke();
            }

            if (m_repaint)
            {
                Refresh();
            }
        }

        public bool Update()
        {
            var updated = false;
            foreach (var embeddedImage in m_embeddedImages)
            {
                if (embeddedImage.Value.Update())
                {
                    updated = true;
                }
            }

            return updated;
        }

        private void OnEditorUpdate()
        {
            if (Update())
            {
                Repaint();
                if (ParentWindow != null)
                {
                    ParentWindow.Repaint();
                }
            }
        }

        private void OnEnable()
        {
            if (!m_registeredToUpdate)
            {
                EditorApplication.update += OnEditorUpdate;
                m_registeredToUpdate = true;
            }
        }

        private void OnDisable()
        {
            if (m_registeredToUpdate)
            {
                EditorApplication.update -= OnEditorUpdate;
                m_registeredToUpdate = false;
            }
        }

        /// <summary>
        /// Initializes the Editor.
        /// </summary>
        protected void Initialize()
        {
            m_repaint = false;
            m_isFirstText = true;
            m_drawingCallbacks.Clear();
            m_embeddedImages.Clear();

            var markdownPage = (MetaHubMarkdownPage)target;
            if (!markdownPage)
            {
                return;
            }

            Telemetry.OnPageLoaded(markdownPage.TelemetryContext, markdownPage);
            var text = markdownPage.MarkdownText;
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            m_linkStyle ??= Styles.Markdown.Hyperlink.Style;

            m_normalTextStyle ??= Styles.Markdown.Text.Style;

            m_listTextStyle ??= new GUIStyle(m_normalTextStyle);
            m_listTextStyle.padding.left = 0;

            var currentEvent = Event.current;

            Draw(() =>
            {
                m_scrollView = GUILayout.BeginScrollView(m_scrollView);
                GUILayout.BeginVertical(GUILayout.Width(RenderedWindowWidth));
            });

            #region Render Markdown as segments
            // now render segments, just as we did for the markdown text parts
            var segments = (target as MetaHubMarkdownPage).TryParsedMarkdown;
            var prevIsImage = false;
            var imageSectionWidth = 0f;
            foreach (var segment in segments)
            {
                if (segment is ParsedMD.HyperlinkSegment hyperlink)
                {
                    if (hyperlink.IsImage)
                    {
                        var imagePath = hyperlink.URL;
                        if (!s_cachedImages.ContainsKey(imagePath))
                        {
                            // the image path is always relative to the root of the project
                            var embeddedImage = new EmbeddedImage(imagePath);
                            s_cachedImages[imagePath] = embeddedImage;
                            embeddedImage.LoadImage();
                            DelayedRefresh();
                        }

                        m_embeddedImages[imagePath] = s_cachedImages[imagePath];

                        if (!prevIsImage)
                        {
                            Draw(() =>
                            {
                                _ = EditorGUILayout.BeginHorizontal();
                                imageSectionWidth = 0;
                            });
                        }

                        Draw(() =>
                        {
                            Texture2D img = null;
                            float width = 100;
                            float height = 100;
                            var showLoading = true;
                            if (s_cachedImages.TryGetValue(imagePath, out var embeddedImage) && embeddedImage != null &&
                                embeddedImage.IsLoaded)
                            {
                                img = embeddedImage.CurrentTexture;
                                if (img == null)
                                {
                                    embeddedImage.LoadImage();
                                }
                                else
                                {
                                    showLoading = false;
                                    width = img.width;
                                    height = img.height;
                                }
                            }
                            if (showLoading || img == null)
                            {
                                img = EmptyTexture;
                                width = 100;
                                height = 100;
                            }

                            var aspectRatio = img.GetAspectRatio();
                            if (hyperlink.Properties != null)
                            {
                                if (hyperlink.Properties.TryGetValue("width", out var widthStr))
                                {
                                    if (widthStr.Contains('%'))
                                    {
                                        var pct = float.Parse(widthStr.Replace("%", ""));
                                        var maxWidth = RenderedWindowWidth * pct / 100;
                                        width = showLoading ? maxWidth : Mathf.Min(width, maxWidth);
                                    }
                                    else
                                    {
                                        width = float.Parse(widthStr);
                                    }

                                    height = width / aspectRatio;
                                }
                            }

                            if (width > RenderedWindowWidth - PADDING)
                            {
                                width = RenderedWindowWidth - PADDING;
                                height = width / aspectRatio;
                            }

                            if (null == m_imageLabelStyle)
                            {
                                m_imageLabelStyle = Styles.Markdown.ImageLabel.Style;
                            }

                            // If next image would go beyond screen we create a new row
                            if (imageSectionWidth + width >= RenderedWindowWidth - PADDING)
                            {
                                GUILayout.FlexibleSpace();
                                EditorGUILayout.EndHorizontal();
                                _ = EditorGUILayout.BeginHorizontal();
                                imageSectionWidth = 0;
                            }

                            var content = new GUIContent(img);
                            var imageLabelRect = GUILayoutUtility.GetRect(content, m_imageLabelStyle,
                                GUILayout.Height(height), GUILayout.Width(width));

                            if (GUI.Button(imageLabelRect, content, m_imageLabelStyle))
                            {
                                ImageViewer.ShowWindow(embeddedImage, Path.GetFileNameWithoutExtension(imagePath));
                                Telemetry.OnImageClicked(markdownPage.TelemetryContext, markdownPage, img.name);
                            }

                            imageSectionWidth += width;
                        });
                        prevIsImage = true;
                    }
                    else // blue text hyperlink. TODO: distinguish web links from source code links
                    {
                        if (prevIsImage)
                        {
                            Draw(() =>
                            {
                                GUILayout.FlexibleSpace();
                                EditorGUILayout.EndHorizontal();
                            });
                            prevIsImage = false;
                        }
                        var url = hyperlink.URL;
                        Draw(() =>
                        {
                            _ = EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(EditorGUI.indentLevel * Styles.Markdown.INDENT_SPACE);
                            GUILayout.Label($"<color={Styles.DefaultLinkColor}>" + hyperlink.Text + "</color>", m_linkStyle,
                                                               GUILayout.MaxWidth(RenderedWindowWidth));
                            var linkRect = GUILayoutUtility.GetLastRect();
                            if (currentEvent.type == EventType.MouseDown && linkRect.Contains(currentEvent.mousePosition))
                            {
                                if (url.StartsWith('.')) // this is a local file
                                {
                                    // if it's a source code file, open it in the editor
                                    if (url.EndsWith(".cs"))
                                    {
                                        MarkdownUtils.NavigateToSourceFile(url);
                                    }
                                }
                                Application.OpenURL(url);
                            }
                            EditorGUILayout.EndHorizontal();
                        });
                    }
                }
                else
                {
                    if (prevIsImage && !string.IsNullOrEmpty(segment.Text.Trim()))
                    {
                        Draw(() =>
                        {
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.EndHorizontal();
                        });
                        prevIsImage = false;
                    }
                    ProcessSegment(segment.Text);
                }
            }
            #endregion

            // if the last element is an image we need to end the layout
            if (prevIsImage)
            {
                Draw(() =>
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                });
                prevIsImage = false;
            }

            Draw(() =>
            {
                GUILayout.Space(Styles.Markdown.PADDING);
                GUILayout.EndVertical();
                GUILayout.EndScrollView();
            });
        }

        /// <summary>
        /// Draws the given action.
        /// </summary>
        /// <param name="action">The action to draw.</param>
        private void Draw(Action action)
        {
            m_drawingCallbacks.Add(action);
        }

        /// <summary>
        /// Draws the given text.
        /// </summary>
        /// <param name="text">The text to draw.</param>
        /// <param name="styleOverride">Override the style of the Text Area</param>
        /// <param name="collapseParagraphs">Collapse line break to remove paragraph breaks</param>
        private void DrawText(string text, GUIStyle styleOverride = null, bool collapseParagraphs = false)
        {
            var parsedText = MarkdownUtils.ParseMarkdown(text);
            // If we render the beginning of the file we want to remove the new lines before the header
            if (m_isFirstText)
            {
                m_isFirstText = false;
                parsedText = parsedText.TrimStart();
            }
            if (collapseParagraphs)
            {
                parsedText = Regex.Replace(parsedText, @"\n\n", "\n");
            }
            Draw(() =>
            {
                _ = EditorGUILayout.TextArea(
                    parsedText,
                    styleOverride ?? m_normalTextStyle,
                    GUILayout.MaxWidth(RenderedWindowWidth)
                );
            });
        }

        /// <summary>
        /// Add Draw function to start a Quoted Block
        /// </summary>
        private void DrawQuotedBlockStart()
        {
            Draw(() =>
            {
                GUILayout.Space(Styles.Markdown.SPACING);
                GUILayout.BeginHorizontal(GUILayout.MaxWidth(RenderedWindowWidth));
                GUILayout.Space(Styles.Markdown.SPACING);
                GUILayout.Box("", Styles.Markdown.QuotedBoxIndicator.Style, GUILayout.Width(5), GUILayout.ExpandHeight(true));
                GUILayout.BeginVertical();
                GUILayout.BeginVertical(Styles.Markdown.Box.Style);
                GUILayout.Space(Styles.Markdown.BOX_SPACING);
            });
        }

        /// <summary>
        /// Add Draw function to end a Quoted Block
        /// </summary>
        private void DrawQuotedBlockEnd()
        {
            Draw(() =>
            {
                GUILayout.Space(Styles.Markdown.BOX_SPACING);
                GUILayout.EndVertical();
                GUILayout.EndVertical();
                GUILayout.Space(Styles.Markdown.SPACING);
                GUILayout.EndHorizontal();
                GUILayout.Space(Styles.Markdown.SPACING);
            });
        }

        /// <summary>
        /// Add Draw function to start a List
        /// </summary>
        private void DrawStartList()
        {
            Draw(() =>
            {
                GUILayout.Space(Styles.Markdown.LIST_SPACING * 4);
            });
        }

        /// <summary>
        /// Add Draw function to end a List
        /// </summary>
        private void DrawEndList(string nextText)
        {
            // Only add spacing if the next is not a header
            if (nextText == null || !nextText.StartsWith("#"))
            {
                // add extra line spacing at the end of the list
                Draw(() =>
                {
                    GUILayout.Space(Styles.Markdown.LIST_SPACING * 4);
                });
            }
        }

        /// <summary>
        /// Add Draw function to start an item in a list
        /// </summary>
        private void DrawStartListItem(int indentation, string point)
        {
            Draw(() =>
            {
                GUILayout.Space(indentation > 0 ? Styles.Markdown.LIST_SPACING_HALF : Styles.Markdown.LIST_SPACING);
                GUILayout.BeginHorizontal(GUILayout.MaxWidth(RenderedWindowWidth));
                if (indentation > 0)
                {
                    GUILayout.Space(Styles.Markdown.INDENT_SPACE * indentation);
                }

                Styles.Markdown.Text.DrawHorizontalTextArea(point);
            });
        }

        /// <summary>
        /// Add Draw function to end an item in a list
        /// </summary>
        private void DrawEndListItem(int indentation)
        {
            Draw(() =>
            {
                GUILayout.Space(Styles.Markdown.SPACING);
                GUILayout.EndHorizontal();
                GUILayout.Space(indentation > 0 ? Styles.Markdown.LIST_SPACING_HALF : Styles.Markdown.LIST_SPACING);
            });
        }

        /// <summary>
        /// Process the segment text into draw calls.
        /// We process line by line to find the type of content of each line.
        /// </summary>
        /// <param name="input">Input text to process for the segment</param>
        private void ProcessSegment(string input)
        {
            var lines = input.Split("\n");
            StringBuilder sb = new();
            var quotedBlockLevel = 0;
            var listLevel = 0;
            // Process line by line
            foreach (var line in lines)
            {
                var processedLine = line;
                var newQuotedBlockLevel = 0;
                // First we check if it's in a quoted block
                if (TryMatch(processedLine, m_quotedBlockRegex, out var quotedMatch))
                {
                    processedLine = quotedMatch.Groups[2].Value.Trim();
                    newQuotedBlockLevel = 1;
                    while (TryMatch(processedLine, m_quotedBlockRegex, out var subQuotedMatch))
                    {
                        processedLine = subQuotedMatch.Groups[2].Value.Trim();
                        newQuotedBlockLevel++;
                    }
                }

                // Then we check if it's the start of a list item
                var newListLevel = 0;
                var startNewListItem = false;
                string listPrefix = null;
                if (TryMatch(processedLine, m_orderedContent, out var listMatch))
                {
                    var indentRaw = listMatch.Groups[1].Value;
                    listPrefix = listMatch.Groups[2].Value;
                    listPrefix = string.IsNullOrEmpty(listPrefix) ? "•" : listPrefix; // Normalize '-' and '*' to '•'
                    processedLine = listMatch.Groups[3].Value;

                    newListLevel = Mathf.FloorToInt(indentRaw.Length / 4f) + 1;
                    var matches = m_orderedCount.Matches(listPrefix);
                    if (matches.Count > 0)
                    {
                        newListLevel = Mathf.Max(matches.Count, newListLevel);
                    }
                    startNewListItem = true;
                }

                // If it's not the start of a list item we process the indentation
                // The indentation will give us information if the text belongs to the previous list item.
                if (!startNewListItem && TryMatch(processedLine, m_indentationRegex, out var indentMatch))
                {
                    var indentRaw = indentMatch.Groups[1].Value;
                    processedLine = indentMatch.Groups[2].Value;

                    var indentationLevel = Mathf.FloorToInt(indentRaw.Length / 4f);
                    // empty line are considered part of the previous list item
                    if (string.IsNullOrWhiteSpace(processedLine) || indentationLevel == listLevel)
                    {
                        newListLevel = listLevel;
                    }
                }

                var isNewQuotedBlockLevel = quotedBlockLevel != newQuotedBlockLevel;
                var change = isNewQuotedBlockLevel || listLevel != newListLevel || startNewListItem;
                if (change)
                {
                    // When we detect a change we Draw the content we cumulated so far.
                    DrawContent(sb.ToString(), listLevel > 0 ? m_listTextStyle : null);
                    _ = sb.Clear();
                }


                // Draw content end in order, if applicable
                // 1. List
                // 2. Quoted Block
                if (startNewListItem || newListLevel != listLevel)
                {
                    DrawContentEnd(ContentType.List, listLevel, isNewQuotedBlockLevel ? 0 : newListLevel, processedLine);
                }
                if (newQuotedBlockLevel != quotedBlockLevel)
                {
                    DrawContentEnd(ContentType.Block, quotedBlockLevel, newQuotedBlockLevel, processedLine);
                }

                // Draw content start in order, if applicable
                // 1. Quoted Block
                // 2. List
                if (newQuotedBlockLevel != quotedBlockLevel)
                {
                    if (newQuotedBlockLevel > 0)
                    {
                        DrawContentStart(processedLine, ContentType.Block, quotedBlockLevel, newQuotedBlockLevel);
                    }
                }
                if (startNewListItem || newListLevel != listLevel)
                {
                    DrawContentStart(listPrefix, ContentType.List, isNewQuotedBlockLevel ? 0 : listLevel, newListLevel);
                }

                // update the current levels
                listLevel = newListLevel;
                quotedBlockLevel = newQuotedBlockLevel;
                // append the processed line to be drawn in the correct time
                _ = sb.Append(processedLine);
                _ = sb.Append("\n");
            }

            // Final draw of leftover content
            if (sb.Length > 0)
            {
                DrawContent(sb.ToString(), listLevel > 0 ? m_listTextStyle : null);
            }

            // Finalize any open content in order, if applicable
            // 1. List
            // 2. Quoted Block
            if (listLevel > 0)
            {
                DrawContentEnd(ContentType.List, listLevel, 0);
            }
            if (quotedBlockLevel > 0)
            {
                DrawContentEnd(ContentType.Block, quotedBlockLevel, 0);
            }
        }

        /// <summary>
        /// Try to find a regex match in a string and return if found and output the match.
        /// This is great to use in if statements.
        /// </summary>
        /// <param name="input">Text to apply regex to</param>
        /// <param name="regex">The regex to use</param>
        /// <param name="match">Output match found</param>
        /// <returns>True if a match is found.</returns>
        private bool TryMatch(string input, Regex regex, out Match match)
        {
            match = regex.Match(input);
            return match.Success;
        }

        /// <summary>
        /// Draw the content of a section. Skip empty content.
        /// </summary>
        /// <param name="input">Text to draw</param>
        /// <param name="styleOverride">Style override</param>
        /// <param name="collapseParagraphs">If we collapse pragraph in one (for lists)</param>
        private void DrawContent(string input, GUIStyle styleOverride = null, bool collapseParagraphs = false)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return;
            }

            DrawText(input, styleOverride, collapseParagraphs);
        }

        /// <summary>
        /// Add draw calls for the start of a content type.
        /// </summary>
        /// <param name="startData">Data string used for the content start</param>
        /// <param name="type">The content type we are processing</param>
        /// <param name="prevLevel">The previous level of that content type</param>
        /// <param name="newLevel">The new level of that content type</param>
        /// <exception cref="ArgumentOutOfRangeException">Content type invalid</exception>
        private void DrawContentStart(string startData, ContentType type, int prevLevel, int newLevel)
        {
            switch (type)
            {
                case ContentType.Block:
                    {
                        for (var i = prevLevel; i < newLevel; ++i)
                        {
                            DrawQuotedBlockStart();
                        }
                    }
                    break;
                case ContentType.List:
                    {
                        if (newLevel > 0)
                        {
                            if (prevLevel == 0)
                            {
                                DrawStartList();
                            }
                            DrawStartListItem(newLevel - 1, startData);
                        }
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
        /// <summary>
        /// Add draw calls for the end of a content type.
        /// </summary>
        /// <param name="type">The content type we are processing</param>
        /// <param name="prevLevel">The previous level of that content type</param>
        /// <param name="newLevel">The new level of that content type</param>
        /// <exception cref="ArgumentOutOfRangeException">Content type invalid</exception>
        private void DrawContentEnd(ContentType type, int prevLevel, int newLevel, string followingText = null)
        {
            switch (type)
            {
                case ContentType.Block:
                    {
                        for (var i = newLevel; i < prevLevel; ++i)
                        {
                            DrawQuotedBlockEnd();
                        }
                    }
                    break;
                case ContentType.List:
                    {
                        if (prevLevel > 0)
                        {
                            DrawEndListItem(prevLevel - 1);
                            if (newLevel == 0)
                            {
                                DrawEndList(followingText);
                            }
                        }
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>
        /// Refreshes the editor on the next repaint.
        /// </summary>
        private void DelayedRefresh()
        {
            m_repaint = true;
        }

        /// <summary>
        /// Refreshes and repaints the editor.
        /// </summary>
        private void Refresh()
        {
            m_repaint = false;
            Repaint();
        }

        /// <inheritdoc cref="IMetaHubPage.RegisterWindow"/>
        public void RegisterWindow(EditorWindow window)
        {
            ParentWindow = window;
        }

        /// <inheritdoc cref="IMetaHubPage.UnregisterWindow"/>
        public void UnregisterWindow(EditorWindow window)
        {
            if (window == ParentWindow)
            {
                ParentWindow = null;
            }
        }
    }
}