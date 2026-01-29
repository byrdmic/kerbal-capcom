using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KSPCapcom
{
    /// <summary>
    /// Represents a pinned prompt with display label and sort order.
    /// </summary>
    public struct PinnedPrompt
    {
        /// <summary>
        /// Full prompt text.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Short display label (max ~20 chars). If empty, UI will truncate Text.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Sort order for stable display ordering. Lower values appear first.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Get the display label, falling back to truncated text if label is empty.
        /// </summary>
        public string DisplayLabel => !string.IsNullOrEmpty(Label) ? Label : TruncateText(Text, 20);

        private static string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text ?? "";

            // Try to truncate at word boundary
            int lastSpace = text.LastIndexOf(' ', maxLength - 1);
            if (lastSpace > maxLength / 2)
                return text.Substring(0, lastSpace) + "...";

            return text.Substring(0, maxLength - 3) + "...";
        }
    }

    /// <summary>
    /// Persistent storage for user-pinned prompts using KSP ConfigNode format.
    /// Prompts are stored in a local config file and persist across game sessions.
    /// </summary>
    public class PinnedPromptStore
    {
        private const string CONFIG_FILE = "pinned_prompts.cfg";
        private const string ROOT_NODE = "CAPCOM_PINNED_PROMPTS";
        private const string PROMPT_NODE = "PROMPT";
        private const int MAX_PINS = 10;

        /// <summary>
        /// Default LKO ascent prompt to pre-seed on first run.
        /// </summary>
        public const string DEFAULT_ASCENT_PROMPT = "Write a kOS script for LKO ascent for this craft";
        private const string DEFAULT_ASCENT_LABEL = "LKO Ascent";

        private readonly List<PinnedPrompt> _prompts = new List<PinnedPrompt>();
        private int _nextOrder = 0;

        /// <summary>
        /// Number of currently pinned prompts.
        /// </summary>
        public int Count => _prompts.Count;

        /// <summary>
        /// Whether the store is at maximum capacity.
        /// </summary>
        public bool IsFull => _prompts.Count >= MAX_PINS;

        /// <summary>
        /// Get the full path to the config file.
        /// </summary>
        private string ConfigFilePath
        {
            get
            {
                var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var assemblyDir = Path.GetDirectoryName(assemblyPath);
                return Path.Combine(assemblyDir, CONFIG_FILE);
            }
        }

        /// <summary>
        /// Load pinned prompts from config file.
        /// If file doesn't exist, pre-seeds with default LKO ascent prompt.
        /// </summary>
        public void Load()
        {
            var filePath = ConfigFilePath;
            _prompts.Clear();
            _nextOrder = 0;

            if (!File.Exists(filePath))
            {
                CapcomCore.Log($"Pinned prompts file not found at {filePath}, creating with defaults");
                SeedDefaults();
                Save();
                return;
            }

            try
            {
                var config = ConfigNode.Load(filePath);
                if (config == null)
                {
                    CapcomCore.LogWarning($"Failed to parse pinned prompts file at {filePath}");
                    SeedDefaults();
                    return;
                }

                var rootNode = config.GetNode(ROOT_NODE);
                if (rootNode == null)
                {
                    CapcomCore.LogWarning($"No {ROOT_NODE} node found in pinned prompts file");
                    SeedDefaults();
                    return;
                }

                foreach (var promptNode in rootNode.GetNodes(PROMPT_NODE))
                {
                    var text = promptNode.GetValue("text") ?? "";
                    var label = promptNode.GetValue("label") ?? "";
                    int.TryParse(promptNode.GetValue("order") ?? "0", out int order);

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        _prompts.Add(new PinnedPrompt
                        {
                            Text = text,
                            Label = label,
                            Order = order
                        });

                        if (order >= _nextOrder)
                            _nextOrder = order + 1;
                    }
                }

                // Sort by order
                _prompts.Sort((a, b) => a.Order.CompareTo(b.Order));

                CapcomCore.Log($"Loaded {_prompts.Count} pinned prompt(s)");
            }
            catch (Exception ex)
            {
                CapcomCore.LogError($"Error loading pinned prompts: {ex.Message}");
                SeedDefaults();
            }
        }

        /// <summary>
        /// Save pinned prompts to config file.
        /// </summary>
        public void Save()
        {
            var filePath = ConfigFilePath;

            try
            {
                var config = new ConfigNode();
                var rootNode = config.AddNode(ROOT_NODE);

                foreach (var prompt in _prompts)
                {
                    var promptNode = rootNode.AddNode(PROMPT_NODE);
                    promptNode.AddValue("text", prompt.Text);
                    promptNode.AddValue("label", prompt.Label ?? "");
                    promptNode.AddValue("order", prompt.Order);
                }

                config.Save(filePath);
                CapcomCore.Log($"Saved {_prompts.Count} pinned prompt(s)");
            }
            catch (Exception ex)
            {
                CapcomCore.LogError($"Error saving pinned prompts: {ex.Message}");
            }
        }

        /// <summary>
        /// Pin a new prompt. Returns false if at capacity or prompt already pinned.
        /// </summary>
        /// <param name="text">Full prompt text.</param>
        /// <param name="label">Optional short display label.</param>
        /// <returns>True if successfully pinned, false otherwise.</returns>
        public bool Pin(string text, string label = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (IsFull)
            {
                CapcomCore.LogWarning($"Cannot pin: at maximum capacity ({MAX_PINS})");
                return false;
            }

            if (IsPinned(text))
            {
                CapcomCore.Log("Prompt already pinned");
                return false;
            }

            _prompts.Add(new PinnedPrompt
            {
                Text = text.Trim(),
                Label = label ?? "",
                Order = _nextOrder++
            });

            CapcomCore.Log($"Pinned prompt: {(label ?? text.Substring(0, Math.Min(30, text.Length)))}...");
            return true;
        }

        /// <summary>
        /// Unpin a prompt by text match.
        /// </summary>
        /// <param name="text">Prompt text to unpin.</param>
        /// <returns>True if found and removed, false otherwise.</returns>
        public bool Unpin(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            var index = _prompts.FindIndex(p => p.Text == trimmed);

            if (index < 0)
                return false;

            var removed = _prompts[index];
            _prompts.RemoveAt(index);
            CapcomCore.Log($"Unpinned prompt: {removed.DisplayLabel}");
            return true;
        }

        /// <summary>
        /// Check if a prompt is currently pinned.
        /// </summary>
        /// <param name="text">Prompt text to check.</param>
        /// <returns>True if pinned, false otherwise.</returns>
        public bool IsPinned(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            return _prompts.Any(p => p.Text == trimmed);
        }

        /// <summary>
        /// Get all pinned prompts in display order.
        /// </summary>
        /// <returns>Read-only list of pinned prompts.</returns>
        public IReadOnlyList<PinnedPrompt> GetPinned()
        {
            return _prompts.AsReadOnly();
        }

        /// <summary>
        /// Generate a label from prompt text (first ~20 chars at word boundary).
        /// </summary>
        public static string GenerateLabel(string text, int maxLength = 20)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            text = text.Trim();
            if (text.Length <= maxLength)
                return text;

            int lastSpace = text.LastIndexOf(' ', maxLength - 1);
            if (lastSpace > maxLength / 2)
                return text.Substring(0, lastSpace);

            return text.Substring(0, maxLength - 3) + "...";
        }

        /// <summary>
        /// Seed default prompts on first run.
        /// </summary>
        private void SeedDefaults()
        {
            _prompts.Clear();
            _nextOrder = 0;

            Pin(DEFAULT_ASCENT_PROMPT, DEFAULT_ASCENT_LABEL);
        }
    }
}
