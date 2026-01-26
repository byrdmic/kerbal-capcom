using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace KSPCapcom.KosDocs
{
    /// <summary>
    /// Loads kOS documentation from JSON file with async support.
    /// Uses manual JSON parsing to avoid external dependencies.
    /// </summary>
    public class KosDocLoader
    {
        private const string DOCS_FILENAME = "kos_docs.json";

        // Current supported schema major version
        private const int SUPPORTED_SCHEMA_MAJOR = 1;

        private readonly string _docsFilePath;
        private KosDocIndex _index;
        private bool _isLoading;
        private string _loadError;

        /// <summary>
        /// The loaded documentation index, or null if not loaded.
        /// </summary>
        public KosDocIndex Index => _index;

        /// <summary>
        /// Whether loading is in progress.
        /// </summary>
        public bool IsLoading => _isLoading;

        /// <summary>
        /// Error message from last load attempt, or null if successful.
        /// </summary>
        public string LoadError => _loadError;

        /// <summary>
        /// Whether the index is loaded and ready for queries.
        /// </summary>
        public bool IsReady => _index != null && _index.IsLoaded && !_isLoading;

        /// <summary>
        /// Create a loader with the default file path.
        /// </summary>
        public KosDocLoader() : this(GetDefaultFilePath())
        {
        }

        /// <summary>
        /// Create a loader with a custom file path.
        /// </summary>
        public KosDocLoader(string filePath)
        {
            _docsFilePath = filePath;
        }

        /// <summary>
        /// Get the default path to the docs file (same folder as DLL).
        /// </summary>
        private static string GetDefaultFilePath()
        {
            var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var assemblyDir = Path.GetDirectoryName(assemblyPath);
            return Path.Combine(assemblyDir, DOCS_FILENAME);
        }

        /// <summary>
        /// Load the documentation index synchronously.
        /// For testing and simple use cases.
        /// </summary>
        public bool LoadSync()
        {
            try
            {
                _isLoading = true;
                _loadError = null;

                if (!File.Exists(_docsFilePath))
                {
                    _loadError = $"Documentation file not found: {_docsFilePath}";
                    CapcomCore.LogWarning($"KosDocLoader: {_loadError}");
                    return false;
                }

                var json = File.ReadAllText(_docsFilePath, Encoding.UTF8);
                var index = ParseIndex(json);

                if (index == null)
                {
                    _loadError = "Failed to parse documentation JSON";
                    return false;
                }

                _index = index;
                CapcomCore.Log($"KosDocLoader: Loaded {_index.Count} entries (content version {_index.ContentVersion})");
                return true;
            }
            catch (Exception ex)
            {
                _loadError = ex.Message;
                CapcomCore.LogError($"KosDocLoader: {ex.Message}");
                return false;
            }
            finally
            {
                _isLoading = false;
            }
        }

        /// <summary>
        /// Load the documentation index asynchronously on a background thread.
        /// Calls onComplete on the main thread when done.
        /// </summary>
        /// <param name="onComplete">Callback with success status.</param>
        public void LoadAsync(Action<bool> onComplete = null)
        {
            if (_isLoading)
            {
                CapcomCore.LogWarning("KosDocLoader: Load already in progress");
                return;
            }

            _isLoading = true;
            _loadError = null;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                bool success = false;
                KosDocIndex index = null;

                try
                {
                    if (!File.Exists(_docsFilePath))
                    {
                        _loadError = $"Documentation file not found: {_docsFilePath}";
                        CapcomCore.LogWarning($"KosDocLoader: {_loadError}");
                    }
                    else
                    {
                        var json = File.ReadAllText(_docsFilePath, Encoding.UTF8);
                        index = ParseIndex(json);

                        if (index == null)
                        {
                            _loadError = "Failed to parse documentation JSON";
                        }
                        else
                        {
                            success = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _loadError = ex.Message;
                    CapcomCore.LogError($"KosDocLoader: {ex.Message}");
                }

                // Complete on main thread
                var finalIndex = index;
                var finalSuccess = success;

                MainThreadDispatcher.Instance.Enqueue(() =>
                {
                    _isLoading = false;

                    if (finalSuccess && finalIndex != null)
                    {
                        _index = finalIndex;
                        CapcomCore.Log($"KosDocLoader: Loaded {_index.Count} entries (content version {_index.ContentVersion})");
                    }

                    onComplete?.Invoke(finalSuccess);
                });
            });
        }

        /// <summary>
        /// Parse the documentation index from JSON.
        /// </summary>
        private KosDocIndex ParseIndex(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            var index = new KosDocIndex();

            // Parse metadata
            index.SchemaVersion = ExtractStringValue(json, "schemaVersion");
            index.ContentVersion = ExtractStringValue(json, "contentVersion");
            index.KosMinVersion = ExtractStringValue(json, "kosMinVersion");
            index.SourceUrl = ExtractStringValue(json, "sourceUrl");

            var generatedAt = ExtractStringValue(json, "generatedAt");
            if (!string.IsNullOrEmpty(generatedAt))
            {
                if (DateTime.TryParse(generatedAt, out var dt))
                {
                    index.GeneratedAt = dt;
                }
            }

            // Validate schema version
            if (!ValidateSchemaVersion(index.SchemaVersion))
            {
                CapcomCore.LogWarning($"KosDocLoader: Incompatible schema version {index.SchemaVersion}");
                return null;
            }

            // Parse entries array
            var entriesJson = ExtractArrayValue(json, "entries");
            if (!string.IsNullOrEmpty(entriesJson))
            {
                var entries = ParseEntries(entriesJson);
                foreach (var entry in entries)
                {
                    index.AddEntry(entry);
                }
            }

            return index;
        }

        /// <summary>
        /// Validate that the schema version is compatible.
        /// </summary>
        private bool ValidateSchemaVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
            {
                return false;
            }

            var parts = version.Split('.');
            if (parts.Length < 1)
            {
                return false;
            }

            if (int.TryParse(parts[0], out int major))
            {
                return major == SUPPORTED_SCHEMA_MAJOR;
            }

            return false;
        }

        /// <summary>
        /// Parse the entries array from JSON.
        /// </summary>
        private List<DocEntry> ParseEntries(string entriesJson)
        {
            var entries = new List<DocEntry>();

            // Find each object in the array
            int depth = 0;
            int objectStart = -1;

            for (int i = 0; i < entriesJson.Length; i++)
            {
                char c = entriesJson[i];

                // Skip strings to avoid counting braces inside strings
                if (c == '"')
                {
                    i = SkipString(entriesJson, i);
                    continue;
                }

                if (c == '{')
                {
                    if (depth == 0) objectStart = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && objectStart >= 0)
                    {
                        var objectJson = entriesJson.Substring(objectStart, i - objectStart + 1);
                        var entry = ParseEntry(objectJson);
                        if (entry != null)
                        {
                            entries.Add(entry);
                        }
                        objectStart = -1;
                    }
                }
            }

            return entries;
        }

        /// <summary>
        /// Parse a single entry from JSON.
        /// </summary>
        private DocEntry ParseEntry(string json)
        {
            var entry = new DocEntry();

            entry.Id = ExtractStringValue(json, "id");
            entry.Name = ExtractStringValue(json, "name");
            entry.ParentStructure = ExtractStringValue(json, "parentStructure");
            entry.ReturnType = ExtractStringValue(json, "returnType");
            entry.Signature = ExtractStringValue(json, "signature");
            entry.Description = ExtractStringValue(json, "description");
            entry.Snippet = ExtractStringValue(json, "snippet");
            entry.SourceRef = ExtractStringValue(json, "sourceRef");
            entry.DeprecationNote = ExtractStringValue(json, "deprecationNote");

            // Parse type enum
            var typeStr = ExtractStringValue(json, "type");
            entry.Type = ParseEntryType(typeStr);

            // Parse access enum
            var accessStr = ExtractStringValue(json, "access");
            entry.Access = ParseAccessMode(accessStr);

            // Parse deprecated boolean
            entry.Deprecated = ExtractBoolValue(json, "deprecated");

            // Parse arrays
            entry.Tags = ExtractStringArray(json, "tags");
            entry.Aliases = ExtractStringArray(json, "aliases");
            entry.Related = ExtractStringArray(json, "related");

            // Parse new metadata fields (schema 1.1.0+)
            entry.Category = ExtractStringValue(json, "category");
            entry.UsageFrequency = ExtractStringValue(json, "usageFrequency");

            // Validate required fields
            if (string.IsNullOrEmpty(entry.Id) || string.IsNullOrEmpty(entry.Name))
            {
                return null;
            }

            return entry;
        }

        private DocEntryType ParseEntryType(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                return DocEntryType.Structure;
            }

            switch (type.ToLowerInvariant())
            {
                case "structure": return DocEntryType.Structure;
                case "suffix": return DocEntryType.Suffix;
                case "function": return DocEntryType.Function;
                case "keyword": return DocEntryType.Keyword;
                case "constant": return DocEntryType.Constant;
                case "command": return DocEntryType.Command;
                default: return DocEntryType.Structure;
            }
        }

        private DocAccessMode ParseAccessMode(string access)
        {
            if (string.IsNullOrEmpty(access))
            {
                return DocAccessMode.None;
            }

            switch (access.ToLowerInvariant())
            {
                case "get": return DocAccessMode.Get;
                case "set": return DocAccessMode.Set;
                case "get/set": return DocAccessMode.GetSet;
                case "method": return DocAccessMode.Method;
                default: return DocAccessMode.None;
            }
        }

        #region JSON Parsing Helpers

        /// <summary>
        /// Extract a string value from JSON.
        /// </summary>
        private static string ExtractStringValue(string json, string key)
        {
            var pattern = $"\"{key}\"";
            var keyIndex = json.IndexOf(pattern, StringComparison.Ordinal);
            if (keyIndex < 0) return null;

            var colonIndex = json.IndexOf(':', keyIndex + pattern.Length);
            if (colonIndex < 0) return null;

            var valueStart = colonIndex + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;

            if (valueStart >= json.Length) return null;

            if (json[valueStart] == '"')
            {
                return ExtractQuotedString(json, valueStart);
            }

            if (json.Substring(valueStart).StartsWith("null"))
            {
                return null;
            }

            return null;
        }

        private static string ExtractQuotedString(string json, int startIndex)
        {
            if (startIndex >= json.Length || json[startIndex] != '"')
                return null;

            var sb = new StringBuilder();
            int i = startIndex + 1;

            while (i < json.Length)
            {
                char c = json[i];

                if (c == '\\' && i + 1 < json.Length)
                {
                    char next = json[i + 1];
                    switch (next)
                    {
                        case '"': sb.Append('"'); i += 2; break;
                        case '\\': sb.Append('\\'); i += 2; break;
                        case 'n': sb.Append('\n'); i += 2; break;
                        case 'r': sb.Append('\r'); i += 2; break;
                        case 't': sb.Append('\t'); i += 2; break;
                        case 'u':
                            if (i + 5 < json.Length)
                            {
                                var hex = json.Substring(i + 2, 4);
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int code))
                                {
                                    sb.Append((char)code);
                                }
                                i += 6;
                            }
                            else
                            {
                                i++;
                            }
                            break;
                        default:
                            sb.Append(next);
                            i += 2;
                            break;
                    }
                }
                else if (c == '"')
                {
                    break;
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Extract a boolean value from JSON.
        /// </summary>
        private static bool ExtractBoolValue(string json, string key)
        {
            var pattern = $"\"{key}\"";
            var keyIndex = json.IndexOf(pattern, StringComparison.Ordinal);
            if (keyIndex < 0) return false;

            var colonIndex = json.IndexOf(':', keyIndex + pattern.Length);
            if (colonIndex < 0) return false;

            var valueStart = colonIndex + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;

            if (valueStart >= json.Length) return false;

            var remaining = json.Substring(valueStart);
            if (remaining.StartsWith("true")) return true;
            if (remaining.StartsWith("false")) return false;

            return false;
        }

        /// <summary>
        /// Extract an array value from JSON (returns contents without brackets).
        /// </summary>
        private static string ExtractArrayValue(string json, string key)
        {
            var pattern = $"\"{key}\"";
            var keyIndex = json.IndexOf(pattern, StringComparison.Ordinal);
            if (keyIndex < 0) return null;

            var colonIndex = json.IndexOf(':', keyIndex + pattern.Length);
            if (colonIndex < 0) return null;

            var valueStart = colonIndex + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;

            if (valueStart >= json.Length || json[valueStart] != '[') return null;

            int depth = 0;
            int i = valueStart;
            bool inString = false;

            while (i < json.Length)
            {
                char c = json[i];

                if (inString)
                {
                    if (c == '\\' && i + 1 < json.Length)
                    {
                        i += 2;
                        continue;
                    }
                    if (c == '"')
                    {
                        inString = false;
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inString = true;
                    }
                    else if (c == '[')
                    {
                        depth++;
                    }
                    else if (c == ']')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return json.Substring(valueStart + 1, i - valueStart - 1);
                        }
                    }
                }

                i++;
            }

            return null;
        }

        /// <summary>
        /// Extract a string array from JSON.
        /// </summary>
        private static List<string> ExtractStringArray(string json, string key)
        {
            var result = new List<string>();
            var arrayContents = ExtractArrayValue(json, key);
            if (string.IsNullOrEmpty(arrayContents))
            {
                return result;
            }

            // Parse quoted strings from array
            int i = 0;
            while (i < arrayContents.Length)
            {
                // Skip to next quote
                while (i < arrayContents.Length && arrayContents[i] != '"')
                    i++;

                if (i >= arrayContents.Length)
                    break;

                var str = ExtractQuotedString(arrayContents, i);
                if (str != null)
                {
                    result.Add(str);
                }

                // Skip past this string
                i = SkipString(arrayContents, i) + 1;
            }

            return result;
        }

        /// <summary>
        /// Skip past a quoted string, returning the index of the closing quote.
        /// </summary>
        private static int SkipString(string json, int startIndex)
        {
            if (startIndex >= json.Length || json[startIndex] != '"')
                return startIndex;

            int i = startIndex + 1;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    i += 2;
                }
                else if (c == '"')
                {
                    return i;
                }
                else
                {
                    i++;
                }
            }

            return i;
        }

        #endregion
    }
}
