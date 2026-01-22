using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using KSPCapcom.Responders;

namespace KSPCapcom
{
    /// <summary>
    /// Chat panel window for CAPCOM communication.
    /// Uses Unity IMGUI for rendering.
    ///
    /// Features:
    /// - Scrollable message history with bounded size
    /// - Smart auto-scroll (only scrolls to bottom if user hasn't scrolled up)
    /// - Enter to send, Shift+Enter for newline
    /// - Lightweight rendering to avoid frame hitches
    /// </summary>
    public class ChatPanel
    {
        private const int WINDOW_ID = 84729;
        private const float DEFAULT_WIDTH = 350f;
        private const float DEFAULT_HEIGHT = 400f;
        private const float MIN_WIDTH = 280f;
        private const float MIN_HEIGHT = 200f;

        /// <summary>
        /// Maximum number of messages to retain in history.
        /// Oldest messages are removed when limit is exceeded.
        /// </summary>
        private const int MAX_MESSAGES = 100;

        /// <summary>
        /// Threshold in pixels from bottom to consider "at bottom" for auto-scroll.
        /// </summary>
        private const float SCROLL_BOTTOM_THRESHOLD = 30f;

        /// <summary>
        /// Maximum height of the input text area in pixels.
        /// </summary>
        private const float MAX_INPUT_HEIGHT = 80f;

        private Rect _windowRect;
        private bool _isVisible;
        private string _inputText = "";
        private Vector2 _scrollPosition;
        private readonly List<ChatMessage> _messages;
        private readonly IResponder _responder;

        // Pending message state (replaces _waitingForResponse)
        private ChatMessage _pendingMessage;
        private readonly MessageQueue _messageQueue;
        private CancellationTokenSource _currentRequestCts;

        // Styles
        private GUIStyle _windowStyle;
        private GUIStyle _messageStyle;
        private GUIStyle _userMessageStyle;
        private GUIStyle _systemMessageStyle;
        private GUIStyle _inputStyle;
        private bool _stylesInitialized;

        // Input focus management
        private bool _focusInput;

        // Settings UI state
        private readonly CapcomSettings _settings;
        private readonly SecretStore _secrets;
        private bool _settingsExpanded = false;
        private string _endpointInput = "";
        private string _modelInput = "";
        private string _apiKeyInput = "";
        private bool _showApiKeyField = false;
        private GUIStyle _settingsHeaderStyle;
        private GUIStyle _settingsBoxStyle;
        private GUIStyle _validationErrorStyle;
        private GUIStyle _statusLabelStyle;
        private GUIStyle _cancelButtonStyle;
        private GUIStyle _queueCountStyle;
        private GUIStyle _queuedMessageStyle;

        // Auto-scroll management
        private bool _shouldAutoScroll = true;
        private float _lastScrollViewHeight;
        private float _lastContentHeight;
        private bool _pendingScrollToBottom;

        /// <summary>
        /// Whether the chat panel is currently visible.
        /// </summary>
        public bool IsVisible => _isVisible;

        /// <summary>
        /// Current number of messages in history.
        /// </summary>
        public int MessageCount => _messages.Count;

        public ChatPanel() : this(new EchoResponder(), null, null)
        {
        }

        public ChatPanel(IResponder responder) : this(responder, null, null)
        {
        }

        public ChatPanel(IResponder responder, CapcomSettings settings) : this(responder, settings, null)
        {
        }

        public ChatPanel(IResponder responder, CapcomSettings settings, SecretStore secrets)
        {
            _responder = responder ?? throw new ArgumentNullException(nameof(responder));
            _settings = settings;
            _secrets = secrets;
            _messages = new List<ChatMessage>();
            _messageQueue = new MessageQueue();
            _isVisible = false;
            _stylesInitialized = false;
            _pendingMessage = null;

            // Initialize settings UI inputs from settings
            if (_settings != null)
            {
                _endpointInput = _settings.Endpoint;
                _modelInput = _settings.Model;
            }

            // Position window on the right side of the screen
            float x = Screen.width - DEFAULT_WIDTH - 50;
            float y = 100;
            _windowRect = new Rect(x, y, DEFAULT_WIDTH, DEFAULT_HEIGHT);

            // Add welcome message
            AddSystemMessage("CAPCOM online. How can I assist you, Flight?");

            CapcomCore.Log($"ChatPanel initialized with responder: {_responder.Name}");
        }

