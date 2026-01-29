using System.Collections.Generic;
using UnityEngine;
using KSPCapcom.UI;

namespace KSPCapcom
{
    public partial class ChatPanel
    {
        // Input focus management
        private int _focusInputFrames;
        private const int FOCUS_FRAME_COUNT = 3;

        // Command history navigation
        private const int MAX_COMMAND_HISTORY = 50;
        private readonly List<string> _commandHistory = new List<string>();
        private int _historyIndex = -1;           // -1 = not navigating, 0..N = position in history
        private string _workingCopy = "";         // Preserved draft when user starts navigating

        // Pinned prompts UI state
        private bool _pinsExpanded = false;

        /// <summary>
        /// Check if any settings text field or editor panel currently has focus.
        /// </summary>
        private bool IsSettingsFieldFocused()
        {
            string focused = GUI.GetNameOfFocusedControl();
            return focused == "SettingsModel" ||
                   focused == "SettingsApiKey" ||
                   focused == "SettingsEndpoint" ||
                   focused == "SettingsArchivePath" ||
                   focused == "SaveDialogFilename" ||
                   focused == ScriptEditorPanel.ControlName;
        }

        /// <summary>
        /// Draw the input area with state-aware keyboard handling.
        ///
        /// Input State Machine:
        /// | State              | Conditions                          | Enter           | Shift+Enter | Escape           |
        /// |--------------------|-------------------------------------|-----------------|-------------|------------------|
        /// | GENERATING         | IsWaitingForResponse == true        | Send (queues)   | Newline     | Cancel request   |
        /// | IDLE_WITH_INPUT    | Not generating, input non-empty     | Send            | Newline     | Clear input      |
        /// | IDLE_EMPTY         | Not generating, input empty         | No-op           | Newline     | Close panel      |
        ///
        /// Escape behavior uses progressive dismissal: first clears input, then closes panel.
        /// </summary>
        private void DrawInputArea()
        {
            // Generating status indicator with elapsed time
            if (IsWaitingForResponse)
            {
                var elapsed = (System.DateTime.UtcNow - _requestStartTime).TotalSeconds;
                var statusText = $"Generating... ({elapsed:F0}s)";
                GUILayout.Label(statusText, _statusLabelStyle);
            }

            // Draw pinned prompts row
            DrawPinnedPromptsRow();

            // Handle keyboard input before drawing
            bool shouldSend = false;
            Event e = Event.current;

            // Only handle keyboard events when ChatInput has focus
            if (e.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == "ChatInput")
            {
                // Determine current state
                bool isGenerating = IsWaitingForResponse;
                bool hasInput = !string.IsNullOrWhiteSpace(_inputText);

                // Handle Enter key
                if (e.keyCode == KeyCode.Return)
                {
                    if (e.shift)
                    {
                        // Shift+Enter: let TextArea handle newline naturally
                    }
                    else if (hasInput)
                    {
                        // Send (or queue if generating)
                        shouldSend = true;
                        e.Use();
                    }
                }
                // Handle Escape key
                else if (e.keyCode == KeyCode.Escape)
                {
                    if (isGenerating)
                    {
                        // GENERATING: Cancel request
                        CancelCurrentRequest();
                        CapcomCore.Log("User cancelled request via Escape key");
                        e.Use();
                    }
                    else if (hasInput)
                    {
                        // IDLE_WITH_INPUT: Clear input
                        _inputText = "";
                        CapcomCore.Log("User cleared input via Escape key");
                        e.Use();
                    }
                    else
                    {
                        // IDLE_EMPTY: Close panel
                        Hide();
                        CapcomCore.Log("User closed panel via Escape key");
                        e.Use();
                    }
                }
                // Handle Up arrow for history navigation
                else if (e.keyCode == KeyCode.UpArrow && !e.shift)
                {
                    NavigateHistoryUp();
                    e.Use();
                }
                // Handle Down arrow for history navigation
                else if (e.keyCode == KeyCode.DownArrow && !e.shift)
                {
                    NavigateHistoryDown();
                    e.Use();
                }
            }

            GUILayout.BeginHorizontal();

            // Pin/unpin toggle (only show if we have a pinned store and non-empty input)
            DrawPinToggle();

            // Input remains enabled even while waiting (messages will queue)
            // This keeps UI responsive

            // Calculate dynamic height for input area based on content
            // Use TextArea for multiline input with Shift+Enter support
            // Name the control BEFORE drawing
            GUI.SetNextControlName("ChatInput");

            // Calculate appropriate height based on line count (capped)
            int lineCount = 1;
            if (!string.IsNullOrEmpty(_inputText))
            {
                lineCount = _inputText.Split('\n').Length;
            }
            float inputHeight = Mathf.Min(20f + (lineCount - 1) * 16f, MAX_INPUT_HEIGHT);

            string newInput = GUILayout.TextArea(_inputText, _inputStyle,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(inputHeight));

            // Detect if user manually edited (not from history navigation)
            if (newInput != _inputText && _historyIndex != -1)
            {
                // User typed while navigating - exit history mode
                ResetHistoryNavigation();
            }
            _inputText = newInput;

            // Focus AFTER drawing, with guard against stealing from settings fields
            if (_focusInputFrames > 0)
            {
                if (!IsSettingsFieldFocused())
                {
                    GUI.FocusControl("ChatInput");
                }
                _focusInputFrames--;
            }

            // Ascent button with disabled reason
            var ascentValidation = ValidateAscentPrerequisites();
            bool canAscent = ascentValidation.IsValid;

            GUILayout.BeginVertical(GUILayout.Width(55));
            GUI.enabled = canAscent;
            if (GUILayout.Button("Ascent", _critiqueButtonStyle, GUILayout.Width(50), GUILayout.Height(inputHeight)))
            {
                OnAscentScriptClick();
            }
            GUI.enabled = true;

            // Show reason as small hint below disabled button
            if (!canAscent && !string.IsNullOrEmpty(ascentValidation.Reason))
            {
                GUILayout.Label(ascentValidation.Reason, _disabledHintStyle, GUILayout.Width(55));
            }
            GUILayout.EndVertical();

            // Critique button - only shown/enabled in editor with valid craft
            bool canCritique = CanCritique();
            GUI.enabled = canCritique;
            if (GUILayout.Button("Critique", _critiqueButtonStyle, GUILayout.Width(60), GUILayout.Height(inputHeight)))
            {
                OnCritiqueClick();
            }
            GUI.enabled = true;

            // Send/Stop button - contextual based on state
            if (IsWaitingForResponse)
            {
                // Stop button when waiting for response
                if (GUILayout.Button("Stop", _cancelButtonStyle, GUILayout.Width(50), GUILayout.Height(inputHeight)))
                {
                    CancelCurrentRequest();
                    CapcomCore.Log("User cancelled request via Stop button");
                }

                // Queue count indicator
                if (_messageQueue.Count > 0)
                {
                    GUILayout.Label($"+{_messageQueue.Count}", _queueCountStyle, GUILayout.Width(25), GUILayout.Height(inputHeight));
                }
            }
            else
            {
                // Normal Send button
                if (GUILayout.Button("Send", GUILayout.Width(50), GUILayout.Height(inputHeight)))
                {
                    if (!string.IsNullOrWhiteSpace(_inputText))
                    {
                        shouldSend = true;
                    }
                }
            }

            GUILayout.EndHorizontal();

            // Process send after UI is drawn
            if (shouldSend)
            {
                SendMessage();
            }
        }

