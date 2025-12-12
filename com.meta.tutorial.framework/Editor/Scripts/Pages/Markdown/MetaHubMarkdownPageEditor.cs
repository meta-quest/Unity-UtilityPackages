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
            CodeBlock,
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
        private readonly Regex m_codeBlockRegex = new(@"^(\s*)```(.*)", RegexOptions.Compiled | RegexOptions.Multiline);
        private readonly Regex m_codeBlockInnerCodeRegex = new(@"^```.*?\n(.*?)^```", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);

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

        /// <summary>
        /// Result of preparing an image for display.
        /// </summary>
        private struct PreparedImage
        {
            public Texture2D Texture;
            public float Width;
            public float Height;
            public bool ShowLoading;
        }

        /// <summary>
        /// Prepares an image for display by loading it from cache or creating a new embedded image.
        /// Handles sizing based on hyperlink properties and max width constraints.
        /// </summary>
        /// <param name="imagePath">Path to the image file.</param>
        /// <param name="hyperlink">Hyperlink segment containing optional width/height properties.</param>
        /// <param name="maxWidth">Maximum width constraint for the image.</param>
        /// <param name="defaultSize">Default size when image is loading or unavailable.</param>
        /// <returns>A PreparedImage struct containing the texture and calculated dimensions.</returns>
        private PreparedImage PrepareImageForDisplay(
            string imagePath,
            ParsedMD.HyperlinkSegment hyperlink,
            float maxWidth,
            float defaultSize = 100f)
        {
            // Ensure image is cached and loading
            if (!s_cachedImages.ContainsKey(imagePath))
            {
                var embeddedImage = new EmbeddedImage(imagePath);
                s_cachedImages[imagePath] = embeddedImage;
                embeddedImage.LoadImage();
                DelayedRefresh();
            }

            m_embeddedImages[imagePath] = s_cachedImages[imagePath];

            // Get texture and dimensions
            Texture2D img = null;
            var width = defaultSize;
            var height = defaultSize;
            var showLoading = true;

            if (s_cachedImages.TryGetValue(imagePath, out var embeddedImage2) &&
                embeddedImage2 != null &&
                embeddedImage2.IsLoaded)
            {
                img = embeddedImage2.CurrentTexture;
                if (img == null)
                {
                    embeddedImage2.LoadImage();
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
                width = defaultSize;
                height = defaultSize;
            }

            // Apply sizing from hyperlink properties
            var aspectRatio = img.GetAspectRatio();
            if (hyperlink?.Properties != null)
            {
                if (hyperlink.Properties.TryGetValue("width", out var widthStr))
                {
                    if (widthStr.Contains('%'))
                    {
                        var pct = float.Parse(widthStr.Replace("%", ""));
                        var scaledWidth = maxWidth * pct / 100;
                        width = showLoading ? scaledWidth : Mathf.Min(width, scaledWidth);
                    }
                    else
                    {
                        width = float.Parse(widthStr);
                    }
                    height = width / aspectRatio;
                }
            }

            // Constrain to max width
            if (width > maxWidth)
            {
                width = maxWidth;
                height = width / aspectRatio;
            }

            return new PreparedImage
            {
                Texture = img,
                Width = width,
                Height = height,
                ShowLoading = showLoading
            };
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
                if (segment is ParsedMD.TableSegment tableSegment)
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
                    DrawTable(tableSegment);
                }
                else if (segment is ParsedMD.HyperlinkSegment hyperlink)
                {
                    if (hyperlink.IsImage)
                    {
                        var imagePath = hyperlink.URL;

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
                            var prepared = PrepareImageForDisplay(imagePath, hyperlink, RenderedWindowWidth - PADDING);

                            if (null == m_imageLabelStyle)
                            {
                                m_imageLabelStyle = Styles.Markdown.ImageLabel.Style;
                            }

                            // If next image would go beyond screen we create a new row
                            if (imageSectionWidth + prepared.Width >= RenderedWindowWidth - PADDING)
                            {
                                GUILayout.FlexibleSpace();
                                EditorGUILayout.EndHorizontal();
                                _ = EditorGUILayout.BeginHorizontal();
                                imageSectionWidth = 0;
                            }

                            var content = new GUIContent(prepared.Texture);
                            var imageLabelRect = GUILayoutUtility.GetRect(content, m_imageLabelStyle,
                                GUILayout.Height(prepared.Height), GUILayout.Width(prepared.Width));

                            if (GUI.Button(imageLabelRect, content, m_imageLabelStyle))
                            {
                                ImageViewer.ShowWindow(s_cachedImages[imagePath], Path.GetFileNameWithoutExtension(imagePath));
                                Telemetry.OnImageClicked(markdownPage.TelemetryContext, markdownPage, prepared.Texture.name);
                            }

                            imageSectionWidth += prepared.Width;
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
        /// Draws a markdown table as a Unity Editor table using rect-based layout.
        /// </summary>
        /// <param name="tableSegment">The table segment containing headers and rows.</param>
        private void DrawTable(ParsedMD.TableSegment tableSegment)
        {
            var markdownPage = (MetaHubMarkdownPage)target;

            Draw(() =>
            {
                GUILayout.Space(Styles.Markdown.SPACING);

                var headerStyle = Styles.Markdown.TableHeader.Style;
                var cellStyle = Styles.Markdown.TableCell.Style;
                var maxTableWidth = RenderedWindowWidth - Styles.Markdown.PADDING - Styles.Markdown.SPACING;
                var maxColumnWidth = maxTableWidth / tableSegment.ColumnCount;

                // === PASS 1a: Calculate column widths first ===
                var columnWidths = new float[tableSegment.ColumnCount];

                for (var i = 0; i < tableSegment.ColumnCount; i++)
                {
                    // Check header width
                    var headerText = MarkdownUtils.ParseMarkdown(tableSegment.Headers[i].RawText);
                    var headerSize = headerStyle.CalcSize(new GUIContent(headerText));
                    columnWidths[i] = Mathf.Min(headerSize.x + Styles.Markdown.TABLE_CELL_PADDING * 2, maxColumnWidth);

                    // Check all row cells for this column
                    foreach (var row in tableSegment.Rows)
                    {
                        if (i < row.Length)
                        {
                            var cellText = MarkdownUtils.ParseMarkdown(row[i].RawText);
                            var cellSize = cellStyle.CalcSize(new GUIContent(cellText));
                            var cellWidth = Mathf.Min(cellSize.x + Styles.Markdown.TABLE_CELL_PADDING * 2, maxColumnWidth);
                            columnWidths[i] = Mathf.Max(columnWidths[i], cellWidth);
                        }
                    }
                }

                // === PASS 1b: Calculate row heights using column widths for proper text wrapping and images ===
                var rowHeights = new float[tableSegment.Rows.Length];
                var headerHeight = 0f;

                // Store cell content heights for Pass 4 (avoid recalculating)
                var headerContentHeights = new float[tableSegment.ColumnCount];
                var cellContentHeights = new float[tableSegment.Rows.Length, tableSegment.ColumnCount];

                // Calculate header height
                for (var i = 0; i < tableSegment.ColumnCount; i++)
                {
                    var contentWidth = columnWidths[i] - Styles.Markdown.TABLE_CELL_PADDING * 2;
                    var cellHeight = CalculateCellContentHeight(tableSegment.Headers[i], contentWidth, headerStyle);
                    headerContentHeights[i] = cellHeight;
                    headerHeight = Mathf.Max(headerHeight, cellHeight + Styles.Markdown.TABLE_CELL_PADDING * 2);
                }

                // Calculate each row's height based on the tallest cell (including images)
                for (var rowIdx = 0; rowIdx < tableSegment.Rows.Length; rowIdx++)
                {
                    var row = tableSegment.Rows[rowIdx];
                    for (var i = 0; i < row.Length && i < columnWidths.Length; i++)
                    {
                        var contentWidth = columnWidths[i] - Styles.Markdown.TABLE_CELL_PADDING * 2;
                        var cellHeight = CalculateCellContentHeight(row[i], contentWidth, cellStyle);
                        cellContentHeights[rowIdx, i] = cellHeight;
                        rowHeights[rowIdx] = Mathf.Max(rowHeights[rowIdx], cellHeight + Styles.Markdown.TABLE_CELL_PADDING * 2);
                    }
                }

                // Calculate total table dimensions
                var totalTableWidth = 0f;
                for (var i = 0; i < columnWidths.Length; i++)
                {
                    totalTableWidth += columnWidths[i];
                }

                var totalTableHeight = headerHeight + 1f; // +1 for header separator
                for (var rowIdx = 0; rowIdx < rowHeights.Length; rowIdx++)
                {
                    totalTableHeight += rowHeights[rowIdx];
                    if (rowIdx < rowHeights.Length - 1)
                    {
                        totalTableHeight += 1f; // Row separator
                    }
                }

                // === PASS 2: Reserve space and get base rect ===
                _ = EditorGUILayout.BeginHorizontal();
                GUILayout.Space(Styles.Markdown.TABLE_LEFT_INDENT);

                var tableRect = GUILayoutUtility.GetRect(totalTableWidth, totalTableHeight, GUILayout.ExpandWidth(false));

                // === PASS 3: Draw backgrounds and separators ===
                var currentY = tableRect.y;

                // Draw header background
                var headerRect = new Rect(tableRect.x, currentY, totalTableWidth, headerHeight);
                EditorGUI.DrawRect(headerRect, Styles.Markdown.TableHeaderBackground);
                currentY += headerHeight;

                // Draw header separator
                var headerSepRect = new Rect(tableRect.x, currentY, totalTableWidth, 1f);
                EditorGUI.DrawRect(headerSepRect, Styles.Markdown.TableSeparatorColor);
                currentY += 1f;

                // Draw row backgrounds with alternating colors
                for (var rowIdx = 0; rowIdx < tableSegment.Rows.Length; rowIdx++)
                {
                    var rowColor = rowIdx % 2 == 0
                        ? Styles.Markdown.TableRowBackground
                        : Styles.Markdown.TableRowAlternateBackground;

                    var rowRect = new Rect(tableRect.x, currentY, totalTableWidth, rowHeights[rowIdx]);
                    EditorGUI.DrawRect(rowRect, rowColor);
                    currentY += rowHeights[rowIdx];

                    // Draw row separator (except after last row)
                    if (rowIdx < tableSegment.Rows.Length - 1)
                    {
                        var rowSepRect = new Rect(tableRect.x, currentY, totalTableWidth, 1f);
                        EditorGUI.DrawRect(rowSepRect, Styles.Markdown.TableSeparatorColor);
                        currentY += 1f;
                    }
                }

                // Draw vertical separators between columns
                var verticalX = tableRect.x;
                for (var i = 0; i < columnWidths.Length - 1; i++)
                {
                    verticalX += columnWidths[i];
                    var vertSepRect = new Rect(verticalX, tableRect.y, 1f, totalTableHeight);
                    EditorGUI.DrawRect(vertSepRect, Styles.Markdown.TableSeparatorColor);
                }

                // === PASS 4: Draw cell content ===
                currentY = tableRect.y;

                // Draw header cells
                var currentX = tableRect.x;
                for (var i = 0; i < tableSegment.Headers.Length; i++)
                {
                    var cellRect = new Rect(
                        currentX + Styles.Markdown.TABLE_CELL_PADDING,
                        currentY + Styles.Markdown.TABLE_CELL_PADDING,
                        columnWidths[i] - Styles.Markdown.TABLE_CELL_PADDING * 2,
                        headerHeight - Styles.Markdown.TABLE_CELL_PADDING * 2
                    );
                    DrawTableCellRect(tableSegment.Headers[i], cellRect, true, markdownPage, headerContentHeights[i]);
                    currentX += columnWidths[i];
                }
                currentY += headerHeight + 1f; // +1 for separator

                // Draw data rows
                for (var rowIdx = 0; rowIdx < tableSegment.Rows.Length; rowIdx++)
                {
                    var row = tableSegment.Rows[rowIdx];
                    currentX = tableRect.x;

                    for (var i = 0; i < row.Length; i++)
                    {
                        var cellRect = new Rect(
                            currentX + Styles.Markdown.TABLE_CELL_PADDING,
                            currentY + Styles.Markdown.TABLE_CELL_PADDING,
                            columnWidths[i] - Styles.Markdown.TABLE_CELL_PADDING * 2,
                            rowHeights[rowIdx] - Styles.Markdown.TABLE_CELL_PADDING * 2
                        );
                        DrawTableCellRect(row[i], cellRect, false, markdownPage, cellContentHeights[rowIdx, i]);
                        currentX += columnWidths[i];
                    }

                    currentY += rowHeights[rowIdx];
                    if (rowIdx < tableSegment.Rows.Length - 1)
                    {
                        currentY += 1f; // Row separator
                    }
                }

                // Draw table border
                var borderRect = new Rect(tableRect.x, tableRect.y, totalTableWidth, totalTableHeight);
                DrawRectBorder(borderRect, Styles.Markdown.TableSeparatorColor);

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(Styles.Markdown.SPACING);
            });
        }

        /// <summary>
        /// Draws a rect border (outline only, no fill).
        /// </summary>
        private void DrawRectBorder(Rect rect, Color color, float thickness = 1f)
        {
            // Top
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            // Bottom
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            // Left
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            // Right
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        /// <summary>
        /// Calculates the height of a table cell's content, including text and images.
        /// </summary>
        private float CalculateCellContentHeight(ParsedMD.TableCell cell, float contentWidth, GUIStyle textStyle)
        {
            var totalHeight = 0f;

            foreach (var segment in cell.Segments)
            {
                if (segment is ParsedMD.HyperlinkSegment hyperlink)
                {
                    if (hyperlink.IsImage)
                    {
                        // Get image dimensions
                        var prepared = PrepareImageForDisplay(hyperlink.URL, hyperlink, contentWidth, defaultSize: 50f);
                        totalHeight += prepared.Height;
                    }
                    else
                    {
                        // Hyperlink text height
                        var linkText = hyperlink.Text;
                        var linkHeight = m_linkStyle != null
                            ? m_linkStyle.CalcHeight(new GUIContent(linkText), contentWidth)
                            : textStyle.CalcHeight(new GUIContent(linkText), contentWidth);
                        totalHeight += linkHeight;
                    }
                }
                else
                {
                    // Regular text height with word wrapping
                    var parsedText = MarkdownUtils.ParseMarkdown(segment.Text);
                    if (!string.IsNullOrEmpty(parsedText))
                    {
                        var textHeight = textStyle.CalcHeight(new GUIContent(parsedText), contentWidth);
                        totalHeight += textHeight;
                    }
                }
            }

            return Mathf.Max(totalHeight, textStyle.lineHeight);
        }

        /// <summary>
        /// Draws a single table cell with support for text, hyperlinks, and images using rect-based positioning.
        /// </summary>
        /// <param name="cell">The table cell to draw.</param>
        /// <param name="cellRect">The rect to draw the cell in (excluding padding).</param>
        /// <param name="isHeader">Whether this is a header cell.</param>
        /// <param name="markdownPage">The markdown page for telemetry.</param>
        /// <param name="preCalculatedContentHeight">Pre-calculated content height from Pass 1b to avoid recalculation.</param>
        private void DrawTableCellRect(ParsedMD.TableCell cell, Rect cellRect, bool isHeader, MetaHubMarkdownPage markdownPage, float preCalculatedContentHeight)
        {
            var style = isHeader ? Styles.Markdown.TableHeader.Style : Styles.Markdown.TableCell.Style;

            // Use pre-calculated content height for vertical centering
            var startY = cellRect.y + (cellRect.height - preCalculatedContentHeight) / 2f;
            var currentY = startY;

            foreach (var segment in cell.Segments)
            {
                if (segment is ParsedMD.HyperlinkSegment hyperlink)
                {
                    if (hyperlink.IsImage)
                    {
                        var prepared = PrepareImageForDisplay(hyperlink.URL, hyperlink, cellRect.width, defaultSize: 50f);
                        var imageRect = new Rect(
                            cellRect.x + (cellRect.width - prepared.Width) / 2f,
                            currentY,
                            prepared.Width,
                            prepared.Height
                        );
                        DrawTableCellImageAtRect(hyperlink, imageRect, markdownPage);
                        currentY += prepared.Height;
                    }
                    else
                    {
                        var linkSize = m_linkStyle.CalcSize(new GUIContent(hyperlink.Text));
                        var linkRect = new Rect(
                            cellRect.x + (cellRect.width - linkSize.x) / 2f,
                            currentY,
                            linkSize.x,
                            linkSize.y
                        );
                        DrawTableCellHyperlinkAtRect(hyperlink, linkRect);
                        currentY += linkSize.y;
                    }
                }
                else
                {
                    var parsedText = MarkdownUtils.ParseMarkdown(segment.Text);
                    if (!string.IsNullOrEmpty(parsedText))
                    {
                        var textHeight = style.CalcHeight(new GUIContent(parsedText), cellRect.width);
                        var textRect = new Rect(cellRect.x, currentY, cellRect.width, textHeight);
                        EditorGUI.SelectableLabel(textRect, parsedText, style);
                        currentY += textHeight;
                    }
                }
            }
        }

        /// <summary>
        /// Draws an image at a specific rect within a table cell.
        /// </summary>
        private void DrawTableCellImageAtRect(ParsedMD.HyperlinkSegment hyperlink, Rect imageRect, MetaHubMarkdownPage markdownPage)
        {
            var imagePath = hyperlink.URL;
            var prepared = PrepareImageForDisplay(imagePath, hyperlink, imageRect.width, defaultSize: 50f);

            if (null == m_imageLabelStyle)
            {
                m_imageLabelStyle = Styles.Markdown.ImageLabel.Style;
            }

            var content = new GUIContent(prepared.Texture);

            if (GUI.Button(imageRect, content, m_imageLabelStyle))
            {
                ImageViewer.ShowWindow(s_cachedImages[imagePath], Path.GetFileNameWithoutExtension(imagePath));
                Telemetry.OnImageClicked(markdownPage.TelemetryContext, markdownPage, prepared.Texture.name);
            }
        }

        /// <summary>
        /// Draws a hyperlink at a specific rect within a table cell.
        /// </summary>
        private void DrawTableCellHyperlinkAtRect(ParsedMD.HyperlinkSegment hyperlink, Rect linkRect)
        {
            var url = hyperlink.URL;
            var linkContent = $"<color={Styles.DefaultLinkColor}>" + hyperlink.Text + "</color>";

            if (GUI.Button(linkRect, linkContent, m_linkStyle))
            {
                if (url.StartsWith('.'))
                {
                    if (url.EndsWith(".cs"))
                    {
                        MarkdownUtils.NavigateToSourceFile(url);
                    }
                }
                Application.OpenURL(url);
            }
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
        /// Add Draw function to the start of a code block
        /// </summary>
        private void DrawStartCodeBlock()
        {
            Draw(() =>
            {
                GUILayout.Space(Styles.Markdown.SPACING);
                GUILayout.BeginHorizontal(GUILayout.MaxWidth(RenderedWindowWidth - 20));
                GUILayout.Space(Styles.Markdown.SPACING);
                GUILayout.BeginVertical(Styles.Markdown.Box.Style);
                GUILayout.Space(Styles.Markdown.BOX_SPACING * 2);
            });
        }

        /// <summary>
        /// Add Draw function to the end of a code block
        /// </summary>
        private void DrawEndCodeBlock(string code)
        {
            Draw(() =>
            {
                GUILayout.Space(Styles.Markdown.BOX_SPACING * 2);
                GUILayout.EndVertical();
                var lastRect = GUILayoutUtility.GetLastRect();
                if (code != null && lastRect.Contains(Event.current.mousePosition))
                {
                    if (GUI.Button(new Rect(lastRect.x + lastRect.width - 50, lastRect.y, 50, 20), "Copy"))
                    {
                        if (TryMatch(code, m_codeBlockInnerCodeRegex, out var match))
                        {
                            EditorGUIUtility.systemCopyBuffer = match.Groups[1].Value.Trim();
                        }
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(Styles.Markdown.SPACING);
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
            var isCodeBlock = false;
            // Process line by line
            foreach (var line in lines)
            {
                var processedLine = line;
                string listPrefix = null;
                var newListLevel = 0;
                var startNewListItem = false;
                var isNewQuotedBlockLevel = false;
                var newQuotedBlockLevel = 0;
                var codeBlockStarted = false;
                var codeBlockEnded = false;

                if (TryMatch(processedLine, m_codeBlockRegex, out var codeBlockMatch))
                {
                    isCodeBlock = !isCodeBlock;
                    codeBlockStarted = isCodeBlock;
                    codeBlockEnded = !isCodeBlock;
                }

                // We don't process quotes, list and indentation in code block
                if (!isCodeBlock)
                {
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
                    if (TryMatch(processedLine, m_orderedContent, out var listMatch))
                    {
                        var indentRaw = listMatch.Groups[1].Value;
                        listPrefix = listMatch.Groups[2].Value;
                        listPrefix =
                            string.IsNullOrEmpty(listPrefix) ? "•" : listPrefix; // Normalize '-' and '*' to '•'
                        processedLine = listMatch.Groups[3].Value;

                        newListLevel = Mathf.FloorToInt(indentRaw.Length / 2f) + 1;
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
                }

                isNewQuotedBlockLevel = quotedBlockLevel != newQuotedBlockLevel;
                var change =
                    isNewQuotedBlockLevel || listLevel != newListLevel || startNewListItem || codeBlockStarted;
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
                    DrawContentEnd(
                        ContentType.List, listLevel, isNewQuotedBlockLevel ? 0 : newListLevel, processedLine);
                }

                if (newQuotedBlockLevel != quotedBlockLevel)
                {
                    DrawContentEnd(ContentType.Block, quotedBlockLevel, newQuotedBlockLevel, processedLine);
                }

                // Draw content start in order, if applicable
                // 1. Code Block
                // 2. Quoted Block
                // 3. List
                if (codeBlockStarted)
                {
                    DrawContentStart(processedLine, ContentType.CodeBlock, 0, 0);
                }
                if (newQuotedBlockLevel != quotedBlockLevel)
                {
                    if (newQuotedBlockLevel > 0)
                    {
                        DrawContentStart(processedLine, ContentType.Block, quotedBlockLevel, newQuotedBlockLevel);
                    }
                }

                if (startNewListItem || newListLevel != listLevel)
                {
                    DrawContentStart(
                        listPrefix, ContentType.List, isNewQuotedBlockLevel ? 0 : listLevel, newListLevel);
                }

                // update the current levels
                listLevel = newListLevel;
                quotedBlockLevel = newQuotedBlockLevel;
                // append the processed line to be drawn in the correct time
                _ = sb.Append(processedLine);
                _ = sb.Append("\n");

                if (codeBlockEnded)
                {
                    var code = sb.ToString();
                    DrawContent(code);
                    _ = sb.Clear();
                    DrawContentEnd(ContentType.CodeBlock, 0, 0, code);
                }
            }

            // Final draw of leftover content
            string finalText = null;
            if (sb.Length > 0)
            {
                finalText = sb.ToString();
                DrawContent(finalText, listLevel > 0 ? m_listTextStyle : null);
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

            if (isCodeBlock)
            {
                DrawContentEnd(ContentType.CodeBlock, 0, 0, finalText);
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
                case ContentType.CodeBlock:
                    {
                        DrawStartCodeBlock();
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
                case ContentType.CodeBlock:
                    {
                        DrawEndCodeBlock(followingText);
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
