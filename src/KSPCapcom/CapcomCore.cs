using System;
using System.Reflection;
using UnityEngine;
using KSPCapcom.Critique;
using KSPCapcom.Editor;
using KSPCapcom.KosDocs;
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
        private ReadinessPanel _readinessPanel;
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

        // Log filter state
        private static bool _logFilterInstalled = false;
        private static FilteringLogHandler _logHandler;

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

            // Install log filter once to suppress noisy Unity GUI errors
            if (!_logFilterInstalled)
            {
                _logHandler = new FilteringLogHandler(Debug.unityLogger.logHandler);
                Debug.unityLogger.logHandler = _logHandler;
                _logFilterInstalled = true;
            }

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
            _readinessPanel = new ReadinessPanel(_settings);
            _toolbarButton = new ToolbarButton(OnToolbarToggle);

            // Create critique service (uses same connector as chat)
            var critiqueService = new CritiqueService(connector);
            _chatPanel.SetCritiqueService(critiqueService);

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

            // Initialize kOS documentation index (loads async)
            InitializeKosDocs();
        }

        /// <summary>
        /// Initialize the kOS documentation service.
        /// Loading is async to avoid frame hitches.
        /// </summary>
        private void InitializeKosDocs()
        {
            KosDocService.Instance.Initialize(success =>
            {
                if (success)
                {
                    Log($"kOS docs loaded: {KosDocService.Instance.EntryCount} entries");
                }
                else
                {
                    LogWarning("kOS docs failed to load - kOS syntax help will be limited");
                }
            });
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

            // Subscribe ReadinessPanel now that monitor is available
            _readinessPanel?.SubscribeToMonitor();
        }

        private void OnDestroy()
        {
            Log("OnDestroy - cleaning up");

            // Clean up toolbar button to prevent duplicates
            _toolbarButton?.Destroy();
            _toolbarButton = null;

            _chatPanel = null;

            // Clean up readiness panel subscriptions
            _readinessPanel?.Cleanup();
            _readinessPanel = null;

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

            // Render the readiness panel if visible
            _readinessPanel?.OnGUI();
        }

        private void Update()
        {
            // Alt+R keyboard shortcut for readiness panel
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.R))
            {
                _readinessPanel?.Toggle();
            }
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

    /// <summary>
    /// Custom log handler that filters out noisy Unity GUI messages.
    /// </summary>
    internal class FilteringLogHandler : ILogHandler
    {
        private readonly ILogHandler _defaultHandler;

        public FilteringLogHandler(ILogHandler defaultHandler)
        {
            _defaultHandler = defaultHandler;
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            // Suppress the common Unity GUI error about GetLast after BeginGroup
            // This comes from stock KSP or other mods with GUI layout issues
            if (logType == LogType.Error && format != null &&
                format.Contains("GetLast immediately after beginning a group"))
            {
                return; // Swallow the message
            }

            _defaultHandler.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            _defaultHandler.LogException(exception, context);
        }
    }
}
