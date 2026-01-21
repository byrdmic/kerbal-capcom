using System;
using System.Reflection;
using UnityEngine;

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
            _chatPanel = new ChatPanel(new Responders.EchoResponder(), _settings);
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
        }

        private void OnDestroy()
        {
            Log("OnDestroy - cleaning up");

            // Clean up toolbar button to prevent duplicates
            _toolbarButton?.Destroy();
            _toolbarButton = null;

            _chatPanel = null;

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
