using System;
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

        private ToolbarButton _toolbarButton;
        private ChatPanel _chatPanel;

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
            Log("Bootstrap Awake()");
        }

        private void Start()
        {
            Log("Bootstrap Start()");

            // Initialize components
            _chatPanel = new ChatPanel();
            _toolbarButton = new ToolbarButton(OnToolbarToggle);

            Log($"Initialized in scene: {HighLogic.LoadedScene}");
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
                Log($"Chat panel toggled: {_chatPanel.IsVisible}");
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
