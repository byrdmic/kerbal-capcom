using System;
using System.Linq;
using UnityEngine;
using KSPCapcom.Editor;

namespace KSPCapcom
{
    /// <summary>
    /// Readiness panel window for craft metrics display.
    /// Shows TWR, delta-V, control authority, and staging warnings.
    /// Uses Unity IMGUI for rendering.
    /// </summary>
    public class ReadinessPanel
    {
        private const int WINDOW_ID = 84730;
        private const float DEFAULT_WIDTH = 260f;
        private const float DEFAULT_HEIGHT = 240f;
        private const float MIN_WIDTH = 220f;
        private const float MIN_HEIGHT = 180f;

        private Rect _windowRect;
        private bool _isVisible;
        private ReadinessMetrics _currentMetrics;
        private readonly CapcomSettings _settings;
        private bool _isSubscribed;

        // Styles
        private GUIStyle _windowStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _dimmedStyle;
        private GUIStyle _smallLabelStyle;
        private GUIStyle _warningStyle;
        private GUIStyle _errorStyle;
        private bool _stylesInitialized;

        /// <summary>
        /// Whether the readiness panel is currently visible.
        /// </summary>
        public bool IsVisible => _isVisible;

        public ReadinessPanel(CapcomSettings settings)
        {
            _settings = settings;
            _isVisible = settings?.ReadinessPanelVisible ?? false;
            _stylesInitialized = false;
            _currentMetrics = ReadinessMetrics.NotAvailable;

            InitializeWindowRect();
            // Subscription will be called by CapcomCore after monitor is ready

            CapcomCore.Log("ReadinessPanel initialized");
        }

        private void InitializeWindowRect()
        {
            // Position left of chat panel
            // Chat is at (Screen.width - 350 - 50, 100)
            // Readiness is 10px to the left with a 10px gap
            float x = Screen.width - 350 - 50 - DEFAULT_WIDTH - 10;
            float y = 100;
            _windowRect = new Rect(x, y, DEFAULT_WIDTH, DEFAULT_HEIGHT);
        }

        /// <summary>
        /// Subscribe to EditorCraftMonitor updates. Safe to call multiple times.
        /// </summary>
        public void SubscribeToMonitor()
        {
            // Guard: already subscribed or monitor not available yet
            if (_isSubscribed || EditorCraftMonitor.Instance == null)
            {
                return;
            }

            EditorCraftMonitor.Instance.OnSnapshotReady += OnSnapshotUpdated;
            _isSubscribed = true;
            CapcomCore.Log("ReadinessPanel subscribed to EditorCraftMonitor");
        }

        private void OnSnapshotUpdated(EditorCraftSnapshot snapshot)
        {
            if (snapshot == null || snapshot.IsEmpty)
            {
                _currentMetrics = ReadinessMetrics.NotAvailable;
                return;
            }

            _currentMetrics = snapshot.Readiness;
            CapcomCore.Log($"ReadinessPanel received snapshot update: TWR={_currentMetrics.TWR.AtmosphericTWR:F2}");
        }

        /// <summary>
        /// Toggle the panel visibility.
        /// </summary>
        public void Toggle()
        {
            _isVisible = !_isVisible;
            if (_settings != null)
            {
                _settings.ReadinessPanelVisible = _isVisible;
            }
            CapcomCore.Log($"Readiness panel {(_isVisible ? "opened" : "closed")}");
        }

        /// <summary>
        /// Show the panel.
        /// </summary>
        public void Show()
        {
            if (!_isVisible)
            {
                _isVisible = true;
                if (_settings != null)
                {
                    _settings.ReadinessPanelVisible = true;
                }
                CapcomCore.Log("Readiness panel opened");
            }
        }

