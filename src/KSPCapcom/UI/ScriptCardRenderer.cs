using System;
using KSPCapcom.Parsing;
using KSPCapcom.Validation;
using UnityEngine;

namespace KSPCapcom.UI
{
    /// <summary>
    /// Renders kOS script cards in the chat panel using IMGUI.
    /// Provides Copy button, expandable view for long scripts, and syntax status.
    /// </summary>
    public class ScriptCardRenderer
    {
        #region Style Constants

        // Card colors
        private static readonly Color COLOR_CARD_BACKGROUND = new Color(0.15f, 0.15f, 0.2f, 1f);
        private static readonly Color COLOR_CODE_TEXT = new Color(0.9f, 0.95f, 0.9f);
        private static readonly Color COLOR_HEADER_TEXT = new Color(0.7f, 0.8f, 0.9f);
        private static readonly Color COLOR_BUTTON_NORMAL = new Color(0.4f, 0.6f, 0.8f);
        private static readonly Color COLOR_BUTTON_HOVER = new Color(0.5f, 0.7f, 0.9f);

        // Status badge colors
        private static readonly Color COLOR_BADGE_SUCCESS = new Color(0.4f, 0.8f, 0.4f);
        private static readonly Color COLOR_BADGE_WARNING = new Color(1f, 0.7f, 0.3f);
        private static readonly Color COLOR_BADGE_NEUTRAL = new Color(0.6f, 0.6f, 0.6f);

        // Dimensions
        private const int FONT_SIZE_CODE = 11;
        private const int FONT_SIZE_HEADER = 10;
        private const int FONT_SIZE_BADGE = 9;
        private const int MAX_COLLAPSED_LINES = 10;
        private const float CARD_PADDING = 6f;
        private const float BUTTON_WIDTH = 45f;
        private const float BUTTON_HEIGHT = 18f;

        #endregion

        // Styles (lazily initialized)
        private GUIStyle _cardBoxStyle;
        private GUIStyle _codeStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _badgeStyle;
        private bool _stylesInitialized;

        // Track expanded state per code block (by hash of content)
        private readonly System.Collections.Generic.HashSet<int> _expandedCards =
            new System.Collections.Generic.HashSet<int>();

        /// <summary>
        /// Render a script card for a code block segment.
        /// </summary>
        /// <param name="codeBlock">The code block segment to render.</param>
        /// <param name="maxWidth">Maximum width for the card.</param>
        public void DrawScriptCard(CodeBlockSegment codeBlock, float maxWidth)
        {
            if (codeBlock == null)
            {
                return;
            }

            InitializeStyles();

            int cardId = codeBlock.RawCode.GetHashCode();
            bool isExpanded = _expandedCards.Contains(cardId);

            // Count lines for collapse decision
            string[] lines = codeBlock.RawCode.Split('\n');
            bool needsCollapse = lines.Length > MAX_COLLAPSED_LINES;

            // Card container
            GUILayout.BeginVertical(_cardBoxStyle, GUILayout.MaxWidth(maxWidth));

            // Header row: [kOS Script] [Copy] [Expand]
            DrawHeader(codeBlock, cardId, needsCollapse, isExpanded);

            // Code content
            DrawCodeContent(codeBlock.RawCode, lines, needsCollapse, isExpanded);

            // Status badge
            DrawStatusBadge(codeBlock);

            GUILayout.EndVertical();
        }