        /// <summary>
        /// Whether a response is currently being generated or pending messages are queued.
        /// </summary>
        private bool IsWaitingForResponse => _pendingMessage != null || _responder.IsBusy;

        /// <summary>
        /// Toggle the panel visibility.
        /// </summary>
        public void Toggle()
        {
            _isVisible = !_isVisible;
            CapcomCore.Log($"Chat panel {(_isVisible ? "opened" : "closed")}");
            if (_isVisible)
            {
                _focusInput = true;
                // When showing panel, scroll to bottom to show latest
                _pendingScrollToBottom = true;
                _shouldAutoScroll = true;
            }
        }

        /// <summary>
        /// Show the panel.
        /// </summary>
        public void Show()
        {
            if (!_isVisible)
            {
                CapcomCore.Log("Chat panel opened");
                _isVisible = true;
                _focusInput = true;
                _pendingScrollToBottom = true;
                _shouldAutoScroll = true;
            }
        }

        /// <summary>
        /// Hide the panel.
        /// </summary>
        public void Hide()
        {
            if (_isVisible)
            {
                CapcomCore.Log("Chat panel closed");
            }
            _isVisible = false;
        }

        /// <summary>
        /// Called every frame to render the GUI.
        /// </summary>
        public void OnGUI()
        {
            if (!_isVisible)
            {
                return;
            }

            InitializeStyles();

            // Make window draggable and draw it
            _windowRect = GUILayout.Window(
                WINDOW_ID,
                _windowRect,
                DrawWindow,
                "CAPCOM",
                _windowStyle,
                GUILayout.MinWidth(MIN_WIDTH),
                GUILayout.MinHeight(MIN_HEIGHT)
            );

            // Clamp window to screen bounds
            ClampWindowToScreen();
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized)
            {
                return;
            }

            // Window style
            _windowStyle = new GUIStyle(HighLogic.Skin.window)
            {
                padding = new RectOffset(8, 8, 20, 8)
            };

            // Base message style
            _messageStyle = new GUIStyle(HighLogic.Skin.label)
            {
                wordWrap = true,
                richText = true,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(8, 8, 4, 4),
                margin = new RectOffset(0, 0, 2, 2)
            };

            // User message style (right-aligned, different color)
            _userMessageStyle = new GUIStyle(_messageStyle);
            _userMessageStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);

            // System/CAPCOM message style
            _systemMessageStyle = new GUIStyle(_messageStyle);
            _systemMessageStyle.normal.textColor = new Color(0.6f, 0.9f, 0.6f);

            // Input field style - use TextArea style for multiline support
            _inputStyle = new GUIStyle(HighLogic.Skin.textArea)
            {
                padding = new RectOffset(6, 6, 4, 4),
                wordWrap = true
            };

