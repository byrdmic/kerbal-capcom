using System;
using System.Collections.Generic;
using UnityEngine;
using KSPCapcom.Responders;

namespace KSPCapcom
{
    /// <summary>
    /// Chat panel window for CAPCOM communication.
    /// Uses Unity IMGUI for rendering.
    /// </summary>
    public class ChatPanel
    {
        private const int WINDOW_ID = 84729; // Unique ID for the window
        private const float DEFAULT_WIDTH = 350f;
        private const float DEFAULT_HEIGHT = 400f;
        private const float MIN_WIDTH = 280f;
        private const float MIN_HEIGHT = 200f;

        private Rect _windowRect;
        private bool _isVisible;
        private string _inputText = "";
        private Vector2 _scrollPosition;
        private readonly List<ChatMessage> _messages;
        private readonly IResponder _responder;
        private bool _waitingForResponse;
        private GUIStyle _windowStyle;
        private GUIStyle _messageStyle;
        private GUIStyle _userMessageStyle;
        private GUIStyle _systemMessageStyle;
        private GUIStyle _inputStyle;
        private bool _stylesInitialized;
        private bool _focusInput;

        /// <summary>
        /// Whether the chat panel is currently visible.
        /// </summary>
        public bool IsVisible => _isVisible;

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

            // Position window in the right side of the screen
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
            }
        }

        /// <summary>
        /// Show the panel.
        /// </summary>
        public void Show()
        {
            _isVisible = true;
            _focusInput = true;
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

            // Input field style
            _inputStyle = new GUIStyle(HighLogic.Skin.textField)
            {
                padding = new RectOffset(6, 6, 4, 4),
                wordWrap = false
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
            _scrollPosition = GUILayout.BeginScrollView(
                _scrollPosition,
                false,
                true,
                GUILayout.ExpandHeight(true)
            );

            foreach (var message in _messages)
            {
                DrawMessage(message);
            }

            GUILayout.EndScrollView();
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
            GUILayout.BeginHorizontal();

            // Disable input while waiting for response
            GUI.enabled = !_waitingForResponse;

            // Set focus to input field if needed
            if (_focusInput)
            {
                GUI.FocusControl("ChatInput");
                _focusInput = false;
            }

            // Text input
            GUI.SetNextControlName("ChatInput");
            _inputText = GUILayout.TextField(_inputText, _inputStyle, GUILayout.ExpandWidth(true));

            // Check for Enter key to send
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                if (GUI.GetNameOfFocusedControl() == "ChatInput" && !string.IsNullOrWhiteSpace(_inputText))
                {
                    SendMessage();
                    Event.current.Use();
                }
            }

            // Send button with waiting indicator
            string buttonText = _waitingForResponse ? "..." : "Send";
            if (GUILayout.Button(buttonText, GUILayout.Width(50)))
            {
                if (!string.IsNullOrWhiteSpace(_inputText))
                {
                    SendMessage();
                }
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
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

            // Request response from responder
            ProcessUserMessage(text);

            // Scroll to bottom
            ScrollToBottom();
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

        private void AddUserMessage(string text)
        {
            _messages.Add(ChatMessage.FromUser(text));
            CapcomCore.Log($"[User] {text}");
        }

        private void AddAssistantMessage(string text)
        {
            _messages.Add(ChatMessage.FromAssistant(text));
            CapcomCore.Log($"[Assistant] {text}");
        }

        private void AddSystemMessage(string text)
        {
            _messages.Add(ChatMessage.FromSystem(text));
            CapcomCore.Log($"[System] {text}");
        }

        private void ScrollToBottom()
        {
            _scrollPosition = new Vector2(0, float.MaxValue);
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
