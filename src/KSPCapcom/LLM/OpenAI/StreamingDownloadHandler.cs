using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;

namespace KSPCapcom.LLM.OpenAI
{
    /// <summary>
    /// Custom download handler for processing OpenAI's Server-Sent Events (SSE) streaming format.
    /// Parses "data: {...}\n\n" chunks incrementally as they arrive.
    /// Supports both content streaming and tool call accumulation.
    /// </summary>
    public class StreamingDownloadHandler : DownloadHandlerScript
    {
        // Buffer size for receiving streaming data from Unity
        private const int BUFFER_SIZE = 4096;

        private readonly Action<string> _onChunk;
        private readonly StringBuilder _buffer;
        private readonly StringBuilder _completeResponse;
        private readonly List<ToolCallAccumulator> _toolCallAccumulators;
        private byte[] _partialBytes;
        private bool _isComplete;
        private string _finishReason;

        /// <summary>
        /// Get the complete accumulated response text.
        /// </summary>
        public string CompleteResponse => _completeResponse.ToString();

        /// <summary>
        /// Whether the stream has completed (received [DONE] marker).
        /// </summary>
        public bool IsComplete => _isComplete;

        /// <summary>
        /// The finish reason from the final chunk (e.g., "stop", "tool_calls", "length").
        /// </summary>
        public string FinishReason => _finishReason;

        /// <summary>
        /// Whether this response contains tool calls.
        /// </summary>
        public bool HasToolCalls => _finishReason == "tool_calls" && _toolCallAccumulators.Count > 0;

        /// <summary>
        /// Get the accumulated tool calls, if any.
        /// </summary>
        public List<ToolCall> GetToolCalls()
        {
            if (!HasToolCalls)
                return null;

            var toolCalls = new List<ToolCall>();
            foreach (var accumulator in _toolCallAccumulators)
            {
                toolCalls.Add(accumulator.Build());
            }
            return toolCalls;
        }

        /// <summary>
        /// Create a new streaming download handler.
        /// </summary>
        /// <param name="onChunk">Callback invoked for each content chunk received.</param>
        /// <remarks>
        /// The base class DownloadHandlerScript MUST be initialized with a pre-allocated buffer.
        /// Without this, Unity has no memory to write incoming data into, and ReceiveData() will
        /// never be called (or called with empty data), resulting in zero chunks being processed.
        /// </remarks>
        public StreamingDownloadHandler(Action<string> onChunk) : base(new byte[BUFFER_SIZE])
        {
            _onChunk = onChunk ?? throw new ArgumentNullException(nameof(onChunk));
            _buffer = new StringBuilder();
            _completeResponse = new StringBuilder();
            _toolCallAccumulators = new List<ToolCallAccumulator>();
            _isComplete = false;
            _finishReason = null;
        }

        /// <summary>
        /// Called by Unity when data is received from the network.
        /// Processes SSE format: "data: {...}\n\n"
        /// </summary>
        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            // Debug: confirm ReceiveData is being called
            CapcomCore.Log($"StreamingDownloadHandler.ReceiveData: {dataLength} bytes");

            if (data == null || dataLength == 0)
                return true;

