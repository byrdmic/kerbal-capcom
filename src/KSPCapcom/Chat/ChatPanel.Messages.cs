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

            // Use parsed rendering for completed assistant messages with code blocks
            if (!message.IsPending && message.HasCodeBlocks && message.Role == MessageRole.Assistant)
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

            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
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

                // Expanded details section
                if (isExpanded)
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
        }
    }
}
