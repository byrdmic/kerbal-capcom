using System;
using System.Collections.Generic;
using System.Text;

namespace KSPCapcom.LLM.OpenAI
{
    /// <summary>
    /// DTOs and JSON helpers for OpenAI Chat Completions API.
    /// Uses manual JSON serialization to avoid external dependencies.
    /// </summary>

    #region Request DTOs

    /// <summary>
    /// Request body for the Chat Completions API.
    /// </summary>
    public class ChatCompletionRequest
    {
        public string Model { get; set; }
        public List<ChatMessageDto> Messages { get; set; } = new List<ChatMessageDto>();
        public float? Temperature { get; set; }
        public int? MaxTokens { get; set; }
        public bool Stream { get; set; }
        public List<ToolDefinition> Tools { get; set; }
        public string ToolChoice { get; set; }

        /// <summary>
        /// Serialize this request to JSON.
        /// </summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"model\":\"{JsonEscape(Model)}\"");
            sb.Append(",\"messages\":[");

            for (int i = 0; i < Messages.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(Messages[i].ToJson());
            }

            sb.Append("]");

            if (Temperature.HasValue)
            {
                sb.Append($",\"temperature\":{Temperature.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }

            if (MaxTokens.HasValue)
            {
                sb.Append($",\"max_completion_tokens\":{MaxTokens.Value}");
            }

            if (Stream)
            {
                sb.Append(",\"stream\":true");
            }

            if (Tools != null && Tools.Count > 0)
            {
                sb.Append(",\"tools\":[");
                for (int i = 0; i < Tools.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(Tools[i].ToJson());
                }
                sb.Append("]");

                if (!string.IsNullOrEmpty(ToolChoice))
                {
                    sb.Append($",\"tool_choice\":\"{JsonEscape(ToolChoice)}\"");
                }
            }

            sb.Append("}");
            return sb.ToString();
        }

        private static string JsonEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }

    /// <summary>
    /// A single message in the conversation.
    /// </summary>
    public class ChatMessageDto
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public List<ToolCall> ToolCalls { get; set; }
        public string ToolCallId { get; set; }
        public string Name { get; set; }

        public ChatMessageDto() { }

        public ChatMessageDto(string role, string content)
        {
            Role = role;
            Content = content;
        }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"role\":\"{JsonEscape(Role)}\"");

            // For tool role, content can be null but tool_call_id is required
            if (Role == "tool")
            {
                if (!string.IsNullOrEmpty(ToolCallId))
                {
                    sb.Append($",\"tool_call_id\":\"{JsonEscape(ToolCallId)}\"");
                }
                sb.Append($",\"content\":\"{JsonEscape(Content ?? "")}\"");
            }
            else if (ToolCalls != null && ToolCalls.Count > 0)
            {
                // Assistant message with tool_calls - content may be null
                if (Content != null)
                {
                    sb.Append($",\"content\":\"{JsonEscape(Content)}\"");
                }
                else
                {
                    sb.Append(",\"content\":null");
                }
                sb.Append(",\"tool_calls\":[");
                for (int i = 0; i < ToolCalls.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(SerializeToolCall(ToolCalls[i]));
                }
                sb.Append("]");
            }
            else
            {
                sb.Append($",\"content\":\"{JsonEscape(Content)}\"");
            }