        /// <summary>
        /// Hide the panel.
        /// </summary>
        public void Hide()
        {
            if (_isVisible)
            {
                _isVisible = false;
                if (_settings != null)
                {
                    _settings.ReadinessPanelVisible = false;
                }
                CapcomCore.Log("Readiness panel closed");
            }
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

            // Defensive: Retry subscription if we're in editor and monitor is now available
            if (!_isSubscribed && HighLogic.LoadedSceneIsEditor && EditorCraftMonitor.Instance != null)
            {
                SubscribeToMonitor();
            }

            InitializeStyles();

            // Make window draggable and draw it
            _windowRect = GUILayout.Window(
                WINDOW_ID,
                _windowRect,
                DrawWindow,
                "READINESS",
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

            // Base label style
            _labelStyle = new GUIStyle(HighLogic.Skin.label)
            {
                wordWrap = true,
                richText = true,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(4, 4, 2, 2)
            };

            // Dimmed style for unavailable data
            _dimmedStyle = new GUIStyle(_labelStyle);
            _dimmedStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            _dimmedStyle.fontStyle = FontStyle.Italic;

            // Small label style for subtext
            _smallLabelStyle = new GUIStyle(_labelStyle)
            {
                fontSize = 11
            };
            _smallLabelStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            // Warning style (orange)
            _warningStyle = new GUIStyle(_labelStyle);
            _warningStyle.normal.textColor = new Color(1f, 0.8f, 0.4f);

            // Error style (red)
            _errorStyle = new GUIStyle(_labelStyle);
            _errorStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);

            _stylesInitialized = true;
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();

            // Check if we're in Flight scene
            if (!HighLogic.LoadedSceneIsEditor)
            {
                GUILayout.Label("Editor only", _dimmedStyle);
                GUILayout.EndVertical();
                GUI.DragWindow();
                return;
            }

            // Check if monitor is available
            if (EditorCraftMonitor.Instance == null)
            {
                GUILayout.Label("Editor only", _dimmedStyle);
                GUILayout.EndVertical();
                GUI.DragWindow();
                return;
            }

            // Check if we have data
            if (_currentMetrics == null || _currentMetrics == ReadinessMetrics.NotAvailable)
            {
                GUILayout.Label("No craft loaded", _dimmedStyle);
                GUILayout.EndVertical();
                GUI.DragWindow();
                return;
            }

            // Draw sections
            DrawTWRSection();
            GUILayout.Space(6);
            DrawDeltaVSection();
            GUILayout.Space(6);
            DrawControlSection();
            GUILayout.Space(6);
            DrawStagingSection();

            GUILayout.EndVertical();

            // Make window draggable by the title bar
            GUI.DragWindow(new Rect(0, 0, _windowRect.width, 20));
        }

        private void DrawTWRSection()
        {
            if (_currentMetrics.TWR == null || !_currentMetrics.TWR.IsAvailable)
            {
                GUILayout.Label("Launch TWR: N/A", _dimmedStyle);
                return;
            }

            var twr = _currentMetrics.TWR;
            float displayTWR = twr.AtmosphericTWR;
            Color twrColor = GetTWRColor(displayTWR);
            GUIStyle coloredStyle = GetColoredStyle(_labelStyle, twrColor);

            // Main TWR line with colored indicator
            string indicator = "\u25CF"; // Filled circle
            GUILayout.Label($"Launch TWR: {displayTWR:F2} <color=#{ColorToHex(twrColor)}>{indicator}</color>", _labelStyle);

            // Subtext with both ASL and Vac values
            GUILayout.Label($"(ASL/Vac: {twr.AtmosphericTWR:F2}/{twr.VacuumTWR:F2})", _smallLabelStyle);
        }

        private void DrawDeltaVSection()
        {
            if (_currentMetrics.DeltaV == null || !_currentMetrics.DeltaV.IsAvailable)
            {
                GUILayout.Label("Delta-V: N/A", _dimmedStyle);
                return;
            }

            float deltaV = _currentMetrics.DeltaV.TotalDeltaV;
            string formattedDeltaV = deltaV >= 1000 ? $"{deltaV:N0}" : $"{deltaV:F0}";
            GUILayout.Label($"Delta-V: ~{formattedDeltaV} m/s", _labelStyle);
        }

