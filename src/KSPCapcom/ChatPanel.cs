using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using KSPCapcom.Critique;
using KSPCapcom.Editor;
using KSPCapcom.LLM;
using KSPCapcom.Parsing;
using KSPCapcom.Responders;
using KSPCapcom.Validation;

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
    public partial class ChatPanel
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

        /// <summary>
        /// Canonical prompt for LKO ascent script generation.
        /// Fixed string ensures deterministic behavior for tests and transcripts.
        /// </summary>
        private const string ASCENT_SCRIPT_PROMPT = "Write a kOS script for LKO ascent for this craft";

        private Rect _windowRect;
        private bool _isVisible;
        private string _inputText = "";
        private Vector2 _scrollPosition;
        private readonly List<ChatMessage> _messages;
        private readonly IResponder _responder;

        // Pending message state
        private ChatMessage _pendingMessage;
        private readonly MessageQueue _messageQueue;
        private CancellationTokenSource _currentRequestCts;

        // Critique service
        private CritiqueService _critiqueService;

        // Settings and secrets
        private readonly CapcomSettings _settings;
        private readonly SecretStore _secrets;

        // Auto-scroll management
        private bool _shouldAutoScroll = true;
        private float _lastScrollViewHeight;
        private float _lastContentHeight;
        private bool _pendingScrollToBottom;
        private int _unseenMessageCount;

        // Request timing for generating indicator
        private DateTime _requestStartTime;

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
                _archivePathInput = _settings.KosArchivePath;
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
        /// Set the critique service for design critique functionality.
        /// </summary>
        public void SetCritiqueService(CritiqueService critiqueService)
        {
            _critiqueService = critiqueService;
        }

        /// <summary>
        /// Whether a response is currently being generated or pending messages are queued.
        /// </summary>
        private bool IsWaitingForResponse => _pendingMessage != null || _responder.IsBusy;

        /// <summary>
        /// Whether the critique button should be enabled.
        /// Requires: in editor, have critique service, have valid craft, not busy.
        /// </summary>
        private bool CanCritique()
        {
            if (_critiqueService == null) return false;
            if (IsWaitingForResponse) return false;
            if (!HighLogic.LoadedSceneIsEditor) return false;

            var monitor = EditorCraftMonitor.Instance;
            if (monitor == null) return false;

            var snapshot = monitor.CurrentSnapshot;
            var validation = _critiqueService.ValidateCraft(snapshot);
            return validation.IsValid;
        }

        /// <summary>
        /// Whether the ascent script button should be enabled.
        /// Requires: in editor, not busy.
        /// </summary>
        private bool CanWriteAscentScript()
        {
            if (IsWaitingForResponse) return false;
            if (!HighLogic.LoadedSceneIsEditor) return false;
            return true;
        }

        /// <summary>
        /// Get the current craft snapshot from the editor monitor.
        /// </summary>
        private EditorCraftSnapshot GetCurrentSnapshot()
        {
            var monitor = EditorCraftMonitor.Instance;
            return monitor?.CurrentSnapshot ?? EditorCraftSnapshot.Empty;
        }

        /// <summary>
        /// Toggle the panel visibility.
        /// </summary>
        public void Toggle()
        {
            _isVisible = !_isVisible;
            CapcomCore.Log($"Chat panel {(_isVisible ? "opened" : "closed")}");
            if (_isVisible)
            {
                _focusInputFrames = FOCUS_FRAME_COUNT;
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
                _focusInputFrames = FOCUS_FRAME_COUNT;
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

            _windowRect = GUILayout.Window(
                WINDOW_ID,
                _windowRect,
                DrawWindow,
                "CAPCOM",
                _windowStyle,
                GUILayout.MinWidth(MIN_WIDTH),
                GUILayout.MinHeight(MIN_HEIGHT)
            );

            ClampWindowToScreen();
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();

            // Show grounded mode indicator when active
            if (_settings != null && _settings.GroundedModeEnabled)
            {
                var groundedIndicatorStyle = new GUIStyle(HighLogic.Skin.label)
                {
                    fontSize = FONT_SIZE_SMALL,
                    alignment = TextAnchor.MiddleCenter
                };
                groundedIndicatorStyle.normal.textColor = COLOR_INFO;
                GUILayout.Label("Grounded Mode Active", groundedIndicatorStyle);
            }

            DrawSettingsArea();
            DrawMessagesArea();
            GUILayout.Space(4);
            DrawInputArea();

            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, _windowRect.width, 20));
        }

        #region Critique and Ascent Handlers

        private void OnCritiqueClick()
        {
            if (_critiqueService == null || IsWaitingForResponse) return;

            var monitor = EditorCraftMonitor.Instance;
            monitor?.ForceRefresh();

            var snapshot = GetCurrentSnapshot();
            var validation = _critiqueService.ValidateCraft(snapshot);
            if (!validation.IsValid)
            {
                AddSystemMessage(FormatWarning(validation.Reason));
                return;
            }

            var userMessage = ChatMessage.FromUser($"[Critique: {snapshot.CraftName}]");
            _messages.Add(userMessage);
            TrimMessageHistory();
            ScrollToBottom();
            CapcomCore.Log($"[User] Critique requested for {snapshot.CraftName}");

            _pendingMessage = ChatMessage.FromAssistantPending("Analyzing craft design...");
            _messages.Add(_pendingMessage);
            ScrollToBottom();

            _currentRequestCts = new CancellationTokenSource();
            _requestStartTime = DateTime.UtcNow;

            _critiqueService.RequestCritique(
                snapshot,
                _currentRequestCts.Token,
                OnCritiqueComplete,
                OnStreamChunk
            );
        }

        private void OnAscentScriptClick()
        {
            if (IsWaitingForResponse) return;

            var monitor = EditorCraftMonitor.Instance;
            monitor?.ForceRefresh();

            var snapshot = GetCurrentSnapshot();
            if (snapshot == null || snapshot.IsEmpty)
            {
                AddSystemMessage(FormatWarning("No craft metrics available - script will use default parameters"));
            }

            var userMessage = ChatMessage.FromUser($"[Ascent Script Request]");
            _messages.Add(userMessage);
            TrimMessageHistory();
            ScrollToBottom();
            CapcomCore.Log($"[User] Ascent script requested");

            _pendingMessage = ChatMessage.FromAssistantPending("Generating ascent script...");
            _messages.Add(_pendingMessage);
            ScrollToBottom();

            _currentRequestCts = new CancellationTokenSource();
            _requestStartTime = DateTime.UtcNow;

            _responder.Respond(
                ASCENT_SCRIPT_PROMPT,
                _messages.AsReadOnly(),
                _currentRequestCts.Token,
                OnResponderComplete,
                OnStreamChunk
            );
        }

        private void OnCritiqueComplete(ResponderResult result)
        {
            if (_pendingMessage != null)
            {
                if (result.Success)
                {
                    bool hasStreamedContent = !string.IsNullOrEmpty(_pendingMessage.Text)
                        && _pendingMessage.Text != "Analyzing craft design...";
                    bool hasFinalContent = !string.IsNullOrEmpty(result.Text);

                    if (hasStreamedContent)
                    {
                        _pendingMessage.Complete();
                    }
                    else if (hasFinalContent)
                    {
                        _pendingMessage.Complete(result.Text);
                    }
                    else
                    {
                        _messages.Remove(_pendingMessage);
                        AddSystemMessage(FormatWarning("Critique completed but response was empty. Please try again."));
                        CapcomCore.LogWarning("Critique: Request succeeded but received empty response");
                    }

                    ParseAndValidateMessage(_pendingMessage);
                    CapcomCore.Log($"[Assistant] Critique complete");
                }
                else
                {
                    _messages.Remove(_pendingMessage);
                    var elapsed = (DateTime.UtcNow - _requestStartTime).TotalSeconds;
                    var errorData = BuildErrorMessageData(result.Error, elapsed);
                    AddErrorMessage(errorData);
                    CapcomCore.LogError($"Critique error: {result.ErrorMessage}");
                }
                _pendingMessage = null;
            }

            _currentRequestCts?.Dispose();
            _currentRequestCts = null;
            ScrollToBottom();
        }

        #endregion

        #region Message Handling

        private void SendMessage()
        {
            string text = _inputText.Trim();
            if (string.IsNullOrEmpty(text)) return;

            bool willBeQueued = _responder.IsBusy || _pendingMessage != null;

            var userMessage = ChatMessage.FromUser(text, isQueued: willBeQueued);
            _messages.Add(userMessage);
            TrimMessageHistory();
            ScrollToBottom();
            CapcomCore.Log($"[User] {text}");

            _inputText = "";
            AddToHistory(text);
            _focusInputFrames = FOCUS_FRAME_COUNT;

            if (willBeQueued)
            {
                bool wasDropped = _messageQueue.Enqueue(new MessageRequest(
                    text,
                    new List<ChatMessage>(_messages).AsReadOnly(),
                    OnResponderComplete,
                    userMessage
                ));

                if (wasDropped)
                {
                    AddSystemMessage(FormatWarning("Queue full - oldest message dropped"));
                }

                CapcomCore.Log($"Message queued (queue size: {_messageQueue.Count})");
                return;
            }

            ProcessUserMessage(text);
        }

        private void ProcessUserMessage(string userText)
        {
            if (_pendingMessage != null)
            {
                CapcomCore.LogWarning("Already waiting for response, ignoring");
                return;
            }

            _pendingMessage = ChatMessage.FromAssistantPending();
            _messages.Add(_pendingMessage);
            ScrollToBottom();

            _currentRequestCts = new CancellationTokenSource();
            _requestStartTime = DateTime.UtcNow;

            _responder.Respond(
                userText,
                _messages.AsReadOnly(),
                _currentRequestCts.Token,
                OnResponderComplete,
                OnStreamChunk
            );
        }

        private void OnStreamChunk(string accumulatedText)
        {
            if (_pendingMessage != null)
            {
                _pendingMessage.UpdateText(accumulatedText);
                ScrollToBottomIfAutoScroll();
            }
        }

        private void OnResponderComplete(ResponderResult result)
        {
            if (_pendingMessage != null)
            {
                if (result.Success)
                {
                    if (string.IsNullOrEmpty(_pendingMessage.Text) || _pendingMessage.Text == "CAPCOM is thinking...")
                    {
                        _pendingMessage.Complete(result.Text);
                    }
                    else
                    {
                        _pendingMessage.Complete();
                    }

                    ParseAndValidateMessage(_pendingMessage);
                    CapcomCore.Log($"[Assistant] {result.Text}");
                }
                else
                {
                    _messages.Remove(_pendingMessage);
                    var elapsed = (DateTime.UtcNow - _requestStartTime).TotalSeconds;
                    var errorData = BuildErrorMessageData(result.Error, elapsed);
                    AddErrorMessage(errorData);
                    CapcomCore.LogError($"Responder error: {result.ErrorMessage}");
                }
                _pendingMessage = null;
            }
            else if (result.Success)
            {
                AddAssistantMessage(result.Text);
            }
            else
            {
                var elapsed = (DateTime.UtcNow - _requestStartTime).TotalSeconds;
                var errorData = BuildErrorMessageData(result.Error, elapsed);
                AddErrorMessage(errorData);
                CapcomCore.LogError($"Responder error: {result.ErrorMessage}");
            }

            _currentRequestCts?.Dispose();
            _currentRequestCts = null;

            ScrollToBottom();
            ProcessNextQueuedMessage();
        }

        private void ParseAndValidateMessage(ChatMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.Text)) return;

            try
            {
                var parsed = _codeBlockParser.Parse(message.Text);
                message.SetParsedContent(parsed);

                if (parsed.HasCodeBlocks)
                {
                    CapcomCore.Log($"ChatPanel: Parsed {parsed.CodeBlockCount} code block(s)");

                    var syntaxChecker = new KosSyntaxChecker();
                    foreach (var segment in parsed.Segments)
                    {
                        if (segment is CodeBlockSegment codeBlock && codeBlock.IsKosLikely)
                        {
                            codeBlock.SyntaxResult = syntaxChecker.Check(codeBlock.RawCode);
                            if (codeBlock.SyntaxResult.HasIssues)
                            {
                                CapcomCore.LogWarning($"ChatPanel: Code block has {codeBlock.SyntaxResult.Issues.Count} syntax issue(s)");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CapcomCore.LogWarning($"ChatPanel: Failed to parse message - {ex.Message}");
            }
        }

        private void ProcessNextQueuedMessage()
        {
            if (_responder.IsBusy || _pendingMessage != null) return;

            var next = _messageQueue.Dequeue();
            if (next != null)
            {
                next.UserChatMessage?.MarkDequeued();
                CapcomCore.Log($"Processing queued message (remaining: {_messageQueue.Count})");
                ProcessUserMessage(next.UserMessage);
            }
        }

        public void CancelCurrentRequest()
        {
            _currentRequestCts?.Cancel();
            _responder.Cancel();
            _messageQueue.Clear();

            if (_pendingMessage != null)
            {
                _messages.Remove(_pendingMessage);
                _pendingMessage = null;
                AddSystemMessage(FormatWarning("Request cancelled"));
            }
        }

        #endregion

        #region Error Handling

        private ErrorMessageData BuildErrorMessageData(LLMError error, double elapsedSeconds)
        {
            var data = new ErrorMessageData
            {
                IsCancellation = error?.Type == LLMErrorType.Cancelled,
                IsRetryable = error?.IsRetryable ?? false,
            };

            if (data.IsCancellation)
            {
                data.ShortMessage = "Request cancelled";
                data.HasDetails = false;
                return data;
            }

            data.ShortMessage = error != null
                ? ErrorMapper.GetUserFriendlyMessage(error)
                : "An error occurred";
            data.HasDetails = error != null && error.Type != LLMErrorType.None;

            if (data.HasDetails)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Type: {error.Type}");
                if (!string.IsNullOrEmpty(error.ProviderCode))
                    sb.AppendLine($"Code: {SanitizeProviderCode(error.ProviderCode)}");
                sb.AppendLine($"Timing: {elapsedSeconds:F1}s");
                if (error.IsRetryable && error.SuggestedRetryDelayMs > 0)
                    sb.AppendLine($"Suggested retry: {error.SuggestedRetryDelayMs}ms");
                data.TechnicalDetails = sb.ToString().TrimEnd();
            }

            return data;
        }

        private string SanitizeProviderCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return code;
            if (code.Length > 20 && !code.Contains(" ")) return "[redacted]";
            return code;
        }

        #endregion

        #region Message Management

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
            else
            {
                _unseenMessageCount++;
            }
            CapcomCore.Log($"[Assistant] {text}");
        }

        public void AddSystemMessage(string text)
        {
            _messages.Add(ChatMessage.FromSystem(text));
            TrimMessageHistory();
            if (_shouldAutoScroll)
            {
                ScrollToBottom();
            }
            else
            {
                _unseenMessageCount++;
            }
            CapcomCore.Log($"[System] {text}");
        }

        private void TrimMessageHistory()
        {
            while (_messages.Count > MAX_MESSAGES)
            {
                _messages.RemoveAt(0);
            }
        }

        public void ClearHistory()
        {
            _messages.Clear();
            _errorMessageData.Clear();
            _expandedErrorIds.Clear();
            _scrollPosition = Vector2.zero;
            _shouldAutoScroll = true;
            _unseenMessageCount = 0;
            CapcomCore.Log("Chat history cleared");
        }

        #endregion

        #region Scrolling

        public void ScrollToBottom()
        {
            _pendingScrollToBottom = true;
        }

        private void ScrollToBottomIfAutoScroll()
        {
            if (_shouldAutoScroll)
            {
                _pendingScrollToBottom = true;
            }
        }

        #endregion

        private void ClampWindowToScreen()
        {
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0, Screen.width - _windowRect.width);
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0, Screen.height - _windowRect.height);
        }
    }
}