        /// <summary>
        /// Add a command to the history ring buffer.
        /// Skips empty/whitespace strings and consecutive duplicates.
        /// </summary>
        private void AddToHistory(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            // Skip consecutive duplicates
            if (_commandHistory.Count > 0 &&
                _commandHistory[_commandHistory.Count - 1] == command)
            {
                return;
            }

            _commandHistory.Add(command);

            // Trim if exceeds capacity (remove oldest)
            while (_commandHistory.Count > MAX_COMMAND_HISTORY)
            {
                _commandHistory.RemoveAt(0);
            }

            // Reset navigation state
            _historyIndex = -1;
            _workingCopy = "";
        }

        /// <summary>
        /// Navigate to older command in history (Up arrow).
        /// </summary>
        private void NavigateHistoryUp()
        {
            if (_commandHistory.Count == 0)
            {
                return;
            }

            if (_historyIndex == -1)
            {
                // First Up press - save current input and go to newest history entry
                _workingCopy = _inputText;
                _historyIndex = _commandHistory.Count - 1;
            }
            else if (_historyIndex > 0)
            {
                // Navigate to older entry
                _historyIndex--;
            }
            // else: already at oldest, stay there

            _inputText = _commandHistory[_historyIndex];
        }

        /// <summary>
        /// Navigate to newer command in history (Down arrow).
        /// </summary>
        private void NavigateHistoryDown()
        {
            if (_historyIndex == -1)
            {
                // Not navigating, nothing to do
                return;
            }

            if (_historyIndex < _commandHistory.Count - 1)
            {
                // Navigate to newer entry
                _historyIndex++;
                _inputText = _commandHistory[_historyIndex];
            }
            else
            {
                // At newest entry, return to working copy
                _historyIndex = -1;
                _inputText = _workingCopy;
            }
        }

