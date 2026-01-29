using UnityEngine;

namespace KSPCapcom
{
    public partial class ChatPanel
    {
        // Settings UI state
        private bool _settingsExpanded = false;
        private string _endpointInput = "";
        private string _modelInput = "";
        private string _apiKeyInput = "";
        private bool _showApiKeyField = false;
        private string _archivePathInput = "";

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

                GUILayout.Space(4);

                // kOS Archive Path row
                GUILayout.BeginHorizontal();
                GUILayout.Label("kOS Archive:", GUILayout.Width(70));

                GUI.SetNextControlName("SettingsArchivePath");
                string newArchivePath = GUILayout.TextField(_archivePathInput, GUILayout.ExpandWidth(true));
                if (newArchivePath != _archivePathInput)
                {
                    _archivePathInput = newArchivePath;
                    _settings.SetKosArchivePath(newArchivePath);
                }

                GUILayout.EndHorizontal();

                // Archive path validation message or hint
                if (!string.IsNullOrEmpty(_archivePathInput))
                {
                    if (!_settings.IsArchivePathValid)
                    {
                        GUILayout.Label(_settings.ArchivePathValidationError, _validationErrorStyle);
                    }
                    else
                    {
                        var validStyle = new GUIStyle(_statusLabelStyle);
                        validStyle.normal.textColor = COLOR_SUCCESS;
                        GUILayout.Label("Archive folder found", validStyle);
                    }
                }
                else
                {
                    GUILayout.Label("Path to Ships/Script/ folder", _statusLabelStyle);
                }

                GUILayout.EndVertical();
            }

            GUILayout.Space(4);
        }
    }
}
