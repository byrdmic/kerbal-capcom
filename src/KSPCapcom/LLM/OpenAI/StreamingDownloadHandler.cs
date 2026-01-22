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
        public StreamingDownloadHandler(Action<string> onChunk)
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

            // Remove processed content from buffer
            if (lastProcessed > 0)
            {
                _buffer.Remove(0, lastProcessed);
            }
        }

        /// <summary>
        /// Process a single SSE event.
        /// </summary>
        private void ProcessEvent(string eventText)
        {
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

                    // Notify callback
                    _onChunk?.Invoke(deltaContent);
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
            // Find the choices array
            var choicesJson = JsonParser.ExtractArrayValue(json, "choices");
            if (string.IsNullOrEmpty(choicesJson))
                return null;

            // Find the first choice object
            int objectStart = choicesJson.IndexOf('{');
            if (objectStart < 0)
                return null;

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
                return null;

            string choiceJson = choicesJson.Substring(objectStart, objectEnd - objectStart + 1);

            // Extract delta object
            var deltaJson = JsonParser.ExtractObjectValue(choiceJson, "delta");
            if (string.IsNullOrEmpty(deltaJson))
                return null;

            // Extract content from delta
            return JsonParser.ExtractStringValue(deltaJson, "content");
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
