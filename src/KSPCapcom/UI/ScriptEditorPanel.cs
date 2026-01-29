using System;
using UnityEngine;
using KSPCapcom.Parsing;

namespace KSPCapcom.UI
{
    /// <summary>
    /// Floating IMGUI window for viewing and editing kOS scripts.
    /// Allows users to inspect and modify scripts before saving.
    /// </summary>
    public class ScriptEditorPanel
    {
        private const int WINDOW_ID = 84731;
        private const float DEFAULT_WIDTH = 450f;
        private const float DEFAULT_HEIGHT = 400f;
        private const float MIN_WIDTH = 350f;
        private const float MIN_HEIGHT = 250f;

        /// <summary>
        /// Control name for focus management.
        /// </summary>
        public const string ControlName = "ScriptEditorCode";

        private Rect _windowRect;
        private bool _isVisible;
        private Vector2 _scrollPosition;

        // Source and edited content
        private CodeBlockSegment _sourceCodeBlock;
        private string _editedCode = "";
        private bool _hasUnsavedChanges;

        // Callback for save action
        private Action<string> _onSaveCallback;

        // Settings reference
        private readonly CapcomSettings _settings;

        // Focus management
        private int _focusFrames;
        private const int FOCUS_FRAME_COUNT = 3;

        // Styles
        private GUIStyle _windowStyle;
        private GUIStyle _codeStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _titleStyle;
        private bool _stylesInitialized;

        // Colors
        private static readonly Color COLOR_UNSAVED = new Color(1f, 0.8f, 0.4f);
        private static readonly Color COLOR_MUTED = new Color(0.6f, 0.6f, 0.6f);

        /// <summary>
        /// Whether the editor panel is currently visible.
        /// </summary>
        public bool IsVisible => _isVisible;

        public ScriptEditorPanel(CapcomSettings settings)
        {
            _settings = settings;
            _isVisible = false;
            _stylesInitialized = false;

            // Position window in center of screen
            float x = (Screen.width - DEFAULT_WIDTH) / 2;
            float y = (Screen.height - DEFAULT_HEIGHT) / 2;
            _windowRect = new Rect(x, y, DEFAULT_WIDTH, DEFAULT_HEIGHT);

            CapcomCore.Log("ScriptEditorPanel initialized");
        }

        /// <summary>
        /// Open the editor with a code block and save callback.
        /// </summary>
        /// <param name="codeBlock">The code block to edit.</param>
        /// <param name="onSave">Callback invoked with edited code when Save is clicked.</param>
        public void Open(CodeBlockSegment codeBlock, Action<string> onSave)
        {
            if (codeBlock == null)
            {
                return;
            }

            _sourceCodeBlock = codeBlock;
            _editedCode = codeBlock.RawCode;
            _hasUnsavedChanges = false;
            _onSaveCallback = onSave;
            _scrollPosition = Vector2.zero;
            _isVisible = true;
            _focusFrames = FOCUS_FRAME_COUNT;

            CapcomCore.Log("ScriptEditorPanel opened");
        }

        /// <summary>
        /// Close the editor, discarding any unsaved changes.
        /// </summary>
        public void Close()
        {
            _isVisible = false;
            _sourceCodeBlock = null;
            _editedCode = "";
            _hasUnsavedChanges = false;
            _onSaveCallback = null;

            CapcomCore.Log("ScriptEditorPanel closed");
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

            // Handle keyboard shortcuts before window
            HandleKeyboardInput();

            _windowRect = GUILayout.Window(
                WINDOW_ID,
                _windowRect,
                DrawWindow,
                "",
                _windowStyle,
                GUILayout.MinWidth(MIN_WIDTH),
                GUILayout.MinHeight(MIN_HEIGHT)
            );

            ClampWindowToScreen();
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();

            // Title bar with unsaved indicator and close button
            DrawTitleBar();

            // Scrollable code editor
            DrawCodeArea();

            // Status bar
            DrawStatusBar();

            // Action buttons
            DrawButtons();

            GUILayout.EndVertical();

            // Make window draggable by the title bar area
            GUI.DragWindow(new Rect(0, 0, _windowRect.width, 25));
        }

