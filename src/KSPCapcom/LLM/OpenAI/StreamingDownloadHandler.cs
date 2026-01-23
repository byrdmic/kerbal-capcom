using System;
using System.Text;
using UnityEngine.Networking;

namespace KSPCapcom.LLM.OpenAI
{
    /// <summary>
    /// Custom download handler for processing OpenAI's Server-Sent Events (SSE) streaming format.
    /// Parses "data: {...}\n\n" chunks incrementally as they arrive.
    /// </summary>
    public class StreamingDownloadHandler : DownloadHandlerScript
    {
        // Buffer size for receiving streaming data from Unity
        private const int BUFFER_SIZE = 4096;

        private readonly Action<string> _onChunk;
        private readonly StringBuilder _buffer;
        private readonly StringBuilder _completeResponse;
        private byte[] _partialBytes;
        private bool _isComplete;

        /// <summary>
        /// Get the complete accumulated response text.
        /// </summary>
        public string CompleteResponse => _completeResponse.ToString();

        /// <summary>
        /// Whether the stream has completed (received [DONE] marker).
        /// </summary>
        public bool IsComplete => _isComplete;

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
            _isComplete = false;
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
                // Extract delta content from: {"choices":[{"delta":{"content":"text"},...}]}
                var deltaContent = ExtractDeltaContent(dataContent);

                if (!string.IsNullOrEmpty(deltaContent))
                {
                    // Accumulate complete response
                    _completeResponse.Append(deltaContent);

                    // Notify callback with accumulated text
                    _onChunk?.Invoke(_completeResponse.ToString());
                }

                // Check for finish_reason: "length" with no content - indicates token limit hit
                if (dataContent.Contains("\"finish_reason\":\"length\"") && _completeResponse.Length == 0)
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
}