            sb.Append("}");
            return sb.ToString();
        }

        private static string SerializeToolCall(ToolCall call)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"id\":\"{JsonEscape(call.Id)}\"");
            sb.Append($",\"type\":\"{JsonEscape(call.Type ?? "function")}\"");
            if (call.Function != null)
            {
                sb.Append(",\"function\":{");
                sb.Append($"\"name\":\"{JsonEscape(call.Function.Name)}\"");
                sb.Append($",\"arguments\":\"{JsonEscape(call.Function.Arguments)}\"");
                sb.Append("}");
            }
            sb.Append("}");
            return sb.ToString();
        }

        private static string JsonEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        /// <summary>
        /// Convert MessageRole enum to OpenAI role string.
        /// </summary>
        public static string RoleToString(MessageRole role)
        {
            switch (role)
            {
                case MessageRole.User: return "user";
                case MessageRole.Assistant: return "assistant";
                case MessageRole.System: return "system";
                case MessageRole.Tool: return "tool";
                default: return "user";
            }
        }
    }

    #endregion

    #region Response DTOs

    /// <summary>
    /// Response from the Chat Completions API.
    /// </summary>
    public class ChatCompletionResponse
    {
        public string Id { get; set; }
        public string Object { get; set; }
        public string Model { get; set; }
        public List<ChatChoice> Choices { get; set; } = new List<ChatChoice>();
        public UsageDto Usage { get; set; }

        /// <summary>
        /// Get the first choice's message content, or empty string if none.
        /// </summary>
        public string GetContent()
        {
            if (Choices == null || Choices.Count == 0)
                return "";
            return Choices[0].Message?.Content ?? "";
        }
    }

    /// <summary>
    /// A single completion choice.
    /// </summary>
    public class ChatChoice
    {
        public int Index { get; set; }
        public ChatMessageDto Message { get; set; }
        public string FinishReason { get; set; }
        public List<ToolCall> ToolCalls { get; set; }
    }

    /// <summary>
    /// Token usage statistics.
    /// </summary>
    public class UsageDto
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }

        public LLMUsage ToLLMUsage()
        {
            return new LLMUsage(PromptTokens, CompletionTokens, TotalTokens);
        }
    }

    /// <summary>
    /// Error response from OpenAI API.
    /// </summary>
    public class OpenAIErrorResponse
    {
        public OpenAIErrorDetail Error { get; set; }
    }

    /// <summary>
    /// Error details from OpenAI API.
    /// </summary>
    public class OpenAIErrorDetail
    {
        public string Message { get; set; }
        public string Type { get; set; }
        public string Code { get; set; }
    }

    #endregion

    #region Tool DTOs

    /// <summary>
    /// A tool definition for function calling.
    /// </summary>
    public class ToolDefinition
    {
        public string Type { get; set; } = "function";
        public FunctionDefinition Function { get; set; }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"type\":\"{JsonEscape(Type)}\"");
            if (Function != null)
            {
                sb.Append(",\"function\":");
                sb.Append(Function.ToJson());
            }
            sb.Append("}");
            return sb.ToString();
        }

        private static string JsonEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }

    /// <summary>
    /// Function definition for a tool.
    /// </summary>
    public class FunctionDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public FunctionParameters Parameters { get; set; }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"name\":\"{JsonEscape(Name)}\"");

            if (!string.IsNullOrEmpty(Description))
            {
                sb.Append($",\"description\":\"{JsonEscape(Description)}\"");
            }

            if (Parameters != null)
            {
                sb.Append(",\"parameters\":");
                sb.Append(Parameters.ToJson());
            }

            sb.Append("}");
            return sb.ToString();
        }

        private static string JsonEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }

    /// <summary>
    /// Parameters schema for a function.
    /// </summary>
    public class FunctionParameters
    {
        public string Type { get; set; } = "object";
        public Dictionary<string, ParameterProperty> Properties { get; set; } = new Dictionary<string, ParameterProperty>();
        public List<string> Required { get; set; } = new List<string>();

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"type\":\"{JsonEscape(Type)}\"");

            sb.Append(",\"properties\":{");
            bool first = true;
            foreach (var kvp in Properties)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append($"\"{JsonEscape(kvp.Key)}\":");
                sb.Append(kvp.Value.ToJson());
            }
            sb.Append("}");

            if (Required.Count > 0)
            {
                sb.Append(",\"required\":[");
                for (int i = 0; i < Required.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append($"\"{JsonEscape(Required[i])}\"");
                }
                sb.Append("]");
            }

            sb.Append("}");
            return sb.ToString();
        }

        private static string JsonEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }

    /// <summary>
    /// A single parameter property definition.
    /// </summary>
    public class ParameterProperty
    {
        public string Type { get; set; }
        public string Description { get; set; }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"type\":\"{JsonEscape(Type)}\"");

            if (!string.IsNullOrEmpty(Description))
            {
                sb.Append($",\"description\":\"{JsonEscape(Description)}\"");
            }

            sb.Append("}");
            return sb.ToString();
        }

        private static string JsonEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }

    /// <summary>
    /// A tool call requested by the model.
    /// </summary>
    public class ToolCall
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public FunctionCall Function { get; set; }
    }

    /// <summary>
    /// A function call within a tool call.
    /// </summary>
    public class FunctionCall
    {
        public string Name { get; set; }
        public string Arguments { get; set; }
    }

    #endregion

    #region JSON Parser

    /// <summary>
    /// Simple JSON parser for OpenAI responses.
    /// Handles only the subset of JSON needed for the Chat Completions API.
    /// </summary>
    public static class JsonParser
    {
        /// <summary>
        /// Parse a ChatCompletionResponse from JSON string.
        /// </summary>
        public static ChatCompletionResponse ParseChatCompletionResponse(string json)
        {
            var response = new ChatCompletionResponse();

            response.Id = ExtractStringValue(json, "id");
            response.Object = ExtractStringValue(json, "object");
            response.Model = ExtractStringValue(json, "model");

            // Parse choices array
            var choicesJson = ExtractArrayValue(json, "choices");
            if (!string.IsNullOrEmpty(choicesJson))
            {
                response.Choices = ParseChoices(choicesJson);
            }

            // Parse usage object
            var usageJson = ExtractObjectValue(json, "usage");
            if (!string.IsNullOrEmpty(usageJson))
            {
                response.Usage = ParseUsage(usageJson);
            }

            return response;
        }

        /// <summary>
        /// Parse an OpenAIErrorResponse from JSON string.
        /// </summary>
        public static OpenAIErrorResponse ParseErrorResponse(string json)
        {
            var response = new OpenAIErrorResponse();

            var errorJson = ExtractObjectValue(json, "error");
            if (!string.IsNullOrEmpty(errorJson))
            {
                response.Error = new OpenAIErrorDetail
                {
                    Message = ExtractStringValue(errorJson, "message"),
                    Type = ExtractStringValue(errorJson, "type"),
                    Code = ExtractStringValue(errorJson, "code")
                };
            }

            return response;
        }

        private static List<ChatChoice> ParseChoices(string choicesJson)
        {
            var choices = new List<ChatChoice>();

            // Simple approach: find each object in the array
            int depth = 0;
            int objectStart = -1;

            for (int i = 0; i < choicesJson.Length; i++)
            {
                char c = choicesJson[i];

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
                        var objectJson = choicesJson.Substring(objectStart, i - objectStart + 1);
                        choices.Add(ParseChoice(objectJson));
                        objectStart = -1;
                    }
                }
            }

            return choices;
        }

        private static ChatChoice ParseChoice(string json)
        {
            var choice = new ChatChoice();
            choice.Index = ExtractIntValue(json, "index");
            choice.FinishReason = ExtractStringValue(json, "finish_reason");

            var messageJson = ExtractObjectValue(json, "message");
            if (!string.IsNullOrEmpty(messageJson))
            {
                choice.Message = new ChatMessageDto
                {
                    Role = ExtractStringValue(messageJson, "role"),
                    Content = ExtractStringValue(messageJson, "content")
                };

                // Parse tool_calls if present
                var toolCallsJson = ExtractArrayValue(messageJson, "tool_calls");
                if (!string.IsNullOrEmpty(toolCallsJson))
                {
                    choice.ToolCalls = ParseToolCalls(toolCallsJson);
                    choice.Message.ToolCalls = choice.ToolCalls;
                }
            }

            return choice;
        }

        private static List<ToolCall> ParseToolCalls(string toolCallsJson)
        {
            var toolCalls = new List<ToolCall>();

            int depth = 0;
            int objectStart = -1;
            bool inString = false;

            for (int i = 0; i < toolCallsJson.Length; i++)
            {
                char c = toolCallsJson[i];

                if (inString)
                {
                    if (c == '\\' && i + 1 < toolCallsJson.Length)
                    {
                        i++; // Skip escaped character
                        continue;
                    }
                    if (c == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                }
                else if (c == '{')
                {
                    if (depth == 0) objectStart = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && objectStart >= 0)
                    {
                        var objectJson = toolCallsJson.Substring(objectStart, i - objectStart + 1);
                        var toolCall = ParseToolCall(objectJson);
                        if (toolCall != null)
                        {
                            toolCalls.Add(toolCall);
                        }
                        objectStart = -1;
                    }
                }
            }

            return toolCalls;
        }

        private static ToolCall ParseToolCall(string json)
        {
            var toolCall = new ToolCall
            {
                Id = ExtractStringValue(json, "id"),
                Type = ExtractStringValue(json, "type") ?? "function"
            };

            var functionJson = ExtractObjectValue(json, "function");
            if (!string.IsNullOrEmpty(functionJson))
            {
                toolCall.Function = new FunctionCall
                {
                    Name = ExtractStringValue(functionJson, "name"),
                    Arguments = ExtractStringValue(functionJson, "arguments")
                };
            }

            return toolCall;
        }

        private static UsageDto ParseUsage(string json)
        {
            return new UsageDto
            {
                PromptTokens = ExtractIntValue(json, "prompt_tokens"),
                CompletionTokens = ExtractIntValue(json, "completion_tokens"),
                TotalTokens = ExtractIntValue(json, "total_tokens")
            };
        }

        /// <summary>
        /// Extract a string value from JSON.
        /// </summary>
        public static string ExtractStringValue(string json, string key)
        {
            var pattern = $"\"{key}\"";
            var keyIndex = json.IndexOf(pattern, StringComparison.Ordinal);
            if (keyIndex < 0) return null;

            // Find the colon after the key
            var colonIndex = json.IndexOf(':', keyIndex + pattern.Length);
            if (colonIndex < 0) return null;

            // Skip whitespace
            var valueStart = colonIndex + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;

            if (valueStart >= json.Length) return null;

            // Check if it's a string (starts with ")
            if (json[valueStart] == '"')
            {
                return ExtractQuotedString(json, valueStart);
            }

            // Check if it's null
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
                            // Unicode escape \uXXXX
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
        /// Extract an integer value from JSON.
        /// </summary>
        public static int ExtractIntValue(string json, string key)
        {
            var pattern = $"\"{key}\"";
            var keyIndex = json.IndexOf(pattern, StringComparison.Ordinal);
            if (keyIndex < 0) return 0;

            var colonIndex = json.IndexOf(':', keyIndex + pattern.Length);
            if (colonIndex < 0) return 0;

            var valueStart = colonIndex + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;

            if (valueStart >= json.Length) return 0;

            // Read digits
            var sb = new StringBuilder();
            while (valueStart < json.Length && (char.IsDigit(json[valueStart]) || json[valueStart] == '-'))
            {
                sb.Append(json[valueStart]);
                valueStart++;
            }

            if (int.TryParse(sb.ToString(), out int result))
                return result;

            return 0;
        }

        /// <summary>
        /// Extract a nested object value from JSON (returns the JSON string of the object).
        /// </summary>
        public static string ExtractObjectValue(string json, string key)
        {
            var pattern = $"\"{key}\"";
            var keyIndex = json.IndexOf(pattern, StringComparison.Ordinal);
            if (keyIndex < 0) return null;

            var colonIndex = json.IndexOf(':', keyIndex + pattern.Length);
            if (colonIndex < 0) return null;

            var valueStart = colonIndex + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;

            if (valueStart >= json.Length || json[valueStart] != '{') return null;

            // Find matching closing brace
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
                    else if (c == '{')
                    {
                        depth++;
                    }
                    else if (c == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return json.Substring(valueStart, i - valueStart + 1);
                        }
                    }
                }

                i++;
            }

            return null;
        }

        /// <summary>
        /// Extract an array value from JSON (returns the JSON string of the array contents).
        /// </summary>
        public static string ExtractArrayValue(string json, string key)
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

            // Find matching closing bracket
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
                            // Return contents without the brackets
                            return json.Substring(valueStart + 1, i - valueStart - 1);
                        }
                    }
                }

                i++;
            }

            return null;
        }
    }

    #endregion
}