        private void DrawControlSection()
        {
            if (_currentMetrics.ControlAuthority == null || !_currentMetrics.ControlAuthority.IsAvailable)
            {
                GUILayout.Label("Control: N/A", _dimmedStyle);
                return;
            }

            var control = _currentMetrics.ControlAuthority;
            string statusText;
            GUIStyle statusStyle;

            switch (control.Status)
            {
                case ControlAuthorityStatus.None:
                    statusText = "Control: WARN";
                    statusStyle = _errorStyle;
                    break;
                case ControlAuthorityStatus.Marginal:
                    statusText = "Control: MARGINAL";
                    statusStyle = _warningStyle;
                    break;
                case ControlAuthorityStatus.Good:
                    statusText = "Control: OK";
                    statusStyle = GetColoredStyle(_labelStyle, new Color(0.6f, 0.9f, 0.6f));
                    break;
                default:
                    statusText = "Control: Unknown";
                    statusStyle = _dimmedStyle;
                    break;
            }

            GUILayout.Label(statusText, statusStyle);
        }

        private void DrawStagingSection()
        {
            if (_currentMetrics.Staging == null || !_currentMetrics.Staging.HasWarnings)
            {
                // No warnings - don't show anything (or show "OK" if you prefer)
                // Per plan: only show when warnings exist
                return;
            }

            GUILayout.Label("Staging:", _labelStyle);

            var warnings = _currentMetrics.Staging.Warnings;
            int maxWarnings = Math.Min(5, warnings.Count);

            for (int i = 0; i < maxWarnings; i++)
            {
                string warningIcon = "\u26A0"; // Warning triangle
                GUILayout.Label($" {warningIcon} {warnings[i]}", _warningStyle);
            }

            if (warnings.Count > 5)
            {
                int remaining = warnings.Count - 5;
                GUILayout.Label($"(+{remaining} more)", _dimmedStyle);
            }
        }

        /// <summary>
        /// Get the appropriate color for a TWR value.
        /// </summary>
        private Color GetTWRColor(float twr)
        {
            if (twr < 1.0f)
            {
                return new Color(1f, 0.4f, 0.4f); // Red
            }
            else if (twr < 1.5f)
            {
                return new Color(1f, 0.8f, 0.4f); // Orange
            }
            else
            {
                return new Color(0.6f, 0.9f, 0.6f); // Green
            }
        }

        /// <summary>
        /// Create a colored style from a base style.
        /// </summary>
        private GUIStyle GetColoredStyle(GUIStyle baseStyle, Color color)
        {
            var coloredStyle = new GUIStyle(baseStyle);
            coloredStyle.normal.textColor = color;
            return coloredStyle;
        }

        /// <summary>
        /// Convert a Color to hex string for rich text.
        /// </summary>
        private string ColorToHex(Color color)
        {
            int r = Mathf.RoundToInt(color.r * 255);
            int g = Mathf.RoundToInt(color.g * 255);
            int b = Mathf.RoundToInt(color.b * 255);
            return $"{r:X2}{g:X2}{b:X2}";
        }

        private void ClampWindowToScreen()
        {
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0, Screen.width - _windowRect.width);
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0, Screen.height - _windowRect.height);
        }

        /// <summary>
        /// Cleanup subscriptions.
        /// </summary>
        public void Cleanup()
        {
            if (EditorCraftMonitor.Instance != null && _isSubscribed)
            {
                EditorCraftMonitor.Instance.OnSnapshotReady -= OnSnapshotUpdated;
                _isSubscribed = false;
                CapcomCore.Log("ReadinessPanel unsubscribed from EditorCraftMonitor");
            }
        }
    }
}
