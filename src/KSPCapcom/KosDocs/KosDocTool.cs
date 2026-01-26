using System;
using System.Diagnostics;

namespace KSPCapcom.KosDocs
{
    /// <summary>
    /// Function-calling tool for searching kOS documentation.
    /// Implements the search_kos_docs tool for OpenAI function calling.
    /// </summary>
    public class KosDocTool
    {
        /// <summary>
        /// The tool name used in function calling.
        /// </summary>
        public const string ToolName = "search_kos_docs";

        /// <summary>
        /// Minimum query length required.
        /// </summary>
        public const int MinQueryLength = 2;

        /// <summary>
        /// Maximum results allowed per query.
        /// </summary>
        public const int MaxResultsLimit = 10;

        /// <summary>
        /// Default number of results if not specified.
        /// </summary>
        public const int DefaultMaxResults = 5;

        private readonly KosDocService _service;

        /// <summary>
        /// Create a new KosDocTool using the singleton KosDocService.
        /// </summary>
        public KosDocTool() : this(KosDocService.Instance)
        {
        }

        /// <summary>
        /// Create a new KosDocTool with a specific service (for testing).
        /// </summary>
        public KosDocTool(KosDocService service)
        {
            _service = service ?? KosDocService.Instance;
        }

        /// <summary>
        /// Execute a documentation search.
        /// Never throws - returns error result on failure.
        /// </summary>
        /// <param name="query">Search query string.</param>
        /// <param name="maxResults">Maximum number of results (1-10, default 5).</param>
        /// <returns>Search result containing entries or error.</returns>
        public KosDocSearchResult Execute(string query, int? maxResults = null)
        {
            var stopwatch = Stopwatch.StartNew();
            int limit = maxResults ?? DefaultMaxResults;

            try
            {
                // Validate query
                if (string.IsNullOrWhiteSpace(query))
                {
                    stopwatch.Stop();
                    CapcomCore.Log($"TELEM|SEARCH|query=|topN={limit}|ms={stopwatch.ElapsedMilliseconds}|results=0|error=empty_query");
                    return KosDocSearchResult.Fail("Query cannot be empty");
                }

                if (query.Length < MinQueryLength)
                {
                    stopwatch.Stop();
                    CapcomCore.Log($"TELEM|SEARCH|query={TruncateQuery(query)}|topN={limit}|ms={stopwatch.ElapsedMilliseconds}|results=0|error=query_too_short");
                    return KosDocSearchResult.Fail($"Query must be at least {MinQueryLength} characters");
                }

                // Clamp limit
                if (limit < 1)
                {
                    limit = 1;
                }
                else if (limit > MaxResultsLimit)
                {
                    limit = MaxResultsLimit;
                }

                // Check service readiness
                if (!_service.IsReady)
                {
                    stopwatch.Stop();
                    CapcomCore.Log($"TELEM|SEARCH|query={TruncateQuery(query)}|topN={limit}|ms={stopwatch.ElapsedMilliseconds}|results=0|error=docs_not_loaded");
                    return KosDocSearchResult.Fail("Documentation not loaded");
                }

                // Perform search
                var entries = _service.Search(query, limit);
                stopwatch.Stop();

                CapcomCore.Log($"TELEM|SEARCH|query={TruncateQuery(query)}|topN={limit}|ms={stopwatch.ElapsedMilliseconds}|results={entries.Count}");
                return KosDocSearchResult.FromDocEntries(entries);
            }
            catch (Exception ex)
            {
                // Never propagate exceptions - return error result
                stopwatch.Stop();
                CapcomCore.Log($"TELEM|SEARCH|query={TruncateQuery(query)}|topN={limit}|ms={stopwatch.ElapsedMilliseconds}|results=0|error=exception");
                CapcomCore.LogError($"[{ToolName}] Search exception: {ex.Message}");
                return KosDocSearchResult.Fail("Internal error during search");
            }
        }

        /// <summary>
        /// Get the tool definition JSON for OpenAI function calling.
        /// </summary>
        /// <returns>JSON string containing the tool definition.</returns>
        public static string GetToolDefinitionJson()
        {
            return @"{
  ""type"": ""function"",
  ""function"": {
    ""name"": """ + ToolName + @""",
    ""description"": ""Search the kOS scripting language documentation for API references, including structures, suffixes, functions, and commands. Use this to verify correct kOS syntax before generating scripts."",
    ""parameters"": {
      ""type"": ""object"",
      ""properties"": {
        ""query"": {
          ""type"": ""string"",
          ""description"": ""Search query for kOS documentation. Can be an identifier like 'SHIP:VELOCITY' or 'ALTITUDE', or a natural language query like 'how to get orbit parameters'.""
        },
        ""max_results"": {
          ""type"": ""integer"",
          ""description"": ""Maximum number of results to return (1-10). Default is 5.""
        }
      },
      ""required"": [""query""]
    }
  }
}";
        }

        /// <summary>
        /// Parse arguments JSON and execute the tool.
        /// </summary>
        /// <param name="argumentsJson">JSON string containing tool arguments.</param>
        /// <returns>Search result containing entries or error.</returns>
        public KosDocSearchResult ExecuteFromJson(string argumentsJson)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(argumentsJson))
                {
                    return KosDocSearchResult.Fail("No arguments provided");
                }

                // Parse query
                string query = ExtractStringValue(argumentsJson, "query");
                if (string.IsNullOrEmpty(query))
                {
                    return KosDocSearchResult.Fail("Missing required parameter: query");
                }

                // Parse max_results (optional)
                int? maxResults = ExtractIntValue(argumentsJson, "max_results");

                return Execute(query, maxResults);
            }
            catch (Exception ex)
            {
                CapcomCore.LogError($"[{ToolName}] Failed to parse arguments: {ex.Message}");
                return KosDocSearchResult.Fail("Failed to parse tool arguments");
            }
        }

        /// <summary>
        /// Extract a string value from simple JSON.
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

            if (json.Substring(valueStart).StartsWith("null", StringComparison.Ordinal))
            {
                return null;
            }

            return null;
        }

        /// <summary>
        /// Extract a quoted string starting at the given index.
        /// </summary>
        private static string ExtractQuotedString(string json, int startIndex)
        {
            if (startIndex >= json.Length || json[startIndex] != '"')
                return null;

            var sb = new System.Text.StringBuilder();
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
                        default: sb.Append(next); i += 2; break;
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
        /// Extract an integer value from simple JSON.
        /// </summary>
        private static int? ExtractIntValue(string json, string key)
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

            var sb = new System.Text.StringBuilder();
            while (valueStart < json.Length && (char.IsDigit(json[valueStart]) || json[valueStart] == '-'))
            {
                sb.Append(json[valueStart]);
                valueStart++;
            }

            if (sb.Length > 0 && int.TryParse(sb.ToString(), out int result))
                return result;

            return null;
        }

        /// <summary>
        /// Truncate query string for telemetry logging to avoid logging full user input.
        /// </summary>
        /// <param name="query">The query to truncate.</param>
        /// <param name="maxLen">Maximum length (default 50).</param>
        /// <returns>Truncated query with ellipsis if needed.</returns>
        private static string TruncateQuery(string query, int maxLen = 50)
        {
            if (string.IsNullOrEmpty(query))
                return string.Empty;

            // Replace pipe characters to avoid breaking telemetry format
            var sanitized = query.Replace("|", " ");

            if (sanitized.Length <= maxLen)
                return sanitized;

            return sanitized.Substring(0, maxLen) + "...";
        }
    }
}