        /// <summary>
        /// Draw the card header with title and action buttons.
        /// </summary>
        private void DrawHeader(CodeBlockSegment codeBlock, int cardId, bool needsCollapse, bool isExpanded)
        {
            GUILayout.BeginHorizontal();

            // Title
            string title = codeBlock.IsKosLikely ? "kOS Script" : "Code";
            if (!string.IsNullOrEmpty(codeBlock.Language))
            {
                title = $"[{codeBlock.Language}]";
            }
            else if (codeBlock.IsKosLikely)
            {
                title = "[kOS Script]";
            }

            GUILayout.Label(title, _headerStyle);
            GUILayout.FlexibleSpace();

            // Copy button
            if (GUILayout.Button("Copy", _buttonStyle, GUILayout.Width(BUTTON_WIDTH), GUILayout.Height(BUTTON_HEIGHT)))
            {
                CopyToClipboard(codeBlock.RawCode);
            }

            // Expand/Collapse button (only for long scripts)
            if (needsCollapse)
            {
                string expandLabel = isExpanded ? "▲" : "▼";
                if (GUILayout.Button(expandLabel, _buttonStyle, GUILayout.Width(22f), GUILayout.Height(BUTTON_HEIGHT)))
                {
                    if (isExpanded)
                    {
                        _expandedCards.Remove(cardId);
                    }
                    else
                    {
                        _expandedCards.Add(cardId);
                    }
                }
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draw the code content area.
        /// </summary>
        private void DrawCodeContent(string rawCode, string[] lines, bool needsCollapse, bool isExpanded)
        {
            string displayCode;
            if (needsCollapse && !isExpanded)
            {
                // Show only first N lines with ellipsis
                var truncatedLines = new string[MAX_COLLAPSED_LINES + 1];
                Array.Copy(lines, truncatedLines, Math.Min(lines.Length, MAX_COLLAPSED_LINES));
                truncatedLines[MAX_COLLAPSED_LINES] = $"... ({lines.Length - MAX_COLLAPSED_LINES} more lines)";
                displayCode = string.Join("\n", truncatedLines);
            }
            else
            {
                displayCode = rawCode;
            }

            GUILayout.Label(displayCode, _codeStyle);
        }

        /// <summary>
        /// Draw the syntax status badge.
        /// </summary>
        private void DrawStatusBadge(CodeBlockSegment codeBlock)
        {
            // Only show badge for kOS-like code
            if (!codeBlock.IsKosLikely)
            {
                return;
            }

            GUILayout.BeginHorizontal();

            string badgeText;
            Color badgeColor;

            if (codeBlock.SyntaxResult == null)
            {
                // Not validated yet
                badgeText = "Not validated";
                badgeColor = COLOR_BADGE_NEUTRAL;
            }
            else if (!codeBlock.SyntaxResult.HasIssues)
            {
                badgeText = "Syntax OK";
                badgeColor = COLOR_BADGE_SUCCESS;
            }
            else
            {
                int issueCount = codeBlock.SyntaxResult.Issues.Count;
                badgeText = issueCount == 1 ? "1 syntax issue" : $"{issueCount} syntax issues";
                badgeColor = COLOR_BADGE_WARNING;
            }

            // Apply badge color
            var originalColor = GUI.color;
            GUI.color = badgeColor;
            GUILayout.Label(badgeText, _badgeStyle);
            GUI.color = originalColor;

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Copy text to the system clipboard.
        /// </summary>
        private void CopyToClipboard(string text)
        {
            try
            {
                GUIUtility.systemCopyBuffer = text;
                CapcomCore.Log("ScriptCard: Code copied to clipboard");
            }
            catch (Exception ex)
            {
                CapcomCore.LogWarning($"ScriptCard: Failed to copy to clipboard - {ex.Message}");
            }
        }

        /// <summary>
        /// Initialize IMGUI styles for the script card.
        /// </summary>
        private void InitializeStyles()
        {
            if (_stylesInitialized)
            {
                return;
            }

            // Card box style (dark background)
            _cardBoxStyle = new GUIStyle(HighLogic.Skin.box)
            {
                padding = new RectOffset(
                    (int)CARD_PADDING,
                    (int)CARD_PADDING,
                    (int)CARD_PADDING,
                    (int)CARD_PADDING),
                margin = new RectOffset(0, 0, 4, 4)
            };

            // Create dark background texture
            var bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, COLOR_CARD_BACKGROUND);
            bgTex.Apply();
            _cardBoxStyle.normal.background = bgTex;

            // Code style (monospace, light text)
            _codeStyle = new GUIStyle(HighLogic.Skin.label)
            {
                font = Font.CreateDynamicFontFromOSFont("Consolas", FONT_SIZE_CODE),
                fontSize = FONT_SIZE_CODE,
                wordWrap = true,
                richText = false,
                padding = new RectOffset(4, 4, 4, 4)
            };
            _codeStyle.normal.textColor = COLOR_CODE_TEXT;

            // Header style
            _headerStyle = new GUIStyle(HighLogic.Skin.label)
            {
                fontSize = FONT_SIZE_HEADER,
                fontStyle = FontStyle.Bold
            };
            _headerStyle.normal.textColor = COLOR_HEADER_TEXT;

            // Button style
            _buttonStyle = new GUIStyle(HighLogic.Skin.button)
            {
                fontSize = FONT_SIZE_HEADER,
                padding = new RectOffset(4, 4, 2, 2)
            };
            _buttonStyle.normal.textColor = COLOR_BUTTON_NORMAL;
            _buttonStyle.hover.textColor = COLOR_BUTTON_HOVER;

            // Badge style
            _badgeStyle = new GUIStyle(HighLogic.Skin.label)
            {
                fontSize = FONT_SIZE_BADGE,
                padding = new RectOffset(4, 4, 2, 2)
            };

            _stylesInitialized = true;
        }

        /// <summary>
        /// Clear expanded state for all cards.
        /// </summary>
        public void ClearExpandedState()
        {
            _expandedCards.Clear();
        }
    }
}
