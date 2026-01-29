using System;
using KSPCapcom.Parsing;

namespace KSPCapcom
{
    /// <summary>
    /// Represents a single chat message.
    /// </summary>
    public class ChatMessage
    {
        public string Text { get; private set; }
        public MessageRole Role { get; }
        public DateTime Timestamp { get; }

        /// <summary>
        /// Whether this message is still being generated (pending/streaming).
        /// </summary>
        public bool IsPending { get; private set; }

        /// <summary>
        /// Whether this message is currently queued awaiting processing.
        /// </summary>
        public bool IsQueued { get; private set; }

        /// <summary>
        /// Whether this message was dropped due to queue overflow.
        /// </summary>
        public bool WasDropped { get; private set; }

        /// <summary>
        /// Whether this message is an error message with expandable details.
        /// </summary>
        public bool IsErrorMessage { get; set; }

        /// <summary>
        /// Unique identifier for error messages (used for detail expansion tracking).
        /// </summary>
        public int ErrorId { get; set; }

        /// <summary>
        /// Parsed content with code blocks and prose segments (set after completion).
        /// </summary>
        public ParsedMessageContent ParsedContent { get; private set; }

        /// <summary>
        /// Whether this message contains code blocks.
        /// </summary>
        public bool HasCodeBlocks => ParsedContent?.HasCodeBlocks ?? false;

        /// <summary>
        /// Convenience property for backward compatibility.
        /// </summary>
        public bool IsFromUser => Role == MessageRole.User;

        public ChatMessage(string text, MessageRole role, bool isPending = false)
        {
            Text = text;
            Role = role;
            Timestamp = DateTime.Now;
            IsPending = isPending;
        }

        /// <summary>
        /// Update the message text (for streaming or completing pending messages).
        /// </summary>
        public void UpdateText(string newText)
        {
            Text = newText;
        }

        /// <summary>
        /// Mark the message as complete (no longer pending).
        /// </summary>
        public void Complete(string finalText = null)
        {
            if (finalText != null)
            {
                Text = finalText;
            }
            IsPending = false;
        }

        /// <summary>
        /// Mark the message as no longer queued (now being processed).
        /// </summary>
        public void MarkDequeued() => IsQueued = false;

        /// <summary>
        /// Mark the message as dropped due to queue overflow.
        /// </summary>
        public void MarkDropped()
        {
            IsQueued = false;
            WasDropped = true;
        }

        /// <summary>
        /// Set the parsed content for this message (called after completion).
        /// </summary>
        public void SetParsedContent(ParsedMessageContent content)
        {
            ParsedContent = content;
        }

        public static ChatMessage FromUser(string text, bool isQueued = false) =>
            new ChatMessage(text, MessageRole.User) { IsQueued = isQueued };

        public static ChatMessage FromAssistant(string text) =>
            new ChatMessage(text, MessageRole.Assistant);

        public static ChatMessage FromAssistantPending(string placeholderText = "CAPCOM is thinking...") =>
            new ChatMessage(placeholderText, MessageRole.Assistant, isPending: true);

        public static ChatMessage FromSystem(string text) =>
            new ChatMessage(text, MessageRole.System);

        public static ChatMessage FromError(string text, int errorId) =>
            new ChatMessage(text, MessageRole.System) { IsErrorMessage = true, ErrorId = errorId };
    }
}