        /// <summary>
        /// Reset history navigation state (when user manually edits).
        /// </summary>
        private void ResetHistoryNavigation()
        {
            _historyIndex = -1;
            _workingCopy = "";
        }

        #region Pinned Prompts UI

        /// <summary>
        /// Draw the collapsible pinned prompts row.
        /// </summary>
        private void DrawPinnedPromptsRow()
        {
            if (_pinnedPrompts == null || _pinnedPrompts.Count == 0)
                return;

            GUILayout.BeginHorizontal();

            // Toggle button to expand/collapse
            string toggleLabel = _pinsExpanded ? "▼ Pins" : "► Pins";
            if (GUILayout.Button(toggleLabel, _pinnedButtonStyle, GUILayout.Width(50)))
            {
                _pinsExpanded = !_pinsExpanded;
            }

            // Show pinned prompts as buttons when expanded
            if (_pinsExpanded)
            {
                var pinned = _pinnedPrompts.GetPinned();
                foreach (var prompt in pinned)
                {
                    if (GUILayout.Button(prompt.DisplayLabel, _pinnedButtonStyle, GUILayout.MaxWidth(100)))
                    {
                        // Insert into input field (do not auto-send)
                        _inputText = prompt.Text;
                        _focusInputFrames = FOCUS_FRAME_COUNT;
                        ResetHistoryNavigation();
                        CapcomCore.Log($"Inserted pinned prompt: {prompt.DisplayLabel}");
                    }
                }

                GUILayout.FlexibleSpace();
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draw the pin/unpin toggle button.
        /// Shows filled star if current input is pinned, empty star if not.
        /// Hidden if input is empty or no pin store available.
        /// </summary>
        private void DrawPinToggle()
        {
            if (_pinnedPrompts == null)
                return;

            // Only show toggle if input has content
            if (string.IsNullOrWhiteSpace(_inputText))
            {
                // Reserve space to prevent layout shift
                GUILayout.Space(24);
                return;
            }

            bool isPinned = _pinnedPrompts.IsPinned(_inputText);
            string buttonLabel = isPinned ? "★" : "☆";
            string tooltip = isPinned ? "Unpin this prompt" : "Pin this prompt";

            if (GUILayout.Button(new GUIContent(buttonLabel, tooltip), _pinToggleStyle, GUILayout.Width(24), GUILayout.Height(24)))
            {
                if (isPinned)
                {
                    _pinnedPrompts.Unpin(_inputText);
                    _pinnedPrompts.Save();
                }
                else
                {
                    if (_pinnedPrompts.Pin(_inputText, PinnedPromptStore.GenerateLabel(_inputText)))
                    {
                        _pinnedPrompts.Save();
                    }
                    else if (_pinnedPrompts.IsFull)
                    {
                        AddSystemMessage(FormatWarning("Cannot pin: maximum of 10 pins reached. Unpin one first."));
                    }
                }
            }
        }

        #endregion
    }
}
