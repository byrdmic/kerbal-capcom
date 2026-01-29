using System.Collections.Generic;
using UnityEngine;
using KSPCapcom.Parsing;
using KSPCapcom.UI;

namespace KSPCapcom
{
    public partial class ChatPanel
    {
        // Error message tracking for expandable details
        private readonly Dictionary<int, ErrorMessageData> _errorMessageData = new Dictionary<int, ErrorMessageData>();
        private readonly HashSet<int> _expandedErrorIds = new HashSet<int>();
        private int _nextErrorId = 0;

        // References disclosure tracking (by message index)
        private readonly HashSet<int> _expandedReferenceIds = new HashSet<int>();
        private int _nextReferenceId = 0;
        private readonly Dictionary<ChatMessage, int> _messageReferenceIds = new Dictionary<ChatMessage, int>();

        /// <summary>
        /// Maximum height for expanded references content area.
        /// </summary>
        private const float MAX_REFERENCES_HEIGHT = 150f;

        // Code block parsing and rendering
        private readonly CodeBlockParser _codeBlockParser = new CodeBlockParser();
        private readonly ScriptCardRenderer _scriptCardRenderer = new ScriptCardRenderer();

        private void DrawMessagesArea()
        {
            // Detect scroll wheel input to disengage auto-scroll
            if (Event.current.type == EventType.ScrollWheel)
            {
                _shouldAutoScroll = false;
            }

            // Track scroll position before to detect user-initiated scrollbar drags
            float scrollYBefore = _scrollPosition.y;

            // Begin scroll view and track positions for auto-scroll detection
            _scrollPosition = GUILayout.BeginScrollView(
                _scrollPosition,
                false,
                true,
                GUILayout.ExpandHeight(true)
            );

            // Use the actual scroll view rect from the last layout pass
            if (Event.current.type == EventType.Repaint)
            {
                Rect scrollViewRect = GUILayoutUtility.GetLastRect();
                _lastScrollViewHeight = scrollViewRect.height;
            }

            // Track content start
            float contentStart = 0;
            if (Event.current.type == EventType.Repaint)
            {
                contentStart = GUILayoutUtility.GetLastRect().y;
            }

            foreach (var message in _messages)
            {
                DrawMessage(message);
            }

            // Add a small spacer at the end for visual padding
            GUILayout.Space(4);

            // Track content height for auto-scroll calculation
            if (Event.current.type == EventType.Repaint)
            {
                Rect lastRect = GUILayoutUtility.GetLastRect();
                _lastContentHeight = lastRect.y + lastRect.height - contentStart;
            }

            GUILayout.EndScrollView();

            // Detect if user scrolled via scrollbar drag (position changed upward without pending scroll)
            if (!_pendingScrollToBottom && _scrollPosition.y < scrollYBefore - 1f)
            {
                _shouldAutoScroll = false;
            }

            // Handle pending scroll to bottom
            if (_pendingScrollToBottom && Event.current.type == EventType.Repaint)
            {
                _scrollPosition = new Vector2(0, _lastContentHeight);
                _pendingScrollToBottom = false;
            }

            // Detect if user has scrolled to bottom (re-engage auto-scroll)
            // If scroll position is near the bottom, enable auto-scroll
            if (Event.current.type == EventType.Repaint && _lastContentHeight > _lastScrollViewHeight)
            {
                float maxScroll = _lastContentHeight - _lastScrollViewHeight;
                float distanceFromBottom = maxScroll - _scrollPosition.y;
                if (distanceFromBottom <= SCROLL_BOTTOM_THRESHOLD)
                {
                    _shouldAutoScroll = true;
                    _unseenMessageCount = 0;
                }
            }

            // Jump to latest affordance (visible when auto-scroll is off)
            DrawJumpToLatestButton();

            // Save dialog overlay
            if (_saveDialogOpen)
            {
                DrawSaveDialog();
            }

            // Script editor panel (floating window)
            _scriptEditorPanel?.OnGUI();
        }