        private void DrawTitleBar()
        {
            GUILayout.BeginHorizontal();

            // Title with unsaved indicator
            string title = "Script Editor";
            if (_hasUnsavedChanges)
            {
                title += " *";
            }
            GUILayout.Label(title, _titleStyle);

            GUILayout.FlexibleSpace();

            // Close button
            if (GUILayout.Button("X", _buttonStyle, GUILayout.Width(24), GUILayout.Height(20)))
            {
                Close();
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        private void DrawCodeArea()
        {
            _scrollPosition = GUILayout.BeginScrollView(
                _scrollPosition,
                false,
                true,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true)
            );

            // Set control name for focus management
            GUI.SetNextControlName(ControlName);

            // Editable text area
            string newCode = GUILayout.TextArea(
                _editedCode,
                _codeStyle,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true)
            );

            // Track changes
            if (newCode != _editedCode)
            {
                _editedCode = newCode;
                _hasUnsavedChanges = _sourceCodeBlock != null && newCode != _sourceCodeBlock.RawCode;
            }

            GUILayout.EndScrollView();

            // Handle focus
            if (_focusFrames > 0)
            {
                GUI.FocusControl(ControlName);
                _focusFrames--;
            }
        }

        private void DrawStatusBar()
        {
            GUILayout.BeginHorizontal();

            // Line count
            int lineCount = string.IsNullOrEmpty(_editedCode) ? 0 : _editedCode.Split('\n').Length;
            string lineText = lineCount == 1 ? "1 line" : $"{lineCount} lines";
            GUILayout.Label(lineText, _statusStyle);

            GUILayout.FlexibleSpace();

            // Unsaved changes indicator
            if (_hasUnsavedChanges)
            {
                var unsavedStyle = new GUIStyle(_statusStyle);
                unsavedStyle.normal.textColor = COLOR_UNSAVED;
                GUILayout.Label("Unsaved changes", unsavedStyle);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawButtons()
        {
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            // Cancel button
            if (GUILayout.Button("Cancel", _buttonStyle, GUILayout.Width(70), GUILayout.Height(24)))
            {
                Close();
            }

            GUILayout.Space(8);

            // Save button
            if (GUILayout.Button("Save...", _buttonStyle, GUILayout.Width(70), GUILayout.Height(24)))
            {
                ExecuteSave();
            }

            GUILayout.EndHorizontal();
        }

        private void HandleKeyboardInput()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                // Escape closes the editor (only when editor control is focused)
                if (e.keyCode == KeyCode.Escape && GUI.GetNameOfFocusedControl() == ControlName)
                {
                    Close();
                    e.Use();
                }
            }
        }

        private void ExecuteSave()
        {
            if (_onSaveCallback != null)
            {
                _onSaveCallback.Invoke(_editedCode);
            }
            Close();
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
                padding = new RectOffset(10, 10, 10, 10)
            };

            // Code text area style (monospace)
            _codeStyle = new GUIStyle(HighLogic.Skin.textArea)
            {
                font = Font.CreateDynamicFontFromOSFont("Consolas", 11),
                fontSize = 11,
                wordWrap = false,
                richText = false,
                padding = new RectOffset(6, 6, 6, 6)
            };
            _codeStyle.normal.textColor = new Color(0.9f, 0.95f, 0.9f);

            // Status bar style
            _statusStyle = new GUIStyle(HighLogic.Skin.label)
            {
                fontSize = 10,
                padding = new RectOffset(4, 4, 2, 2)
            };
            _statusStyle.normal.textColor = COLOR_MUTED;

            // Button style
            _buttonStyle = new GUIStyle(HighLogic.Skin.button)
            {
                fontSize = 11,
                padding = new RectOffset(8, 8, 4, 4)
            };

            // Title style
            _titleStyle = new GUIStyle(HighLogic.Skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(4, 4, 4, 4)
            };

            _stylesInitialized = true;
        }

        private void ClampWindowToScreen()
        {
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0, Screen.width - _windowRect.width);
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0, Screen.height - _windowRect.height);
        }
    }
}
