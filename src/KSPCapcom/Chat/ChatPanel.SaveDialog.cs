using UnityEngine;
using KSPCapcom.IO;
using KSPCapcom.Parsing;

namespace KSPCapcom
{
    public partial class ChatPanel
    {
        // Script saving
        private readonly ScriptSaver _scriptSaver = new ScriptSaver();
        private bool _saveDialogOpen = false;
        private string _saveDialogFilename = "";
        private CodeBlockSegment _saveDialogCodeBlock;
        private string _saveDialogValidationError = "";
        private bool _saveDialogShowOverwrite = false;

        /// <summary>
        /// Draw the save dialog modal overlay.
        /// </summary>
        private void DrawSaveDialog()
        {
            // Semi-transparent overlay
            GUI.Box(new Rect(0, 0, _windowRect.width, _windowRect.height), "", HighLogic.Skin.box);

            // Dialog box centered in window
            float dialogWidth = 280f;
            float dialogHeight = _saveDialogShowOverwrite ? 140f : 120f;
            float dialogX = (_windowRect.width - dialogWidth) / 2;
            float dialogY = (_windowRect.height - dialogHeight) / 2;

            GUILayout.BeginArea(new Rect(dialogX, dialogY, dialogWidth, dialogHeight), HighLogic.Skin.window);
            GUILayout.BeginVertical();

            // Title
            GUILayout.Label("<b>Save Script</b>", _systemMessageStyle);
            GUILayout.Space(4);

            // Filename input
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filename:", GUILayout.Width(60));
            GUI.SetNextControlName("SaveDialogFilename");
            _saveDialogFilename = GUILayout.TextField(_saveDialogFilename, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            // Archive path display (read-only)
            if (_settings != null)
            {
                var archiveLabel = _settings.KosArchivePath;
                if (archiveLabel.Length > 35)
                {
                    archiveLabel = "..." + archiveLabel.Substring(archiveLabel.Length - 32);
                }
                GUILayout.Label($"<size={FONT_SIZE_SMALL}><color={HEX_MUTED}>Archive: {archiveLabel}</color></size>", _systemMessageStyle);
            }

            // Validation error or overwrite prompt
            if (!string.IsNullOrEmpty(_saveDialogValidationError))
            {
                GUILayout.Label(_saveDialogValidationError, _validationErrorStyle);
            }
            else if (_saveDialogShowOverwrite)
            {
                var warnStyle = new GUIStyle(_statusLabelStyle);
                warnStyle.normal.textColor = COLOR_WARNING;
                GUILayout.Label($"Overwrite {_saveDialogFilename}?", warnStyle);
            }

            GUILayout.FlexibleSpace();

            // Buttons
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(60)))
            {
                CloseSaveDialog();
            }

            string saveLabel = _saveDialogShowOverwrite ? "Overwrite" : "Save";
            if (GUILayout.Button(saveLabel, GUILayout.Width(70)))
            {
                ExecuteSave();
            }

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();

            // Handle keyboard shortcuts
            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return)
                {
                    ExecuteSave();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    CloseSaveDialog();
                    e.Use();
                }
            }
        }

        /// <summary>
        /// Callback for script card save request. Opens the save dialog.
        /// </summary>
        private void OnScriptSaveRequest(CodeBlockSegment codeBlock)
        {
            if (codeBlock == null)
            {
                return;
            }

            // Check if archive path is configured
            if (_settings == null || !_settings.IsArchivePathValid)
            {
                AddSystemMessage(FormatWarning("Configure kOS archive path in Settings"));
                return;
            }

            // Open save dialog with default filename
            _saveDialogCodeBlock = codeBlock;
            _saveDialogFilename = _scriptSaver.GenerateDefaultFilename(codeBlock.RawCode);
            _saveDialogValidationError = "";
            _saveDialogShowOverwrite = false;
            _saveDialogOpen = true;

            CapcomCore.Log("ScriptSaver: Save dialog opened");
        }

        /// <summary>
        /// Execute the save operation from the dialog.
        /// </summary>
        private void ExecuteSave()
        {
            if (_saveDialogCodeBlock == null || _settings == null)
            {
                return;
            }

            // Validate filename
            var validation = _scriptSaver.ValidateFilename(_saveDialogFilename);
            if (!validation.IsValid)
            {
                _saveDialogValidationError = validation.Error;
                return;
            }

            // Check for existing file
            if (!_saveDialogShowOverwrite && _scriptSaver.FileExists(_settings.KosArchivePath, _saveDialogFilename))
            {
                _saveDialogShowOverwrite = true;
                _saveDialogValidationError = "";
                return;
            }

            // Perform save
            var result = _scriptSaver.Save(
                _settings.KosArchivePath,
                _saveDialogFilename,
                _saveDialogCodeBlock.RawCode,
                _saveDialogShowOverwrite);

            if (result.Success)
            {
                string message = FormatSaveSuccessMessage(result, _saveDialogFilename);
                AddSystemMessage(FormatSuccess(message));
                CloseSaveDialog();
            }
            else
            {
                _saveDialogValidationError = result.Error;
            }
        }

        /// <summary>
        /// Format the success message after saving a script.
        /// </summary>
        private string FormatSaveSuccessMessage(SaveResult result, string filename)
        {
            // Ensure .ks extension for display
            if (!filename.EndsWith(".ks", System.StringComparison.OrdinalIgnoreCase))
                filename += ".ks";

            // Get and truncate folder path
            string folder = System.IO.Path.GetDirectoryName(result.FullPath);
            if (folder.Length > 35)
                folder = "..." + folder.Substring(folder.Length - 32);

            string action = result.WasOverwritten ? "Overwritten" : "Saved";
            string kosCommand = $"runpath(\"0:/{filename}\").";

            return $"{action}: {filename}\nFolder: {folder}\nRun in kOS: {kosCommand}";
        }

        /// <summary>
        /// Close the save dialog without saving.
        /// </summary>
        private void CloseSaveDialog()
        {
            _saveDialogOpen = false;
            _saveDialogCodeBlock = null;
            _saveDialogFilename = "";
            _saveDialogValidationError = "";
            _saveDialogShowOverwrite = false;
        }
    }
}