            // Settings header style (collapsible button)
            _settingsHeaderStyle = new GUIStyle(HighLogic.Skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 4, 4)
            };

            // Settings box style
            _settingsBoxStyle = new GUIStyle(HighLogic.Skin.box)
            {
                padding = new RectOffset(8, 8, 8, 8)
            };

            // Validation error style (red text)
            _validationErrorStyle = new GUIStyle(HighLogic.Skin.label)
            {
                wordWrap = true,
                fontSize = 11
            };
            _validationErrorStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);

            // Status label style (for API key status)
            _statusLabelStyle = new GUIStyle(HighLogic.Skin.label)
            {
                fontSize = 11
            };

            // Cancel/Stop button style (orange-red to indicate stop action)
            _cancelButtonStyle = new GUIStyle(HighLogic.Skin.button);
            _cancelButtonStyle.normal.textColor = new Color(1f, 0.6f, 0.4f);
            _cancelButtonStyle.hover.textColor = new Color(1f, 0.7f, 0.5f);

            // Queue count indicator style (small, muted)
            _queueCountStyle = new GUIStyle(HighLogic.Skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter
            };
            _queueCountStyle.normal.textColor = new Color(0.8f, 0.8f, 0.6f);

            // Queued message style (dimmed)
            _queuedMessageStyle = new GUIStyle(_userMessageStyle);
            _queuedMessageStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            _stylesInitialized = true;
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();

            // Collapsible settings area
            DrawSettingsArea();

            // Messages area with scroll
            DrawMessagesArea();

            GUILayout.Space(4);

            // Input area
            DrawInputArea();

            GUILayout.EndVertical();

            // Make window draggable by the title bar
            GUI.DragWindow(new Rect(0, 0, _windowRect.width, 20));
        }

        private void DrawSettingsArea()
        {
            // Only show settings if we have a settings object
            if (_settings == null)
            {
                return;
            }

            // Collapsible header
            string headerText = _settingsExpanded ? "▼ Settings" : "▶ Settings";
            if (GUILayout.Button(headerText, _settingsHeaderStyle))
            {
                _settingsExpanded = !_settingsExpanded;
            }

            // Expanded settings content
            if (_settingsExpanded)
            {
                GUILayout.BeginVertical(_settingsBoxStyle);

                // Mode row: "Mode:" label + two toggles (Teach/Do) acting as radio buttons
                GUILayout.BeginHorizontal();
                GUILayout.Label("Mode:", GUILayout.Width(60));

                bool isTeach = _settings.Mode == OperationMode.Teach;
                bool newTeach = GUILayout.Toggle(isTeach, "Teach", HighLogic.Skin.button, GUILayout.Width(60));
                bool newDo = GUILayout.Toggle(!isTeach, "Do", HighLogic.Skin.button, GUILayout.Width(40));

                // Handle toggle changes (radio button behavior)
                if (newTeach && !isTeach)
                {
                    _settings.Mode = OperationMode.Teach;
                }
                else if (newDo && isTeach)
                {
                    _settings.Mode = OperationMode.Do;
                }

                GUILayout.Label("(placeholder)", GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();

                GUILayout.Space(4);

                // Model row: "Model:" label + text field
                GUILayout.BeginHorizontal();
                GUILayout.Label("Model:", GUILayout.Width(60));

                string newModel = GUILayout.TextField(_modelInput, GUILayout.ExpandWidth(true));
                if (newModel != _modelInput)
                {
                    _modelInput = newModel;
                    _settings.SetModel(newModel);
                }

                GUILayout.EndHorizontal();

                GUILayout.Space(4);

                // API key status row
                if (_secrets != null)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("API Key:", GUILayout.Width(60));

                    if (_secrets.HasApiKey)
                    {
                        var configuredStyle = new GUIStyle(_statusLabelStyle);
                        configuredStyle.normal.textColor = new Color(0.6f, 0.9f, 0.6f);
                        GUILayout.Label("configured", configuredStyle);
                    }
                    else
                    {
                        var notConfiguredStyle = new GUIStyle(_statusLabelStyle);
                        notConfiguredStyle.normal.textColor = new Color(1f, 0.6f, 0.4f);
                        GUILayout.Label("not configured", notConfiguredStyle);
                    }

                    // Toggle button to show/hide API key input
                    if (GUILayout.Button(_showApiKeyField ? "Hide" : "Edit", GUILayout.Width(40)))
                    {
                        _showApiKeyField = !_showApiKeyField;
                        if (_showApiKeyField)
                        {
                            // Don't pre-fill with existing key for security
                            _apiKeyInput = "";
                        }
                    }

                    GUILayout.EndHorizontal();

                    // API key input field (when editing)
                    if (_showApiKeyField)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(64); // Indent to align with other fields

                        // Password field for API key
                        _apiKeyInput = GUILayout.PasswordField(_apiKeyInput, '*', GUILayout.ExpandWidth(true));

                        if (GUILayout.Button("Save", GUILayout.Width(40)))
                        {
                            if (!string.IsNullOrEmpty(_apiKeyInput))
                            {
                                _secrets.SetApiKey(_apiKeyInput);
                                _apiKeyInput = "";
                                _showApiKeyField = false;
                                CapcomCore.Log("API key updated");
                            }
                        }

                        GUILayout.EndHorizontal();

                        // Hint text
                        GUILayout.Label("Enter your OpenAI API key", _statusLabelStyle);
                    }
                }

                GUILayout.Space(4);

                // Endpoint row: "Endpoint:" label + text field
                GUILayout.BeginHorizontal();
                GUILayout.Label("Endpoint:", GUILayout.Width(60));

                string newEndpoint = GUILayout.TextField(_endpointInput, GUILayout.ExpandWidth(true));
                if (newEndpoint != _endpointInput)
                {
                    _endpointInput = newEndpoint;
                    _settings.SetEndpoint(newEndpoint);
                }

                GUILayout.EndHorizontal();

                // Validation message or hint
                if (!_settings.IsEndpointValid)
                {
                    GUILayout.Label(_settings.EndpointValidationError, _validationErrorStyle);
                }
                else if (!string.IsNullOrEmpty(_endpointInput))
                {
                    GUILayout.Label("(stored but unused in M1)", HighLogic.Skin.label);
                }

                GUILayout.EndVertical();
            }

            GUILayout.Space(4);
        }

        private void DrawMessagesArea()
        {
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

            // Handle pending scroll to bottom
            if (_pendingScrollToBottom && Event.current.type == EventType.Repaint)
            {
                _scrollPosition = new Vector2(0, _lastContentHeight);
                _pendingScrollToBottom = false;
            }

            // Detect if user has scrolled up (disable auto-scroll)
            // If scroll position is near the bottom, keep auto-scroll enabled
            if (Event.current.type == EventType.Repaint && _lastContentHeight > _lastScrollViewHeight)
            {
                float maxScroll = _lastContentHeight - _lastScrollViewHeight;
                float distanceFromBottom = maxScroll - _scrollPosition.y;
                _shouldAutoScroll = distanceFromBottom <= SCROLL_BOTTOM_THRESHOLD;
            }
        }

        private void DrawMessage(ChatMessage message)
        {
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
                        badge = " <color=#ff6666>[dropped]</color>";
                    }
                    else if (message.IsQueued)
                    {
                        badge = " <color=#888888>[queued]</color>";
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

            string timestamp = message.Timestamp.ToString("HH:mm");
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
            GUILayout.Label($"<size=10><color=#888888>{timestamp}</color></size>{badge}", style);
            GUILayout.Label($"{prefix}{displayText}", style);
            GUILayout.EndVertical();

            if (!alignRight)
            {
                GUILayout.FlexibleSpace();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawInputArea()
        {
            // Handle keyboard input before drawing
            // Check for Enter key to send (without Shift) or Shift+Enter for newline
            bool shouldSend = false;
            Event e = Event.current;

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return)
            {
                if (GUI.GetNameOfFocusedControl() == "ChatInput")
                {
                    if (e.shift)
                    {
                        // Shift+Enter: Insert newline (handled naturally by TextArea)
                        // Don't consume the event, let TextArea handle it
                    }
                    else
                    {
                        // Enter without Shift: Send message
                        if (!string.IsNullOrWhiteSpace(_inputText))
                        {
                            shouldSend = true;
                            e.Use(); // Consume the event to prevent newline insertion
                        }
                    }
                }
            }

            // Handle Escape key to cancel pending request
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && IsWaitingForResponse)
            {
                CancelCurrentRequest();
                CapcomCore.Log("User cancelled request via Escape key");
                e.Use();
            }

            GUILayout.BeginHorizontal();

            // Input remains enabled even while waiting (messages will queue)
            // This keeps UI responsive

            // Set focus to input field if needed
            if (_focusInput)
            {
                GUI.FocusControl("ChatInput");
                _focusInput = false;
            }

            // Calculate dynamic height for input area based on content
            // Use TextArea for multiline input with Shift+Enter support
            GUI.SetNextControlName("ChatInput");

            // Calculate appropriate height based on line count (capped)
            int lineCount = 1;
            if (!string.IsNullOrEmpty(_inputText))
            {
                lineCount = _inputText.Split('\n').Length;
            }
            float inputHeight = Mathf.Min(20f + (lineCount - 1) * 16f, MAX_INPUT_HEIGHT);

            _inputText = GUILayout.TextArea(_inputText, _inputStyle,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(inputHeight));

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

        private void SendMessage()
        {
            string text = _inputText.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // Detect if message will be queued before adding
            bool willBeQueued = _responder.IsBusy || _pendingMessage != null;

            // Add user message immediately for responsiveness, with queue state
            var userMessage = ChatMessage.FromUser(text, isQueued: willBeQueued);
            _messages.Add(userMessage);
            TrimMessageHistory();
            ScrollToBottom();
            CapcomCore.Log($"[User] {text}");

            // Clear input immediately
            _inputText = "";

            // Re-focus input after sending
            _focusInput = true;

            // If responder is busy, queue the request
            if (willBeQueued)
            {
                bool wasDropped = _messageQueue.Enqueue(new MessageRequest(
                    text,
                    new List<ChatMessage>(_messages).AsReadOnly(),
                    OnResponderComplete,
                    userMessage
                ));

                // Show overflow warning if a message was dropped
                if (wasDropped)
                {
                    AddSystemMessage("<color=#ffaa00>Queue full - oldest message dropped</color>");
                }

                CapcomCore.Log($"Message queued (queue size: {_messageQueue.Count})");
                return;
            }

            // Process immediately
            ProcessUserMessage(text);
        }

        /// <summary>
        /// Process user message and generate response via the responder.
        /// </summary>
        private void ProcessUserMessage(string userText)
        {
            if (_pendingMessage != null)
            {
                CapcomCore.LogWarning("Already waiting for response, ignoring");
                return;
            }

            // Add pending message immediately for visual feedback
            _pendingMessage = ChatMessage.FromAssistantPending();
            _messages.Add(_pendingMessage);
            ScrollToBottom();

            // Create cancellation token for this request (M2 ready)
            _currentRequestCts = new CancellationTokenSource();

            // Pass conversation history, cancellation token, and streaming callback
            _responder.Respond(
                userText,
                _messages.AsReadOnly(),
                _currentRequestCts.Token,
                OnResponderComplete,
                OnStreamChunk
            );
        }

        /// <summary>
        /// Callback when a streaming chunk is received (accumulated text so far).
        /// Called on Unity main thread.
        /// </summary>
        private void OnStreamChunk(string accumulatedText)
        {
            if (_pendingMessage != null)
            {
                _pendingMessage.UpdateText(accumulatedText);
                ScrollToBottom();
            }
        }

        /// <summary>
        /// Callback when responder finishes generating a response.
        /// </summary>
        private void OnResponderComplete(ResponderResult result)
        {
            // Complete the pending message
            if (_pendingMessage != null)
            {
                if (result.Success)
                {
                    // If streaming was used, text is already set via OnStreamChunk
                    // Just mark as complete. Otherwise, set the text now.
                    if (string.IsNullOrEmpty(_pendingMessage.Text) || _pendingMessage.Text == "CAPCOM is thinking...")
                    {
                        // Non-streaming or streaming with no chunks received
                        _pendingMessage.Complete(result.Text);
                    }
                    else
                    {
                        // Streaming was used, text already updated
                        _pendingMessage.Complete();
                    }
                    CapcomCore.Log($"[Assistant] {result.Text}");
                }
                else
                {
                    // Replace pending with error message
                    _messages.Remove(_pendingMessage);
                    var errorColor = GetErrorColor(result.ErrorMessage);
                    AddSystemMessage($"<color={errorColor}>Error: {result.ErrorMessage}</color>");
                    CapcomCore.LogError($"Responder error: {result.ErrorMessage}");
                }
                _pendingMessage = null;
            }
            else if (result.Success)
            {
                // No pending message (shouldn't happen, but handle gracefully)
                AddAssistantMessage(result.Text);
            }
            else
            {
                var errorColor = GetErrorColor(result.ErrorMessage);
                AddSystemMessage($"<color={errorColor}>Error: {result.ErrorMessage}</color>");
                CapcomCore.LogError($"Responder error: {result.ErrorMessage}");
            }

            // Cleanup cancellation token
            _currentRequestCts?.Dispose();
            _currentRequestCts = null;

            ScrollToBottom();

            // Process next queued message if any
            ProcessNextQueuedMessage();
        }

        /// <summary>
        /// Process the next message in the queue if responder is available.
        /// </summary>
        private void ProcessNextQueuedMessage()
        {
            if (_responder.IsBusy || _pendingMessage != null)
            {
                return;
            }

            var next = _messageQueue.Dequeue();
            if (next != null)
            {
                // Mark the user message as no longer queued
                next.UserChatMessage?.MarkDequeued();
                CapcomCore.Log($"Processing queued message (remaining: {_messageQueue.Count})");
                ProcessUserMessage(next.UserMessage);
            }
        }

        /// <summary>
        /// Cancel the current pending request and clear the queue.
        /// </summary>
        public void CancelCurrentRequest()
        {
            _currentRequestCts?.Cancel();
            _responder.Cancel();

            // Clear the queue (marks all queued messages as dequeued)
            _messageQueue.Clear();

            if (_pendingMessage != null)
            {
                _messages.Remove(_pendingMessage);
                _pendingMessage = null;
                AddSystemMessage("<color=#ffaa00>Request cancelled</color>");
            }
        }

        /// <summary>
        /// Get the appropriate color for an error message.
        /// Timeout errors use orange (recoverable), other errors use red.
        /// </summary>
        private string GetErrorColor(string errorMessage)
        {
            // Timeout is recoverable/retryable, use orange like cancellation
            if (errorMessage != null && errorMessage.Contains("timed out"))
            {
                return "#ffaa00";
            }
            // Other errors use red
            return "#ff6666";
        }

        /// <summary>
        /// Add a message from the user.
        /// </summary>
        private void AddUserMessage(string text)
        {
            _messages.Add(ChatMessage.FromUser(text));
            TrimMessageHistory();
            ScrollToBottom();
            CapcomCore.Log($"[User] {text}");
        }

        private void AddAssistantMessage(string text)
        {
            _messages.Add(ChatMessage.FromAssistant(text));
            TrimMessageHistory();
            if (_shouldAutoScroll)
            {
                ScrollToBottom();
            }
            CapcomCore.Log($"[Assistant] {text}");
        }

        /// <summary>
        /// Add a message from CAPCOM/system.
        /// Can be called externally for LLM responses.
        /// </summary>
        public void AddSystemMessage(string text)
        {
            _messages.Add(ChatMessage.FromSystem(text));
            TrimMessageHistory();
            if (_shouldAutoScroll)
            {
                ScrollToBottom();
            }
            CapcomCore.Log($"[System] {text}");
        }

        /// <summary>
        /// Remove oldest messages if history exceeds limit.
        /// </summary>
        private void TrimMessageHistory()
        {
            while (_messages.Count > MAX_MESSAGES)
            {
                _messages.RemoveAt(0);
            }
        }

        /// <summary>
        /// Clear all messages from history.
        /// </summary>
        public void ClearHistory()
        {
            _messages.Clear();
            _scrollPosition = Vector2.zero;
            _shouldAutoScroll = true;
            CapcomCore.Log("Chat history cleared");
        }

        /// <summary>
        /// Force scroll to the bottom of the message history.
        /// </summary>
        public void ScrollToBottom()
        {
            _pendingScrollToBottom = true;
            _shouldAutoScroll = true;
        }

        private void ClampWindowToScreen()
        {
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0, Screen.width - _windowRect.width);
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0, Screen.height - _windowRect.height);
        }
    }

    /// <summary>
    /// Request object for queued messages.
    /// </summary>
    public class MessageRequest
    {
        public string UserMessage { get; }
        public IReadOnlyList<ChatMessage> History { get; }
        public Action<ResponderResult> OnComplete { get; }
        public DateTime QueuedAt { get; }

        /// <summary>
        /// Reference to the user's ChatMessage in the UI for state updates.
        /// </summary>
        public ChatMessage UserChatMessage { get; }

        public MessageRequest(
            string userMessage,
            IReadOnlyList<ChatMessage> history,
            Action<ResponderResult> onComplete,
            ChatMessage userChatMessage = null)
        {
            UserMessage = userMessage;
            History = history;
            OnComplete = onComplete;
            QueuedAt = DateTime.UtcNow;
            UserChatMessage = userChatMessage;
        }
    }

    /// <summary>
    /// Queue for managing message requests with bounded size.
    /// </summary>
    public class MessageQueue
    {
        private readonly Queue<MessageRequest> _queue = new Queue<MessageRequest>();
        private readonly int _maxQueueSize;

        /// <summary>
        /// Default maximum queue size.
        /// </summary>
        public const int DEFAULT_MAX_QUEUE_SIZE = 5;

        public MessageQueue(int maxQueueSize = DEFAULT_MAX_QUEUE_SIZE)
        {
            _maxQueueSize = maxQueueSize;
        }

        public int Count => _queue.Count;
        public bool HasPending => _queue.Count > 0;

        /// <summary>
        /// Enqueue a message request. Returns true if a message was dropped due to overflow.
        /// </summary>
        public bool Enqueue(MessageRequest request)
        {
            bool wasDropped = false;

            // Drop oldest if at capacity
            while (_queue.Count >= _maxQueueSize)
            {
                var dropped = _queue.Dequeue();
                wasDropped = true;

                // Mark the user's chat message as dropped
                dropped.UserChatMessage?.MarkDropped();

                CapcomCore.LogWarning("Message queue full, dropping oldest request");
                // Notify dropped request
                dropped.OnComplete?.Invoke(
                    ResponderResult.Fail("Request dropped - queue overflow"));
            }

            _queue.Enqueue(request);
            return wasDropped;
        }

        public MessageRequest Dequeue()
        {
            return _queue.Count > 0 ? _queue.Dequeue() : null;
        }

        public MessageRequest Peek()
        {
            return _queue.Count > 0 ? _queue.Peek() : null;
        }

        public void Clear()
        {
            while (_queue.Count > 0)
            {
                var request = _queue.Dequeue();
                // Mark the user message as no longer queued
                request.UserChatMessage?.MarkDequeued();
                request.OnComplete?.Invoke(
                    ResponderResult.Fail("Request cancelled - queue cleared"));
            }
        }
    }

    /// <summary>
    /// Represents a single chat message.
    /// </summary>
    public class ChatMessage
    {
        public string Text { get; private set; }
        public MessageRole Role { get; }
        public DateTime Timestamp { get; }

        /// <summary>
        /// Whether this message is still being generated (pending/streaming).
        /// </summary>
        public bool IsPending { get; private set; }

        /// <summary>
        /// Whether this message is currently queued awaiting processing.
        /// </summary>
        public bool IsQueued { get; private set; }

        /// <summary>
        /// Whether this message was dropped due to queue overflow.
        /// </summary>
        public bool WasDropped { get; private set; }

        /// <summary>
        /// Convenience property for backward compatibility.
        /// </summary>
        public bool IsFromUser => Role == MessageRole.User;

        public ChatMessage(string text, MessageRole role, bool isPending = false)
        {
            Text = text;
            Role = role;
            Timestamp = DateTime.Now;
            IsPending = isPending;
        }

        /// <summary>
        /// Update the message text (for streaming or completing pending messages).
        /// </summary>
        public void UpdateText(string newText)
        {
            Text = newText;
        }

        /// <summary>
        /// Mark the message as complete (no longer pending).
        /// </summary>
        public void Complete(string finalText = null)
        {
            if (finalText != null)
            {
                Text = finalText;
            }
            IsPending = false;
        }

        /// <summary>
        /// Mark the message as no longer queued (now being processed).
        /// </summary>
        public void MarkDequeued() => IsQueued = false;

        /// <summary>
        /// Mark the message as dropped due to queue overflow.
        /// </summary>
        public void MarkDropped()
        {
            IsQueued = false;
            WasDropped = true;
        }

        public static ChatMessage FromUser(string text, bool isQueued = false) =>
            new ChatMessage(text, MessageRole.User) { IsQueued = isQueued };

        public static ChatMessage FromAssistant(string text) =>
            new ChatMessage(text, MessageRole.Assistant);

        public static ChatMessage FromAssistantPending(string placeholderText = "CAPCOM is thinking...") =>
            new ChatMessage(placeholderText, MessageRole.Assistant, isPending: true);

        public static ChatMessage FromSystem(string text) =>
            new ChatMessage(text, MessageRole.System);
    }
}
