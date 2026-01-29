using UnityEngine;

namespace KSPCapcom
{
    public partial class ChatPanel
    {
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

        // Styles
        private GUIStyle _windowStyle;
        private GUIStyle _messageStyle;
        private GUIStyle _userMessageStyle;
        private GUIStyle _systemMessageStyle;
        private GUIStyle _inputStyle;
        private bool _stylesInitialized;

        // Settings UI styles
        private GUIStyle _settingsHeaderStyle;
        private GUIStyle _settingsBoxStyle;
        private GUIStyle _validationErrorStyle;
        private GUIStyle _statusLabelStyle;
        private GUIStyle _cancelButtonStyle;
        private GUIStyle _queueCountStyle;
        private GUIStyle _queuedMessageStyle;
        private GUIStyle _critiqueButtonStyle;
        private GUIStyle _jumpToLatestStyle;

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

        private static string FormatTimestamp(System.DateTime timestamp) =>
            FormatMuted(timestamp.ToString("HH:mm"));

        #endregion
    }
}
