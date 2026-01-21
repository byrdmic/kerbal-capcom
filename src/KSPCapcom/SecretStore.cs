using System;
using System.IO;

namespace KSPCapcom
{
    /// <summary>
    /// Secure storage for API keys using KSP ConfigNode format.
    /// Keys are stored in a gitignored secrets.cfg file.
    /// </summary>
    public class SecretStore
    {
        private const string SECRETS_FILE = "secrets.cfg";
        private const string NODE_NAME = "CAPCOM_SECRETS";
        private const string API_KEY_FIELD = "apiKey";

        private string _apiKey = "";

        /// <summary>
        /// The stored API key. Empty string if not configured.
        /// </summary>
        public string ApiKey => _apiKey;

        /// <summary>
        /// Whether an API key is configured (non-empty).
        /// </summary>
        public bool HasApiKey => !string.IsNullOrEmpty(_apiKey);

        /// <summary>
        /// Get the full path to the secrets file.
        /// </summary>
        private string SecretsFilePath
        {
            get
            {
                // Location: GameData/KSPCapcom/Plugins/secrets.cfg (same folder as the DLL)
                var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var assemblyDir = Path.GetDirectoryName(assemblyPath);
                return Path.Combine(assemblyDir, SECRETS_FILE);
            }
        }

        /// <summary>
        /// Load secrets from the config file.
        /// Creates the file if it doesn't exist.
        /// </summary>
        public void Load()
        {
            var filePath = SecretsFilePath;

            if (!File.Exists(filePath))
            {
                CapcomCore.Log($"Secrets file not found at {filePath}");
                _apiKey = "";
                return;
            }

            try
            {
                var config = ConfigNode.Load(filePath);
                if (config == null)
                {
                    CapcomCore.LogWarning($"Failed to parse secrets file at {filePath}");
                    _apiKey = "";
                    return;
                }

                var secretsNode = config.GetNode(NODE_NAME);
                if (secretsNode == null)
                {
                    CapcomCore.LogWarning($"No {NODE_NAME} node found in secrets file");
                    _apiKey = "";
                    return;
                }

                _apiKey = secretsNode.GetValue(API_KEY_FIELD) ?? "";

                // Log status without revealing the key
                if (HasApiKey)
                {
                    CapcomCore.Log("API key loaded from secrets file");
                }
                else
                {
                    CapcomCore.Log("Secrets file found but API key is empty");
                }
            }
            catch (Exception ex)
            {
                CapcomCore.LogError($"Error loading secrets: {ex.Message}");
                _apiKey = "";
            }
        }

        /// <summary>
        /// Save secrets to the config file.
        /// </summary>
        public void Save()
        {
            var filePath = SecretsFilePath;

            try
            {
                var config = new ConfigNode();
                var secretsNode = config.AddNode(NODE_NAME);
                secretsNode.AddValue(API_KEY_FIELD, _apiKey);

                config.Save(filePath);
                CapcomCore.Log("Secrets saved");
            }
            catch (Exception ex)
            {
                CapcomCore.LogError($"Error saving secrets: {ex.Message}");
            }
        }

        /// <summary>
        /// Set the API key and optionally save immediately.
        /// </summary>
        /// <param name="value">The API key value.</param>
        /// <param name="save">Whether to save to disk immediately.</param>
        public void SetApiKey(string value, bool save = true)
        {
            _apiKey = value ?? "";
            if (save)
            {
                Save();
            }
        }
    }
}
