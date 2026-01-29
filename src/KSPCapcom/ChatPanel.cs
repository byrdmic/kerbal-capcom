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
using KSPCapcom.UI;
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

        /// <summary>
        /// Canonical prompt for LKO ascent script generation.
        /// Fixed string ensures deterministic behavior for tests and transcripts.
        /// </summary>
        private const string ASCENT_SCRIPT_PROMPT = "Write a kOS script for LKO ascent for this craft";

        #region Style Constants

        // === COLORS ===
        private static readonly Color COLOR_SUCCESS = new Color(0.6f, 0.9f, 0.6f);   // Green
        private static readonly Color COLOR_WARNING = new Color(1f, 0.8f, 0.4f);      // Orange
        private static readonly Color COLOR_ERROR = new Color(1f, 0.4f, 0.4f);        // Red
        private static readonly Color COLOR_INFO = new Color(0.4f, 0.8f, 1.0f);       // Cyan

        private static readonly Color COLOR_USER_MESSAGE = new Color(0.9f, 0.9f, 0.9f);
        private static readonly Color COLOR_ASSISTANT_MESSAGE = new Color(0.6f, 0.9f, 0.6f);
        private static readonly Color COLOR_QUEUED_MESSAGE = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color COLOR_MUTED = new Color(0.53f, 0.53f, 0.53f);

        private static readonly Color COLOR_CANCEL_NORMAL = new Color(1f, 0.6f, 0.4f);
        private static readonly Color COLOR_CANCEL_HOVER = new Color(1f, 0.7f, 0.5f);
        private static readonly Color COLOR_ACTION_NORMAL = new Color(0.4f, 0.8f, 1.0f);
        private static readonly Color COLOR_ACTION_HOVER = new Color(0.6f, 0.9f, 1.0f);

        // Hex strings for inline rich text
        private const string HEX_MUTED = "#888888";
        private const string HEX_SUCCESS = "#99e699";
        private const string HEX_WARNING = "#ffaa00";
        private const string HEX_ERROR = "#ff6666";

        // === FONT SIZES ===
        private const int FONT_SIZE_SMALL = 10;
        private const int FONT_SIZE_SECONDARY = 11;

        // === SPACING & PADDING ===
        private static readonly RectOffset PADDING_WINDOW = new RectOffset(8, 8, 20, 8);
        private static readonly RectOffset PADDING_MESSAGE = new RectOffset(8, 8, 4, 4);
        private static readonly RectOffset MARGIN_MESSAGE = new RectOffset(0, 0, 2, 2);
        private static readonly RectOffset PADDING_INPUT = new RectOffset(6, 6, 4, 4);
        private static readonly RectOffset PADDING_BUTTON = new RectOffset(8, 8, 4, 4);
        private static readonly RectOffset PADDING_BOX = new RectOffset(8, 8, 8, 8);

        #endregion

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

        // Critique service
        private CritiqueService _critiqueService;

        // Styles
        private GUIStyle _windowStyle;
        private GUIStyle _messageStyle;
        private GUIStyle _userMessageStyle;
        private GUIStyle _systemMessageStyle;
        private GUIStyle _inputStyle;
        private bool _stylesInitialized;

        // Input focus management
        private int _focusInputFrames;
        private const int FOCUS_FRAME_COUNT = 3;

        // Command history navigation
        private const int MAX_COMMAND_HISTORY = 50;
        private readonly List<string> _commandHistory = new List<string>();
        private int _historyIndex = -1;           // -1 = not navigating, 0..N = position in history
        private string _workingCopy = "";         // Preserved draft when user starts navigating

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
        private GUIStyle _critiqueButtonStyle;
        private GUIStyle _jumpToLatestStyle;

        // Auto-scroll management
        private bool _shouldAutoScroll = true;
        private float _lastScrollViewHeight;
        private float _lastContentHeight;
        private bool _pendingScrollToBottom;
        private int _unseenMessageCount;

        // Request timing for generating indicator
        private DateTime _requestStartTime;

        // Error message tracking for expandable details
        private readonly Dictionary<int, ErrorMessageData> _errorMessageData = new Dictionary<int, ErrorMessageData>();
        private readonly HashSet<int> _expandedErrorIds = new HashSet<int>();
        private int _nextErrorId = 0;

        // Code block parsing and rendering
        private readonly CodeBlockParser _codeBlockParser = new CodeBlockParser();
        private readonly ScriptCardRenderer _scriptCardRenderer = new ScriptCardRenderer();

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
        /// Check if any settings text field currently has focus.
        /// </summary>
        private bool IsSettingsFieldFocused()
        {
            string focused = GUI.GetNameOfFocusedControl();
            return focused == "SettingsModel" ||
                   focused == "SettingsApiKey" ||
                   focused == "SettingsEndpoint";
        }

        /// <summary>
        /// Whether the critique button should be enabled.
        /// Requires: in editor, have critique service, have valid craft, not busy.
        /// </summary>
        private bool CanCritique()
        {
            // Must have critique service configured
            if (_critiqueService == null)
            {
                return false;
            }

            // Can't critique while busy
            if (IsWaitingForResponse)
            {
                return false;
            }

            // Must be in editor scene
            if (!HighLogic.LoadedSceneIsEditor)
            {
                return false;
            }

            // Must have a valid craft
            var monitor = EditorCraftMonitor.Instance;
            if (monitor == null)
            {
                return false;
            }

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
            // Can't generate while busy
            if (IsWaitingForResponse)
            {
                return false;
            }

            // Must be in editor scene
            if (!HighLogic.LoadedSceneIsEditor)
            {
                return false;
            }

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
        /// Handle click on the Critique button.
        /// </summary>
        private void OnCritiqueClick()
        {
            if (_critiqueService == null || IsWaitingForResponse)
            {
                return;
            }

            // Force refresh to get latest state
            var monitor = EditorCraftMonitor.Instance;
            monitor?.ForceRefresh();

            var snapshot = GetCurrentSnapshot();

            // Validate before proceeding
            var validation = _critiqueService.ValidateCraft(snapshot);
            if (!validation.IsValid)
            {
                AddSystemMessage(FormatWarning(validation.Reason));
                return;
            }

            // Add a synthetic user message to show in chat
            var userMessage = ChatMessage.FromUser($"[Critique: {snapshot.CraftName}]");
            _messages.Add(userMessage);
            TrimMessageHistory();
            ScrollToBottom();
            CapcomCore.Log($"[User] Critique requested for {snapshot.CraftName}");

            // Add pending message for visual feedback
            _pendingMessage = ChatMessage.FromAssistantPending("Analyzing craft design...");
            _messages.Add(_pendingMessage);
            ScrollToBottom();

            // Create cancellation token and track start time
            _currentRequestCts = new CancellationTokenSource();
            _requestStartTime = DateTime.UtcNow;

            // Request critique
            _critiqueService.RequestCritique(
                snapshot,
                _currentRequestCts.Token,
                OnCritiqueComplete,
                OnStreamChunk
            );
        }

        /// <summary>
        /// Handle click on the Ascent button.
        /// </summary>
        private void OnAscentScriptClick()
        {
            if (IsWaitingForResponse)
            {
                return;
            }

            // Force refresh to get latest state
            var monitor = EditorCraftMonitor.Instance;
            monitor?.ForceRefresh();

            var snapshot = GetCurrentSnapshot();

            // Warn if no craft metrics available, but proceed anyway
            if (snapshot == null || snapshot.IsEmpty)
            {
                AddSystemMessage(FormatWarning("No craft metrics available - script will use default parameters"));
            }

            // Add synthetic user message to show in chat
            var userMessage = ChatMessage.FromUser($"[Ascent Script Request]");
            _messages.Add(userMessage);
            TrimMessageHistory();
            ScrollToBottom();
            CapcomCore.Log($"[User] Ascent script requested");

            // Add pending message for visual feedback
            _pendingMessage = ChatMessage.FromAssistantPending("Generating ascent script...");
            _messages.Add(_pendingMessage);
            ScrollToBottom();

            // Create cancellation token and track start time
            _currentRequestCts = new CancellationTokenSource();
            _requestStartTime = DateTime.UtcNow;

            // Use the main responder with the canonical prompt
            _responder.Respond(
                ASCENT_SCRIPT_PROMPT,
                _messages.AsReadOnly(),
                _currentRequestCts.Token,
                OnResponderComplete,
                OnStreamChunk
            );
        }

        /// <summary>
        /// Callback when critique response is ready.
        /// </summary>
        private void OnCritiqueComplete(ResponderResult result)
        {
            // Complete the pending message
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
                        // Request succeeded but no content - show error instead of blank
                        _messages.Remove(_pendingMessage);
                        AddSystemMessage(FormatWarning("Critique completed but response was empty. Please try again."));
                        CapcomCore.LogWarning("Critique: Request succeeded but received empty response");
                    }

                    // Parse the message for code blocks (only after completion)
                    ParseAndValidateMessage(_pendingMessage);

                    CapcomCore.Log($"[Assistant] Critique complete");
                }
                else
                {
                    // Replace pending with error (with expandable details)
                    _messages.Remove(_pendingMessage);
                    var elapsed = (DateTime.UtcNow - _requestStartTime).TotalSeconds;
                    var errorData = BuildErrorMessageData(result.Error, elapsed);
                    AddErrorMessage(errorData);
                    CapcomCore.LogError($"Critique error: {result.ErrorMessage}");
                }
                _pendingMessage = null;
            }

            // Cleanup
            _currentRequestCts?.Dispose();
            _currentRequestCts = null;

            ScrollToBottom();
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
                padding = PADDING_WINDOW
            };

            // Base message style
            _messageStyle = new GUIStyle(HighLogic.Skin.label)
            {
                wordWrap = true,
                richText = true,
                alignment = TextAnchor.UpperLeft,
                padding = PADDING_MESSAGE,
                margin = MARGIN_MESSAGE
            };

            // User message style (right-aligned, different color)
            _userMessageStyle = new GUIStyle(_messageStyle);
            _userMessageStyle.normal.textColor = COLOR_USER_MESSAGE;

            // System/CAPCOM message style
            _systemMessageStyle = new GUIStyle(_messageStyle);
            _systemMessageStyle.normal.textColor = COLOR_ASSISTANT_MESSAGE;

            // Input field style - use TextArea style for multiline support
            _inputStyle = new GUIStyle(HighLogic.Skin.textArea)
            {
                padding = PADDING_INPUT,
                wordWrap = true
            };

            // Settings header style (collapsible button)
            _settingsHeaderStyle = new GUIStyle(HighLogic.Skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = PADDING_BUTTON
            };

            // Settings box style
            _settingsBoxStyle = new GUIStyle(HighLogic.Skin.box)
            {
                padding = PADDING_BOX
            };

            // Validation error style (red text)
            _validationErrorStyle = new GUIStyle(HighLogic.Skin.label)
            {
                wordWrap = true,
                fontSize = FONT_SIZE_SECONDARY
            };
            _validationErrorStyle.normal.textColor = COLOR_ERROR;

            // Status label style (for API key status)
            _statusLabelStyle = new GUIStyle(HighLogic.Skin.label)
            {
                fontSize = FONT_SIZE_SECONDARY
            };

            // Cancel/Stop button style (orange-red to indicate stop action)
            _cancelButtonStyle = new GUIStyle(HighLogic.Skin.button);
            _cancelButtonStyle.normal.textColor = COLOR_CANCEL_NORMAL;
            _cancelButtonStyle.hover.textColor = COLOR_CANCEL_HOVER;

            // Queue count indicator style (small, muted)
            _queueCountStyle = new GUIStyle(HighLogic.Skin.label)
            {
                fontSize = FONT_SIZE_SMALL,
                alignment = TextAnchor.MiddleCenter
            };
            _queueCountStyle.normal.textColor = new Color(0.8f, 0.8f, 0.6f); // Unique yellow-gray for queue count

            // Queued message style (dimmed)
            _queuedMessageStyle = new GUIStyle(_userMessageStyle);
            _queuedMessageStyle.normal.textColor = COLOR_QUEUED_MESSAGE;

            // Critique button style (distinct color to indicate special action)
            _critiqueButtonStyle = new GUIStyle(HighLogic.Skin.button);
            _critiqueButtonStyle.normal.textColor = COLOR_ACTION_NORMAL;
            _critiqueButtonStyle.hover.textColor = COLOR_ACTION_HOVER;

            // Jump to latest button style
            _jumpToLatestStyle = new GUIStyle(HighLogic.Skin.button)
            {
                fontSize = FONT_SIZE_SECONDARY,
                padding = PADDING_BUTTON,
                alignment = TextAnchor.MiddleCenter
            };
            _jumpToLatestStyle.normal.textColor = new Color(0.8f, 0.9f, 1.0f); // Light blue for jump button

            _stylesInitialized = true;
        }

        #region Rich Text Helpers

        private static string FormatMuted(string text) =>
            $"<size={FONT_SIZE_SMALL}><color={HEX_MUTED}>{text}</color></size>";

        private static string FormatSuccess(string text) =>
            $"<color={HEX_SUCCESS}>{text}</color>";

        private static string FormatWarning(string text) =>
            $"<color={HEX_WARNING}>{text}</color>";

        private static string FormatError(string text) =>
            $"<color={HEX_ERROR}>{text}</color>";

        private static string FormatBadge(string badge, string hexColor) =>
            $" <color={hexColor}>[{badge}]</color>";

        private static string FormatTimestamp(DateTime timestamp) =>
            FormatMuted(timestamp.ToString("HH:mm"));

        #endregion

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

                // Grounded mode row
                GUILayout.BeginHorizontal();
                GUILayout.Label("Grounded:", GUILayout.Width(60));

                bool wasGrounded = _settings.GroundedModeEnabled;
                bool newGrounded = GUILayout.Toggle(_settings.GroundedModeEnabled,
                    _settings.GroundedModeEnabled ? "On" : "Off",
                    HighLogic.Skin.button, GUILayout.Width(40));

                if (newGrounded != wasGrounded)
                {
                    _settings.GroundedModeEnabled = newGrounded;
                    CapcomCore.Log($"Grounded mode: {(newGrounded ? "On" : "Off")}");
                }

                // Status indicator with color
                var groundedStatusStyle = new GUIStyle(_statusLabelStyle);
                groundedStatusStyle.normal.textColor = _settings.GroundedModeEnabled
                    ? COLOR_INFO
                    : COLOR_MUTED;
                GUILayout.Label(_settings.GroundedModeEnabled ? "(strict kOS validation)" : "(flexible)", groundedStatusStyle);

                GUILayout.EndHorizontal();

                GUILayout.Space(4);

                // Model row: "Model:" label + text field
                GUILayout.BeginHorizontal();
                GUILayout.Label("Model:", GUILayout.Width(60));

                GUI.SetNextControlName("SettingsModel");
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
                        configuredStyle.normal.textColor = COLOR_SUCCESS;
                        GUILayout.Label("configured", configuredStyle);
                    }
                    else
                    {
                        var notConfiguredStyle = new GUIStyle(_statusLabelStyle);
                        notConfiguredStyle.normal.textColor = COLOR_CANCEL_NORMAL;
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
                        GUI.SetNextControlName("SettingsApiKey");
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

                GUI.SetNextControlName("SettingsEndpoint");
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
                    // Render as script card with copy feedback callback
                    _scriptCardRenderer.DrawScriptCard(codeBlock, maxCardWidth, OnScriptCopyResult);
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
                var elapsed = (DateTime.UtcNow - _requestStartTime).TotalSeconds;
                var statusText = $"Generating... ({elapsed:F0}s)";
                GUILayout.Label(statusText, _statusLabelStyle);
            }

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

            // Ascent button - only enabled in editor when not busy
            bool canAscent = CanWriteAscentScript();
            GUI.enabled = canAscent;
            if (GUILayout.Button("Ascent", _critiqueButtonStyle, GUILayout.Width(50), GUILayout.Height(inputHeight)))
            {
                OnAscentScriptClick();
            }
            GUI.enabled = true;

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

            // Add to command history
            AddToHistory(text);

            // Re-focus input after sending
            _focusInputFrames = FOCUS_FRAME_COUNT;

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
                    AddSystemMessage(FormatWarning("Queue full - oldest message dropped"));
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

            // Create cancellation token for this request (M2 ready) and track start time
            _currentRequestCts = new CancellationTokenSource();
            _requestStartTime = DateTime.UtcNow;

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
                ScrollToBottomIfAutoScroll();
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

                    // Parse the message for code blocks (only after completion)
                    ParseAndValidateMessage(_pendingMessage);

                    CapcomCore.Log($"[Assistant] {result.Text}");
                }
                else
                {
                    // Replace pending with error message (with expandable details)
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
                // No pending message (shouldn't happen, but handle gracefully)
                AddAssistantMessage(result.Text);
            }
            else
            {
                // No pending message error case
                var elapsed = (DateTime.UtcNow - _requestStartTime).TotalSeconds;
                var errorData = BuildErrorMessageData(result.Error, elapsed);
                AddErrorMessage(errorData);
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
        /// Parse a completed message for code blocks and run syntax validation.
        /// </summary>
        private void ParseAndValidateMessage(ChatMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.Text))
            {
                return;
            }

            try
            {
                // Parse the message content
                var parsed = _codeBlockParser.Parse(message.Text);
                message.SetParsedContent(parsed);

                if (parsed.HasCodeBlocks)
                {
                    CapcomCore.Log($"ChatPanel: Parsed {parsed.CodeBlockCount} code block(s)");

                    // Run syntax validation on each kOS code block
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
                // Never break chat flow due to parsing failure
                CapcomCore.LogWarning($"ChatPanel: Failed to parse message - {ex.Message}");
            }
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
                AddSystemMessage(FormatWarning("Request cancelled"));
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
                return HEX_WARNING;
            }
            // Other errors use red
            return HEX_ERROR;
        }

        #region Error Message Rendering

        /// <summary>
        /// Build error message data from an LLMError for UI rendering.
        /// </summary>
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
                data.HasDetails = false;  // No details for user-initiated cancel
                return data;
            }

            data.ShortMessage = error != null
                ? ErrorMapper.GetUserFriendlyMessage(error)
                : "An error occurred";
            data.HasDetails = error != null && error.Type != LLMErrorType.None;

            // Build sanitized technical details
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

        /// <summary>
        /// Sanitize provider error codes to prevent leaking secrets.
        /// </summary>
        private string SanitizeProviderCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return code;

            // Redact anything that looks like a token/key (long alphanumeric without spaces)
            if (code.Length > 20 && !code.Contains(" "))
                return "[redacted]";

            return code;
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

        #endregion

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
            else
            {
                _unseenMessageCount++;
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
            else
            {
                _unseenMessageCount++;
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
            _errorMessageData.Clear();
            _expandedErrorIds.Clear();
            _scrollPosition = Vector2.zero;
            _shouldAutoScroll = true;
            _unseenMessageCount = 0;
            CapcomCore.Log("Chat history cleared");
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

        /// <summary>
        /// Force scroll to the bottom of the message history.
        /// Note: This does NOT re-engage auto-scroll. Auto-scroll re-engages
        /// when the user manually scrolls back to the bottom.
        /// </summary>
        public void ScrollToBottom()
        {
            _pendingScrollToBottom = true;
            // Note: Do NOT set _shouldAutoScroll = true here.
            // Auto-scroll re-engages when user scrolls to bottom (detected in DrawMessagesArea).
        }

        /// <summary>
        /// Scroll to bottom only if auto-scroll is currently engaged.
        /// Use this during streaming to respect user's scroll position.
        /// </summary>
        private void ScrollToBottomIfAutoScroll()
        {
            if (_shouldAutoScroll)
            {
                _pendingScrollToBottom = true;
            }
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
        /// Whether this message is an error message with expandable details.
        /// </summary>
        public bool IsErrorMessage { get; set; }

        /// <summary>
        /// Unique identifier for error messages (used for detail expansion tracking).
        /// </summary>
        public int ErrorId { get; set; }

        /// <summary>
        /// Parsed content with code blocks and prose segments (set after completion).
        /// </summary>
        public ParsedMessageContent ParsedContent { get; private set; }

        /// <summary>
        /// Whether this message contains code blocks.
        /// </summary>
        public bool HasCodeBlocks => ParsedContent?.HasCodeBlocks ?? false;

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

        /// <summary>
        /// Set the parsed content for this message (called after completion).
        /// </summary>
        public void SetParsedContent(ParsedMessageContent content)
        {
            ParsedContent = content;
        }

        public static ChatMessage FromUser(string text, bool isQueued = false) =>
            new ChatMessage(text, MessageRole.User) { IsQueued = isQueued };

        public static ChatMessage FromAssistant(string text) =>
            new ChatMessage(text, MessageRole.Assistant);

        public static ChatMessage FromAssistantPending(string placeholderText = "CAPCOM is thinking...") =>
            new ChatMessage(placeholderText, MessageRole.Assistant, isPending: true);

        public static ChatMessage FromSystem(string text) =>
            new ChatMessage(text, MessageRole.System);

        public static ChatMessage FromError(string text, int errorId) =>
            new ChatMessage(text, MessageRole.System) { IsErrorMessage = true, ErrorId = errorId };
    }

    /// <summary>
    /// Data for rendering error messages with optional expandable details.
    /// </summary>
    public struct ErrorMessageData
    {
        /// <summary>Short, user-facing error headline.</summary>
        public string ShortMessage;

        /// <summary>Technical details (error type, timing, provider code).</summary>
        public string TechnicalDetails;

        /// <summary>Whether the error is retryable (affects color: orange vs red).</summary>
        public bool IsRetryable;

        /// <summary>Whether this was a user-initiated cancellation.</summary>
        public bool IsCancellation;

        /// <summary>Whether technical details are available.</summary>
        public bool HasDetails;
    }
}
