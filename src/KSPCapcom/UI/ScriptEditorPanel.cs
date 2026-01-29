using System;
using System.Collections.Generic;
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
        private const string FindFieldControlName = "ScriptEditorFind";

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

        // Word wrap
        private bool _wordWrapEnabled = false;

        // Find functionality
        private bool _findBarVisible = false;
        private string _findText = "";
        private int _currentMatchIndex = 0;
        private List<int> _matchPositions = new List<int>();
        private int _findFocusFrames;

        // Confirmation dialog
        private bool _confirmDialogOpen = false;

        // Styles
        private GUIStyle _windowStyle;
        private GUIStyle _codeStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _toggleStyle;
        private GUIStyle _findFieldStyle;
        private GUIStyle _dialogBoxStyle;
        private GUIStyle _dialogLabelStyle;
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

            // Reset find state
            _findBarVisible = false;
            _findText = "";
            _currentMatchIndex = 0;
            _matchPositions.Clear();

            // Reset confirmation dialog
            _confirmDialogOpen = false;

            CapcomCore.Log("ScriptEditorPanel opened");
        }

        /// <summary>
        /// Close the editor. Shows confirmation dialog if there are unsaved changes.
        /// </summary>
        public void Close()
        {
            if (_hasUnsavedChanges)
            {
                _confirmDialogOpen = true;
                return;
            }

            ForceClose();
        }

        /// <summary>
        /// Force close the editor, discarding any unsaved changes without confirmation.
        /// </summary>
        public void ForceClose()
        {
            _isVisible = false;
            _sourceCodeBlock = null;
            _editedCode = "";
            _hasUnsavedChanges = false;
            _onSaveCallback = null;
            _confirmDialogOpen = false;
            _findBarVisible = false;

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

            // Find bar (collapsible)
            if (_findBarVisible)
            {
                DrawFindBar();
            }

            // Scrollable code editor
            DrawCodeArea();

            // Status bar
            DrawStatusBar();

            // Action buttons
            DrawButtons();

            GUILayout.EndVertical();

            // Confirmation dialog overlay
            if (_confirmDialogOpen)
            {
                DrawConfirmationDialog();
            }

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

                // Update find matches if find bar is active
                if (_findBarVisible && !string.IsNullOrEmpty(_findText))
                {
                    UpdateMatchPositions();
                }
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

            // Word wrap toggle
            string wrapSymbol = _wordWrapEnabled ? "\u25A0" : "\u25A1"; // ■ or □
            if (GUILayout.Button($"Wrap: {wrapSymbol}", _toggleStyle, GUILayout.Width(60)))
            {
                _wordWrapEnabled = !_wordWrapEnabled;
                _codeStyle.wordWrap = _wordWrapEnabled;
            }

            GUILayout.Space(8);

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
            if (e.type != EventType.KeyDown)
            {
                return;
            }

            // Handle confirmation dialog keyboard input
            if (_confirmDialogOpen)
            {
                if (e.keyCode == KeyCode.Escape)
                {
                    _confirmDialogOpen = false;
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    ForceClose();
                    e.Use();
                }
                return;
            }

            // Ctrl+F opens/focuses find bar
            if (e.control && e.keyCode == KeyCode.F)
            {
                _findBarVisible = true;
                _findFocusFrames = FOCUS_FRAME_COUNT;
                e.Use();
                return;
            }

            // Handle find bar keyboard input
            if (_findBarVisible && GUI.GetNameOfFocusedControl() == FindFieldControlName)
            {
                if (e.keyCode == KeyCode.Escape)
                {
                    _findBarVisible = false;
                    _focusFrames = FOCUS_FRAME_COUNT; // Return focus to code area
                    e.Use();
                    return;
                }
                else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    NavigateToNextMatch();
                    e.Use();
                    return;
                }
            }

            // Escape closes the editor (only when editor control is focused)
            if (e.keyCode == KeyCode.Escape && GUI.GetNameOfFocusedControl() == ControlName)
            {
                Close();
                e.Use();
            }
        }

        private void DrawFindBar()
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label("Find:", _statusStyle, GUILayout.Width(35));

            // Find text field
            GUI.SetNextControlName(FindFieldControlName);
            string newFindText = GUILayout.TextField(_findText, _findFieldStyle, GUILayout.Width(150));

            if (newFindText != _findText)
            {
                _findText = newFindText;
                UpdateMatchPositions();
            }

            GUILayout.Space(4);

            // Previous match button
            if (GUILayout.Button("\u25B2", _toggleStyle, GUILayout.Width(24))) // ▲
            {
                NavigateToPreviousMatch();
            }

            // Next match button
            if (GUILayout.Button("\u25BC", _toggleStyle, GUILayout.Width(24))) // ▼
            {
                NavigateToNextMatch();
            }

            GUILayout.Space(4);

            // Match count display
            string matchDisplay;
            if (string.IsNullOrEmpty(_findText))
            {
                matchDisplay = "";
            }
            else if (_matchPositions.Count == 0)
            {
                matchDisplay = "No matches";
            }
            else
            {
                matchDisplay = $"{_currentMatchIndex + 1} of {_matchPositions.Count}";
            }
            GUILayout.Label(matchDisplay, _statusStyle, GUILayout.Width(70));

            GUILayout.FlexibleSpace();

            // Close find bar button
            if (GUILayout.Button("X", _toggleStyle, GUILayout.Width(20)))
            {
                _findBarVisible = false;
                _focusFrames = FOCUS_FRAME_COUNT;
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            // Handle focus for find field
            if (_findFocusFrames > 0)
            {
                GUI.FocusControl(FindFieldControlName);
                _findFocusFrames--;
            }
        }

        private void UpdateMatchPositions()
        {
            _matchPositions.Clear();
            _currentMatchIndex = 0;

            if (string.IsNullOrEmpty(_findText) || string.IsNullOrEmpty(_editedCode))
            {
                return;
            }

            int index = 0;
            while ((index = _editedCode.IndexOf(_findText, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                _matchPositions.Add(index);
                index += _findText.Length;
            }
        }

        private void NavigateToNextMatch()
        {
            if (_matchPositions.Count == 0)
            {
                return;
            }

            _currentMatchIndex = (_currentMatchIndex + 1) % _matchPositions.Count;
            ScrollToMatch(_currentMatchIndex);
        }

        private void NavigateToPreviousMatch()
        {
            if (_matchPositions.Count == 0)
            {
                return;
            }

            _currentMatchIndex = (_currentMatchIndex - 1 + _matchPositions.Count) % _matchPositions.Count;
            ScrollToMatch(_currentMatchIndex);
        }

        private void ScrollToMatch(int matchIndex)
        {
            if (matchIndex < 0 || matchIndex >= _matchPositions.Count)
            {
                return;
            }

            int charPosition = _matchPositions[matchIndex];

            // Estimate line number from character position
            int lineNumber = 0;
            int currentPos = 0;
            string[] lines = _editedCode.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                if (currentPos + lines[i].Length >= charPosition)
                {
                    lineNumber = i;
                    break;
                }
                currentPos += lines[i].Length + 1; // +1 for newline
            }

            // Estimate scroll position based on line number
            // Approximate line height in pixels (monospace font at 11px plus padding)
            float lineHeight = 15f;
            float targetScrollY = lineNumber * lineHeight;

            // Center the match in view if possible
            float viewHeight = _windowRect.height - 150; // Approximate visible code area
            targetScrollY = Mathf.Max(0, targetScrollY - viewHeight / 2);

            _scrollPosition.y = targetScrollY;
        }

        private void DrawConfirmationDialog()
        {
            // Semi-transparent overlay
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(new Rect(0, 0, _windowRect.width, _windowRect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Dialog box dimensions
            float dialogWidth = 220f;
            float dialogHeight = 80f;
            float dialogX = (_windowRect.width - dialogWidth) / 2;
            float dialogY = (_windowRect.height - dialogHeight) / 2;

            // Dialog box
            GUI.Box(new Rect(dialogX, dialogY, dialogWidth, dialogHeight), "", _dialogBoxStyle);

            GUILayout.BeginArea(new Rect(dialogX + 10, dialogY + 10, dialogWidth - 20, dialogHeight - 20));
            GUILayout.BeginVertical();

            // Message
            GUILayout.Label("Discard unsaved changes?", _dialogLabelStyle);
            GUILayout.Space(12);

            // Buttons
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", _buttonStyle, GUILayout.Width(70), GUILayout.Height(24)))
            {
                _confirmDialogOpen = false;
            }

            GUILayout.Space(8);

            if (GUILayout.Button("Discard", _buttonStyle, GUILayout.Width(70), GUILayout.Height(24)))
            {
                ForceClose();
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void ExecuteSave()
        {
            if (_onSaveCallback != null)
            {
                _onSaveCallback.Invoke(_editedCode);
            }
            // Use ForceClose to bypass confirmation since user explicitly saved
            ForceClose();
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

            // Toggle style (for word wrap and find navigation buttons)
            _toggleStyle = new GUIStyle(HighLogic.Skin.button)
            {
                fontSize = 10,
                padding = new RectOffset(4, 4, 2, 2)
            };

            // Find text field style
            _findFieldStyle = new GUIStyle(HighLogic.Skin.textField)
            {
                fontSize = 11,
                padding = new RectOffset(4, 4, 2, 2)
            };

            // Dialog box style
            _dialogBoxStyle = new GUIStyle(HighLogic.Skin.box)
            {
                padding = new RectOffset(10, 10, 10, 10)
            };

            // Dialog label style
            _dialogLabelStyle = new GUIStyle(HighLogic.Skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
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
