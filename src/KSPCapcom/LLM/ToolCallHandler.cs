using System;
using KSPCapcom.KosDocs;
using KSPCapcom.Validation;

namespace KSPCapcom.LLM
{
    /// <summary>
    /// Dispatcher for handling tool calls from the LLM.
    /// Routes tool calls to their implementations and returns results as JSON.
    /// </summary>
    public static class ToolCallHandler
    {
        /// <summary>
        /// Execute a tool call and return the result as JSON.
        /// Never throws - returns error JSON on failure.
        /// </summary>
        /// <param name="toolName">Name of the tool to execute.</param>
        /// <param name="argumentsJson">JSON string containing tool arguments.</param>
        /// <returns>JSON string containing the tool result.</returns>
        public static string Execute(string toolName, string argumentsJson)
        {
            return Execute(toolName, argumentsJson, null);
        }

        /// <summary>
        /// Execute a tool call and return the result as JSON, optionally tracking retrieved docs.
        /// Never throws - returns error JSON on failure.
        /// </summary>
        /// <param name="toolName">Name of the tool to execute.</param>
        /// <param name="argumentsJson">JSON string containing tool arguments.</param>
        /// <param name="docTracker">Optional tracker for retrieved documentation entries.</param>
        /// <returns>JSON string containing the tool result.</returns>
        public static string Execute(string toolName, string argumentsJson, DocEntryTracker docTracker)
        {
            try
            {
                CapcomCore.Log($"[ToolCallHandler] Executing tool: {toolName}");

                switch (toolName)
                {
                    case KosDocTool.ToolName:
                        return ExecuteKosDocSearch(argumentsJson, docTracker);

                    default:
                        CapcomCore.LogWarning($"[ToolCallHandler] Unknown tool: {toolName}");
                        return CreateErrorJson($"Unknown tool: {toolName}");
                }
            }
            catch (Exception ex)
            {
                CapcomCore.LogError($"[ToolCallHandler] Failed to execute {toolName}: {ex.Message}");
                return CreateErrorJson("Internal error executing tool");
            }
        }

        /// <summary>
        /// Execute the kOS documentation search tool.
        /// </summary>
        private static string ExecuteKosDocSearch(string argumentsJson, DocEntryTracker docTracker)
        {
            var tool = new KosDocTool();
            var result = tool.ExecuteFromJson(argumentsJson);

            // Track retrieved entries for validation
            if (docTracker != null && result.Success && result.SourceEntries != null)
            {
                docTracker.Add(result.SourceEntries);
            }

            return result.ToJson();
        }

        /// <summary>
        /// Create an error JSON response.
        /// </summary>
        private static string CreateErrorJson(string message)
        {
            var escaped = message
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");

            return $"{{\"success\":false,\"error\":\"{escaped}\"}}";
        }
    }
}