        /// <summary>
        /// Draw the "Jump to latest" button when auto-scroll is disengaged.
        /// Shows unseen message count if any new messages arrived.
        /// </summary>
        private void DrawJumpToLatestButton()
        {
            if (_shouldAutoScroll)
            {
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            string label = _unseenMessageCount > 0
                ? $"↓ Jump to latest ({_unseenMessageCount})"
                : "↓ Jump to latest";

            if (GUILayout.Button(label, _jumpToLatestStyle, GUILayout.ExpandWidth(false)))
            {
                ScrollToBottom();
                _shouldAutoScroll = true;
                _unseenMessageCount = 0;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawMessage(ChatMessage message)
        {
            // Handle error messages with expandable details
            if (message.IsErrorMessage && _errorMessageData.TryGetValue(message.ErrorId, out var errorData))
            {
                DrawErrorMessage(message, errorData);
                return;
            }

            // Use parsed rendering for completed assistant messages with code blocks or references
            if (!message.IsPending && message.Role == MessageRole.Assistant &&
                (message.HasCodeBlocks || message.HasReferences))
            {
                DrawParsedMessage(message);
                return;
            }

            GUIStyle style;
            string prefix;
            bool alignRight;
            string badge = "";

            switch (message.Role)
            {
                case MessageRole.User:
                    // Use dimmed style for queued messages
                    style = message.IsQueued ? _queuedMessageStyle : _userMessageStyle;
                    prefix = "<b>You:</b> ";
                    alignRight = true;

                    // Add badge for queue state
                    if (message.WasDropped)
                    {
                        badge = FormatBadge("dropped", HEX_ERROR);
                    }
                    else if (message.IsQueued)
                    {
                        badge = FormatBadge("queued", HEX_MUTED);
                    }
                    break;
                case MessageRole.Assistant:
                    style = _systemMessageStyle;
                    prefix = "<b>CAPCOM:</b> ";
                    alignRight = false;
                    break;
                case MessageRole.System:
                    style = _systemMessageStyle;
                    prefix = "";
                    alignRight = false;
                    break;
                default:
                    style = _messageStyle;
                    prefix = "";
                    alignRight = false;
                    break;
            }

            string displayText = message.Text;

            // Pending message indicator with animated ellipsis
            if (message.IsPending)
            {
                int dots = ((int)(Time.time * 2)) % 4;
                string ellipsis = new string('.', dots);
                displayText = $"<i>{message.Text}{ellipsis}</i>";
            }

            GUILayout.BeginHorizontal();

            if (alignRight)
            {
                GUILayout.FlexibleSpace();
            }

            // Draw message box
            GUILayout.BeginVertical(HighLogic.Skin.box, GUILayout.MaxWidth(_windowRect.width * 0.85f));
            GUILayout.Label($"{FormatTimestamp(message.Timestamp)}{badge}", style);
            GUILayout.Label($"{prefix}{displayText}", style);
            GUILayout.EndVertical();

            if (!alignRight)
            {
                GUILayout.FlexibleSpace();
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draw a parsed message with interleaved prose and script cards.
        /// </summary>
        private void DrawParsedMessage(ChatMessage message)
        {
            GUILayout.BeginHorizontal();

            // Assistant messages are left-aligned
            GUILayout.BeginVertical(HighLogic.Skin.box, GUILayout.MaxWidth(_windowRect.width * 0.85f));

            // Timestamp and prefix
            GUILayout.Label(FormatTimestamp(message.Timestamp), _systemMessageStyle);
            GUILayout.Label("<b>CAPCOM:</b>", _systemMessageStyle);

            // Render each segment
            float maxCardWidth = _windowRect.width * 0.8f;

            foreach (var segment in message.ParsedContent.Segments)
            {
                if (segment is ProseSegment prose)
                {
                    // Render prose as normal text
                    if (!string.IsNullOrWhiteSpace(prose.Content))
                    {
                        GUILayout.Label(prose.Content.Trim(), _systemMessageStyle);
                    }
                }
                else if (segment is CodeBlockSegment codeBlock)
                {
                    // Render as script card with copy, save, and open callbacks
                    _scriptCardRenderer.DrawScriptCard(codeBlock, maxCardWidth, OnScriptCopyResult, OnScriptSaveRequest, OnScriptOpenRequest);
                }
            }

            // Draw references disclosure if message has references
            if (message.HasReferences)
            {
                DrawReferencesDisclosure(message);
            }

            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draw the collapsible references disclosure section.
        /// </summary>
        private void DrawReferencesDisclosure(ChatMessage message)
        {
            // Get or assign a reference ID for this message
            if (!_messageReferenceIds.TryGetValue(message, out int refId))
            {
                refId = _nextReferenceId++;
                _messageReferenceIds[message] = refId;
            }

            bool isExpanded = _expandedReferenceIds.Contains(refId);
            string disclosureLabel = isExpanded
                ? $"\u25bc References ({message.ReferencesCount})"
                : $"\u25b6 References ({message.ReferencesCount})";

            // Create disclosure button style
            var disclosureStyle = new GUIStyle(HighLogic.Skin.button)
            {
                fontSize = FONT_SIZE_SMALL,
                padding = new RectOffset(4, 4, 2, 2),
                alignment = TextAnchor.MiddleLeft
            };
            disclosureStyle.normal.textColor = COLOR_MUTED;

            GUILayout.Space(4);

            if (GUILayout.Button(disclosureLabel, disclosureStyle, GUILayout.ExpandWidth(false)))
            {
                if (isExpanded)
                {
                    _expandedReferenceIds.Remove(refId);
                }
                else
                {
                    _expandedReferenceIds.Add(refId);
                }
                // Note: We do NOT call ScrollToBottom() here to avoid scroll jumps
            }

            // Expanded references content
            if (isExpanded)
            {
                DrawExpandedReferences(message.ReferencesText);
            }
        }

        /// <summary>
        /// Draw the expanded references content in a scrollable area.
        /// </summary>
        private void DrawExpandedReferences(string referencesText)
        {
            // Create muted style for references content
            var referencesStyle = new GUIStyle(_messageStyle)
            {
                fontSize = FONT_SIZE_SMALL,
                wordWrap = true
            };
            referencesStyle.normal.textColor = COLOR_MUTED;

            // Render in a fixed-height box with potential scroll
            GUILayout.BeginVertical(HighLogic.Skin.box);

            // Calculate content height - we'll use GUILayout's automatic handling
            // but cap the max height to prevent huge reference sections
            var content = new GUIContent(FormatReferencesForDisplay(referencesText));
            float contentHeight = referencesStyle.CalcHeight(content, _windowRect.width * 0.75f);

            if (contentHeight > MAX_REFERENCES_HEIGHT)
            {
                // Need scrolling - use a scroll view with max height
                GUILayout.BeginScrollView(
                    Vector2.zero,
                    false,
                    true,
                    GUILayout.MaxHeight(MAX_REFERENCES_HEIGHT));
                GUILayout.Label(FormatReferencesForDisplay(referencesText), referencesStyle);
                GUILayout.EndScrollView();
            }
            else
            {
                // Content fits - render directly
                GUILayout.Label(FormatReferencesForDisplay(referencesText), referencesStyle);
            }

            GUILayout.EndVertical();
        }

        /// <summary>
        /// Format references text for display (strip header, clean up formatting).
        /// </summary>
        private string FormatReferencesForDisplay(string referencesText)
        {
            if (string.IsNullOrEmpty(referencesText))
            {
                return string.Empty;
            }

            // Remove the "## References" header line for display
            var lines = referencesText.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
            var sb = new System.Text.StringBuilder();

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                // Skip the header line
                if (trimmed.StartsWith("##") && trimmed.ToLowerInvariant().Contains("references"))
                {
                    continue;
                }
                // Skip empty warning message
                if (trimmed.StartsWith("_No documentation references"))
                {
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    sb.AppendLine(trimmed);
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Callback for script card copy result. Shows toast feedback.
        /// </summary>
        private void OnScriptCopyResult(bool success, string error)
        {
            if (success)
            {
                AddSystemMessage(FormatSuccess("Copied to clipboard"));
            }
            else
            {
                AddSystemMessage(FormatWarning($"Copy failed: {error ?? "unknown error"}"));
            }
        }

        /// <summary>
        /// Callback for script card open request. Opens the script editor panel.
        /// </summary>
        private void OnScriptOpenRequest(CodeBlockSegment codeBlock)
        {
            _scriptEditorPanel?.Open(codeBlock, OnScriptEditorSaveRequest);
        }

        /// <summary>
        /// Callback from script editor save action. Opens the save dialog with edited code.
        /// </summary>
        private void OnScriptEditorSaveRequest(string editedCode)
        {
            var editedBlock = new CodeBlockSegment(editedCode, 0, editedCode.Length, "kos", editedCode, true);
            OnScriptSaveRequest(editedBlock);
        }

        /// <summary>
        /// Draw an error message with optional expandable details.
        /// </summary>
        private void DrawErrorMessage(ChatMessage message, ErrorMessageData errorData)
        {
            // Pick color based on error type
            Color textColor;
            if (errorData.IsCancellation || errorData.IsRetryable)
                textColor = COLOR_WARNING;  // Orange for cancel/retryable
            else
                textColor = COLOR_ERROR;    // Red for fatal

            GUILayout.BeginHorizontal();

            // Error messages are left-aligned like system messages
            GUILayout.BeginVertical(HighLogic.Skin.box, GUILayout.MaxWidth(_windowRect.width * 0.85f));

            // Timestamp
            GUILayout.Label(FormatTimestamp(message.Timestamp), _systemMessageStyle);

            // Short message with appropriate color
            var errorStyle = new GUIStyle(_systemMessageStyle);
            errorStyle.normal.textColor = textColor;
            GUILayout.Label(errorData.ShortMessage, errorStyle);

            // Action row: Details disclosure and Retry button
            GUILayout.BeginHorizontal();

            // Details disclosure (if available and not cancellation)
            if (errorData.HasDetails)
            {
                bool isExpanded = _expandedErrorIds.Contains(message.ErrorId);
                string disclosureLabel = isExpanded ? "▼ Details" : "▶ Details";

                var disclosureStyle = new GUIStyle(HighLogic.Skin.button)
                {
                    fontSize = FONT_SIZE_SMALL,
                    padding = new RectOffset(4, 4, 2, 2)
                };
                disclosureStyle.normal.textColor = COLOR_MUTED;

                if (GUILayout.Button(disclosureLabel, disclosureStyle, GUILayout.ExpandWidth(false)))
                {
                    if (isExpanded)
                        _expandedErrorIds.Remove(message.ErrorId);
                    else
                        _expandedErrorIds.Add(message.ErrorId);
                }
            }

            // Retry button (only for the most recent retryable error when idle)
            if (CanRetryError(message.ErrorId, errorData.IsRetryable))
            {
                var retryStyle = new GUIStyle(HighLogic.Skin.button)
                {
                    fontSize = FONT_SIZE_SMALL,
                    padding = new RectOffset(8, 8, 2, 2)
                };
                retryStyle.normal.textColor = COLOR_WARNING;

                if (GUILayout.Button("Retry", retryStyle, GUILayout.ExpandWidth(false)))
                {
                    OnRetryClick();
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // Expanded details section
            if (errorData.HasDetails && _expandedErrorIds.Contains(message.ErrorId))
            {
                var detailsStyle = new GUIStyle(_messageStyle)
                {
                    fontSize = FONT_SIZE_SMALL
                };
                detailsStyle.normal.textColor = COLOR_MUTED;

                GUILayout.BeginVertical(HighLogic.Skin.box);
                GUILayout.Label(errorData.TechnicalDetails, detailsStyle);
                GUILayout.EndVertical();
            }

            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Add an error message with expandable details.
        /// </summary>
        private void AddErrorMessage(ErrorMessageData errorData)
        {
            AddErrorMessageAndGetId(errorData);
        }

        /// <summary>
        /// Add an error message with expandable details and return its ID.
        /// </summary>
        private int AddErrorMessageAndGetId(ErrorMessageData errorData)
        {
            int errorId = _nextErrorId++;
            var message = ChatMessage.FromError(errorData.ShortMessage, errorId);
            _messages.Add(message);
            _errorMessageData[errorId] = errorData;
            TrimMessageHistory();
            if (_shouldAutoScroll)
            {
                ScrollToBottom();
            }
            else
            {
                _unseenMessageCount++;
            }
            CapcomCore.Log($"[Error] {errorData.ShortMessage}");
            return errorId;
        }
    }
}
