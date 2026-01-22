using System;
using System.Reflection;
using UnityEngine;
using KSPCapcom.Editor;
using KSPCapcom.LLM;
using KSPCapcom.LLM.OpenAI;
using KSPCapcom.Responders;

namespace KSPCapcom
{
    /// <summary>
    /// Main bootstrap class for KSP CAPCOM mod.
    /// Loads in both Editor (VAB/SPH) and Flight scenes.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.FlightAndEditor, once: false)]
    public class CapcomCore : MonoBehaviour
    {
        private const string LOG_PREFIX = "[KSPCapcom]";

        private static CapcomCore _instance;
        public static CapcomCore Instance => _instance;

        // Version logging state
        private static bool _versionLogged = false;

        // Scene tracking for logging transitions
        private static GameScenes _lastLoggedScene = GameScenes.LOADING;

        private ToolbarButton _toolbarButton;
        private ChatPanel _chatPanel;
        private CapcomSettings _settings;
        private SecretStore _secrets;
        private EditorCraftMonitor _editorMonitor;

        /// <summary>
        /// Current CAPCOM settings.
        /// </summary>
        public CapcomSettings Settings => _settings;

        /// <summary>
        /// Whether the chat panel is currently visible.
        /// </summary>
        public bool IsChatVisible => _chatPanel?.IsVisible ?? false;

        private void Awake()
        {
            // Ensure singleton - prevent duplicates across scene loads
            if (_instance != null && _instance != this)
            {
                Log("Duplicate instance detected, destroying self");
                Destroy(this);
                return;
            }

            _instance = this;

            // Initialize telemetry logging
            TelemetryConnector.LogAction = Log;

            // Log version once on first load
            if (!_versionLogged)
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                Log($"KSP CAPCOM v{version} loaded");
                _versionLogged = true;
            }

            Log("Bootstrap Awake()");
        }

        private void Start()
        {
            Log("Bootstrap Start()");

            // Initialize components
            _settings = new CapcomSettings();
            _secrets = new SecretStore();
            _secrets.Load();

            // Create OpenAI connector with API key and model from settings
            var openAIConnector = new OpenAIConnector(
                getApiKey: () => _secrets.ApiKey,
                getModel: () => _settings.Model
            );

            // Wrap with retry logic (off by default)
            var retryingConnector = new RetryingConnector(
                openAIConnector,
                () => _settings.RetryEnabled,
                () => _settings.MaxRetries
            );

            // Wrap with telemetry logging (outermost to capture total duration including retries)
            var connector = new TelemetryConnector(retryingConnector);

            // Create prompt builder with settings reference
            // This builder generates system prompts with CAPCOM tone and Teach/Do mode switching
            var promptBuilder = new PromptBuilder(() => _settings);

            // Create LLM responder wrapping the connector and prompt builder
            var responder = new LLMResponder(connector, promptBuilder);

            // Create chat panel with LLM responder and secrets
            _chatPanel = new ChatPanel(responder, _settings, _secrets);
            _toolbarButton = new ToolbarButton(OnToolbarToggle);

            // Log scene changes
            GameScenes currentScene = HighLogic.LoadedScene;
            if (_lastLoggedScene != GameScenes.LOADING && _lastLoggedScene != currentScene)
            {
                Log($"Scene changed: {_lastLoggedScene} -> {currentScene}");
            }
            else if (_lastLoggedScene == GameScenes.LOADING)
            {
                Log($"Loaded in scene: {currentScene}");
            }
            _lastLoggedScene = currentScene;

            // Initialize editor craft monitor when in VAB/SPH
            if (HighLogic.LoadedSceneIsEditor)
            {
                InitializeEditorMonitor();
            }
        }

        /// <summary>
        /// Initialize the EditorCraftMonitor for VAB/SPH scenes.
        /// </summary>
        private void InitializeEditorMonitor()
        {
            if (_editorMonitor != null)
            {
                return;
            }

            _editorMonitor = gameObject.AddComponent<EditorCraftMonitor>();
            Log("EditorCraftMonitor initialized");
        }

        private void OnDestroy()
        {
            Log("OnDestroy - cleaning up");

            // Clean up toolbar button to prevent duplicates
            _toolbarButton?.Destroy();
            _toolbarButton = null;

            _chatPanel = null;

            // EditorCraftMonitor is a component on this GameObject,
            // so it will be destroyed automatically when this is destroyed.
            _editorMonitor = null;

            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnGUI()
        {
            // Render the chat panel if visible
            _chatPanel?.OnGUI();
        }

        /// <summary>
        /// Called when toolbar button is clicked.
        /// </summary>
        private void OnToolbarToggle()
        {
            if (_chatPanel != null)
            {
                _chatPanel.Toggle();
                // Log removed - ChatPanel.Toggle() now handles logging
            }
        }

        /// <summary>
        /// Toggle the chat panel visibility (can be called externally).
        /// </summary>
        public void ToggleChat()
        {
            OnToolbarToggle();
        }

        /// <summary>
        /// Log a message with the mod prefix.
        /// </summary>
        public static void Log(string message)
        {
            Debug.Log($"{LOG_PREFIX} {message}");
        }

        /// <summary>
        /// Log a warning with the mod prefix.
        /// </summary>
        public static void LogWarning(string message)
        {
            Debug.LogWarning($"{LOG_PREFIX} {message}");
        }

        /// <summary>
        /// Log an error with the mod prefix.
        /// </summary>
        public static void LogError(string message)
        {
            Debug.LogError($"{LOG_PREFIX} {message}");
        }
    }
}
