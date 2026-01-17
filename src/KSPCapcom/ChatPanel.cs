using System;
using System.Collections.Generic;
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
        private bool _waitingForResponse;

        // Styles
        private GUIStyle _windowStyle;
        private GUIStyle _messageStyle;
        private GUIStyle _userMessageStyle;
        private GUIStyle _systemMessageStyle;
        private GUIStyle _inputStyle;
        private bool _stylesInitialized;

        // Input focus management
        private bool _focusInput;

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

        public ChatPanel() : this(new EchoResponder())
        {
        }

        public ChatPanel(IResponder responder)
        {
            _responder = responder ?? throw new ArgumentNullException(nameof(responder));
            _messages = new List<ChatMessage>();
            _isVisible = false;
            _stylesInitialized = false;
            _waitingForResponse = false;

            // Position window on the right side of the screen
            float x = Screen.width - DEFAULT_WIDTH - 50;
            float y = 100;
            _windowRect = new Rect(x, y, DEFAULT_WIDTH, DEFAULT_HEIGHT);

            // Add welcome message
            AddSystemMessage("CAPCOM online. How can I assist you, Flight?");

            CapcomCore.Log($"ChatPanel initialized with responder: {_responder.Name}");
        }

        /// <summary>
        /// Toggle the panel visibility.
        /// </summary>
        public void Toggle()
        {
            _isVisible = !_isVisible;
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

            _stylesInitialized = true;
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();

            // Messages area with scroll
            DrawMessagesArea();

            GUILayout.Space(4);

            // Input area
            DrawInputArea();

            GUILayout.EndVertical();

            // Make window draggable by the title bar
            GUI.DragWindow(new Rect(0, 0, _windowRect.width, 20));
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

            switch (message.Role)
            {
                case MessageRole.User:
                    style = _userMessageStyle;
                    prefix = "<b>You:</b> ";
                    alignRight = true;
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

            GUILayout.BeginHorizontal();

            if (alignRight)
            {
                GUILayout.FlexibleSpace();
            }

            // Draw message box
            GUILayout.BeginVertical(HighLogic.Skin.box, GUILayout.MaxWidth(_windowRect.width * 0.85f));
            GUILayout.Label($"<size=10><color=#888888>{timestamp}</color></size>", style);
            GUILayout.Label($"{prefix}{message.Text}", style);
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

            GUILayout.BeginHorizontal();

            // Disable input while waiting for response
            GUI.enabled = !_waitingForResponse;

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

            // Send button with waiting indicator
            string buttonText = _waitingForResponse ? "..." : "Send";
            if (GUILayout.Button(buttonText, GUILayout.Width(50), GUILayout.Height(inputHeight)))
            {
                if (!string.IsNullOrWhiteSpace(_inputText))
                {
                    shouldSend = true;
                }
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            // Process send after UI is drawn
            if (shouldSend)
            {
                SendMessage();
            }
        }

        private void SendMessage()
        {
            // Prevent sending while waiting for response
            if (_waitingForResponse)
            {
                return;
            }

            string text = _inputText.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // Add user message
            AddUserMessage(text);

            // Clear input
            _inputText = "";

            // Re-focus input after sending
            _focusInput = true;

            // Request response from responder
            ProcessUserMessage(text);
        }

        /// <summary>
        /// Process user message and generate response via the responder.
        /// </summary>
        private void ProcessUserMessage(string userText)
        {
            if (_waitingForResponse)
            {
                CapcomCore.LogWarning("Already waiting for response, ignoring");
                return;
            }

            _waitingForResponse = true;

            // Pass conversation history (responder can ignore if not needed)
            _responder.Respond(
                userText,
                _messages.AsReadOnly(),
                OnResponderComplete
            );
        }

        /// <summary>
        /// Callback when responder finishes generating a response.
        /// </summary>
        private void OnResponderComplete(ResponderResult result)
        {
            _waitingForResponse = false;

            if (result.Success)
            {
                AddAssistantMessage(result.Text);
            }
            else
            {
                // Show error as system message
                AddSystemMessage($"<color=#ff6666>Error: {result.ErrorMessage}</color>");
                CapcomCore.LogError($"Responder error: {result.ErrorMessage}");
            }

            ScrollToBottom();
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
    /// Represents a single chat message.
    /// </summary>
    public class ChatMessage
    {
        public string Text { get; }
        public MessageRole Role { get; }
        public DateTime Timestamp { get; }

        /// <summary>
        /// Convenience property for backward compatibility.
        /// </summary>
        public bool IsFromUser => Role == MessageRole.User;

        public ChatMessage(string text, MessageRole role)
        {
            Text = text;
            Role = role;
            Timestamp = DateTime.Now;
        }

        public static ChatMessage FromUser(string text) =>
            new ChatMessage(text, MessageRole.User);

        public static ChatMessage FromAssistant(string text) =>
            new ChatMessage(text, MessageRole.Assistant);

        public static ChatMessage FromSystem(string text) =>
            new ChatMessage(text, MessageRole.System);
    }
}