            try
            {
                // Handle partial UTF-8 characters from previous chunk
                byte[] fullData;
                int fullLength;

                if (_partialBytes != null && _partialBytes.Length > 0)
                {
                    fullLength = _partialBytes.Length + dataLength;
                    fullData = new byte[fullLength];
                    Array.Copy(_partialBytes, 0, fullData, 0, _partialBytes.Length);
                    Array.Copy(data, 0, fullData, _partialBytes.Length, dataLength);
                    _partialBytes = null;
                }
                else
                {
                    fullData = data;
                    fullLength = dataLength;
                }

                // Try to decode UTF-8
                string text;
                try
                {
                    text = Encoding.UTF8.GetString(fullData, 0, fullLength);
                }
                catch (ArgumentException)
                {
                    // Incomplete UTF-8 sequence at end of buffer
                    // Save last few bytes for next chunk
                    if (fullLength > 4)
                    {
                        int partialLength = Math.Min(4, fullLength);
                        _partialBytes = new byte[partialLength];
                        Array.Copy(fullData, fullLength - partialLength, _partialBytes, 0, partialLength);

                        // Try decoding without the partial bytes
                        text = Encoding.UTF8.GetString(fullData, 0, fullLength - partialLength);
                    }
                    else
                    {
                        // Entire buffer is incomplete, save it
                        _partialBytes = new byte[fullLength];
                        Array.Copy(fullData, 0, _partialBytes, 0, fullLength);
                        return true;
                    }
                }

                // Add to buffer
                _buffer.Append(text);

                // Process complete SSE lines (ending with \n\n)
                ProcessBuffer();

                return true;
            }
            catch (Exception ex)
            {
                CapcomCore.LogError($"StreamingDownloadHandler error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Process the buffer and extract complete SSE events.
        /// </summary>
        private void ProcessBuffer()
        {
            string bufferContent = _buffer.ToString();

            // Normalize line endings: \r\n -> \n (handles Windows/HTTP line endings)
            bufferContent = bufferContent.Replace("\r\n", "\n");

            int lastProcessed = 0;

            while (true)
            {
                // Look for complete SSE event (ends with \n\n)
                int eventEnd = bufferContent.IndexOf("\n\n", lastProcessed, StringComparison.Ordinal);
                if (eventEnd < 0)
                    break; // No complete event yet

                // Extract the event
                string eventText = bufferContent.Substring(lastProcessed, eventEnd - lastProcessed);
                lastProcessed = eventEnd + 2; // Skip the \n\n

                // Process the event
                ProcessEvent(eventText);

                if (_isComplete)
                    break;
            }

            // Update buffer with normalized content minus processed portion
            _buffer.Clear();
            if (lastProcessed < bufferContent.Length)
            {
                _buffer.Append(bufferContent.Substring(lastProcessed));
            }
        }

        /// <summary>
        /// Process a single SSE event.
        /// </summary>
        private void ProcessEvent(string eventText)
        {
            // Debug: log full event (not truncated) to diagnose content extraction
            CapcomCore.Log($"SSE event (full): {eventText}");

            // SSE format: "data: {...}"
            if (!eventText.StartsWith("data: "))
                return;

            string dataContent = eventText.Substring(6).Trim();

            // Check for [DONE] marker
            if (dataContent == "[DONE]")
            {
                _isComplete = true;
                return;
            }

            // Parse JSON chunk
            try
            {
                // Extract finish_reason if present
                var finishReason = ExtractFinishReason(dataContent);
                if (!string.IsNullOrEmpty(finishReason))
                {
                    _finishReason = finishReason;
                    CapcomCore.Log($"SSE finish_reason: {finishReason}");
                }

                // Extract delta content from: {"choices":[{"delta":{"content":"text"},...}]}
                var deltaContent = ExtractDeltaContent(dataContent);

                if (!string.IsNullOrEmpty(deltaContent))
                {
                    // Accumulate complete response
                    _completeResponse.Append(deltaContent);

                    // Notify callback with accumulated text
                    _onChunk?.Invoke(_completeResponse.ToString());
                }

                // Extract tool calls from delta if present
                ExtractAndAccumulateToolCalls(dataContent);

                // Check for finish_reason: "length" with no content - indicates token limit hit
                if (_finishReason == "length" && _completeResponse.Length == 0 && _toolCallAccumulators.Count == 0)
                {
                    CapcomCore.LogWarning("API returned finish_reason='length' with no content - max_tokens may be too low or account has output limits");
                }
            }
            catch (Exception ex)
            {
                CapcomCore.LogError($"Failed to parse streaming chunk: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract the content from a streaming delta chunk.
        /// Parses: choices[0].delta.content
        /// </summary>
        private string ExtractDeltaContent(string json)
        {
            // Debug: log full input JSON
            CapcomCore.Log($"ExtractDeltaContent input: {json}");

            // Find the choices array
            var choicesJson = JsonParser.ExtractArrayValue(json, "choices");
            CapcomCore.Log($"ExtractDeltaContent choices: {choicesJson ?? "(null)"}");
            if (string.IsNullOrEmpty(choicesJson))
                return null;

            // Find the first choice object
            int objectStart = choicesJson.IndexOf('{');
            if (objectStart < 0)
            {
                CapcomCore.Log("ExtractDeltaContent: no '{' found in choices");
                return null;
            }

            // Find matching closing brace for first object
            int depth = 0;
            int objectEnd = -1;

            for (int i = objectStart; i < choicesJson.Length; i++)
            {
                char c = choicesJson[i];

                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        objectEnd = i;
                        break;
                    }
                }
            }

            if (objectEnd < 0)
            {
                CapcomCore.Log("ExtractDeltaContent: no matching '}' found");
                return null;
            }

            string choiceJson = choicesJson.Substring(objectStart, objectEnd - objectStart + 1);
            CapcomCore.Log($"ExtractDeltaContent choiceJson: {choiceJson}");

            // Extract delta object
            var deltaJson = JsonParser.ExtractObjectValue(choiceJson, "delta");
            CapcomCore.Log($"ExtractDeltaContent delta: {deltaJson ?? "(null)"}");
            if (string.IsNullOrEmpty(deltaJson))
                return null;

            // Extract content from delta
            var content = JsonParser.ExtractStringValue(deltaJson, "content");
            CapcomCore.Log($"ExtractDeltaContent content: '{content ?? "(null)"}'");
            return content;
        }

        /// <summary>
        /// Extract finish_reason from a streaming chunk.
        /// </summary>
        private string ExtractFinishReason(string json)
        {
            // Look for "finish_reason":"xxx" or "finish_reason":null
            var choicesJson = JsonParser.ExtractArrayValue(json, "choices");
            if (string.IsNullOrEmpty(choicesJson))
                return null;

            // Simple extraction - find the first choice object
            int objectStart = choicesJson.IndexOf('{');
            if (objectStart < 0)
                return null;

            int depth = 0;
            int objectEnd = -1;
            for (int i = objectStart; i < choicesJson.Length; i++)
            {
                char c = choicesJson[i];
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        objectEnd = i;
                        break;
                    }
                }
            }

            if (objectEnd < 0)
                return null;

            string choiceJson = choicesJson.Substring(objectStart, objectEnd - objectStart + 1);
            return JsonParser.ExtractStringValue(choiceJson, "finish_reason");
        }

        /// <summary>
        /// Extract and accumulate tool calls from a streaming delta.
        /// Tool calls come in multiple chunks that need to be assembled.
        /// </summary>
        private void ExtractAndAccumulateToolCalls(string json)
        {
            // Get choices array
            var choicesJson = JsonParser.ExtractArrayValue(json, "choices");
            if (string.IsNullOrEmpty(choicesJson))
                return;

            // Find first choice object
            int objectStart = choicesJson.IndexOf('{');
            if (objectStart < 0)
                return;

            int depth = 0;
            int objectEnd = -1;
            for (int i = objectStart; i < choicesJson.Length; i++)
            {
                char c = choicesJson[i];
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        objectEnd = i;
                        break;
                    }
                }
            }

            if (objectEnd < 0)
                return;

            string choiceJson = choicesJson.Substring(objectStart, objectEnd - objectStart + 1);

            // Get delta
            var deltaJson = JsonParser.ExtractObjectValue(choiceJson, "delta");
            if (string.IsNullOrEmpty(deltaJson))
                return;

            // Check for tool_calls array in delta
            var toolCallsJson = JsonParser.ExtractArrayValue(deltaJson, "tool_calls");
            if (string.IsNullOrEmpty(toolCallsJson))
                return;

            // Parse each tool call in the array
            // Each item has an "index" field that identifies which tool call it belongs to
            ParseToolCallDeltas(toolCallsJson);
        }

        /// <summary>
        /// Parse tool call deltas from the tool_calls array JSON.
        /// </summary>
        private void ParseToolCallDeltas(string toolCallsArrayJson)
        {
            // Find each object in the array
            int depth = 0;
            int objectStart = -1;
            bool inString = false;

            for (int i = 0; i < toolCallsArrayJson.Length; i++)
            {
                char c = toolCallsArrayJson[i];

                if (inString)
                {
                    if (c == '\\' && i + 1 < toolCallsArrayJson.Length)
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
                    if (depth == 0)
                        objectStart = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && objectStart >= 0)
                    {
                        var objectJson = toolCallsArrayJson.Substring(objectStart, i - objectStart + 1);
                        ProcessToolCallDelta(objectJson);
                        objectStart = -1;
                    }
                }
            }
        }

        /// <summary>
        /// Process a single tool call delta object.
        /// </summary>
        private void ProcessToolCallDelta(string toolCallDeltaJson)
        {
            // Extract index
            int index = JsonParser.ExtractIntValue(toolCallDeltaJson, "index");

            // Ensure we have an accumulator for this index
            while (_toolCallAccumulators.Count <= index)
            {
                _toolCallAccumulators.Add(new ToolCallAccumulator());
            }

            var accumulator = _toolCallAccumulators[index];

            // Extract id if present (first chunk only)
            var id = JsonParser.ExtractStringValue(toolCallDeltaJson, "id");
            if (!string.IsNullOrEmpty(id))
            {
                accumulator.Id = id;
            }

            // Extract type if present (first chunk only)
            var type = JsonParser.ExtractStringValue(toolCallDeltaJson, "type");
            if (!string.IsNullOrEmpty(type))
            {
                accumulator.Type = type;
            }

            // Extract function object
            var functionJson = JsonParser.ExtractObjectValue(toolCallDeltaJson, "function");
            if (!string.IsNullOrEmpty(functionJson))
            {
                // Extract name if present (first chunk only)
                var name = JsonParser.ExtractStringValue(functionJson, "name");
                if (!string.IsNullOrEmpty(name))
                {
                    accumulator.FunctionName = name;
                }

                // Extract arguments chunk (accumulates across chunks)
                var arguments = JsonParser.ExtractStringValue(functionJson, "arguments");
                if (arguments != null) // Can be empty string
                {
                    accumulator.AppendArguments(arguments);
                }
            }

            CapcomCore.Log($"Tool call delta: index={index}, id={accumulator.Id ?? "(none)"}, name={accumulator.FunctionName ?? "(none)"}, args_len={accumulator.ArgumentsLength}");
        }

        /// <summary>
        /// Called when all data has been received.
        /// </summary>
        protected override void CompleteContent()
        {
            // Process any remaining buffer content
            if (_buffer.Length > 0)
            {
                ProcessBuffer();
            }

            base.CompleteContent();
        }

        /// <summary>
        /// Get the downloaded data as text (required override).
        /// </summary>
        protected override string GetText()
        {
            return _completeResponse.ToString();
        }

        /// <summary>
        /// Get the downloaded data as bytes (required override).
        /// </summary>
        protected override byte[] GetData()
        {
            return Encoding.UTF8.GetBytes(_completeResponse.ToString());
        }
    }

    /// <summary>
    /// Helper class to accumulate tool call data from streaming chunks.
    /// Tool calls are streamed in pieces and need to be reassembled.
    /// </summary>
    internal class ToolCallAccumulator
    {
        private readonly StringBuilder _arguments = new StringBuilder();

        public string Id { get; set; }
        public string Type { get; set; } = "function";
        public string FunctionName { get; set; }

        public int ArgumentsLength => _arguments.Length;

        public void AppendArguments(string chunk)
        {
            _arguments.Append(chunk);
        }

        public ToolCall Build()
        {
            return new ToolCall
            {
                Id = Id,
                Type = Type,
                Function = new FunctionCall
                {
                    Name = FunctionName,
                    Arguments = _arguments.ToString()
                }
            };
        }
    }
}
