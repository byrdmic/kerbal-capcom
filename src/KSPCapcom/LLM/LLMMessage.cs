namespace KSPCapcom.LLM
{
    /// <summary>
    /// Simple POCO representing a message in an LLM conversation.
    /// Separate from ChatMessage to avoid UI concerns (timestamps, pending state).
    /// </summary>
    public class LLMMessage
    {
        /// <summary>
        /// The role of the message sender.
        /// </summary>
        public MessageRole Role { get; }

        /// <summary>
        /// The text content of the message.
        /// </summary>
        public string Content { get; }

        public LLMMessage(MessageRole role, string content)
        {
            Role = role;
            Content = content ?? string.Empty;
        }

        /// <summary>
        /// Create a user message.
        /// </summary>
        public static LLMMessage User(string content) =>
            new LLMMessage(MessageRole.User, content);

        /// <summary>
        /// Create an assistant message.
        /// </summary>
        public static LLMMessage Assistant(string content) =>
            new LLMMessage(MessageRole.Assistant, content);

        /// <summary>
        /// Create a system message.
        /// </summary>
        public static LLMMessage System(string content) =>
            new LLMMessage(MessageRole.System, content);

        public override string ToString() =>
            $"[{Role}] {(Content.Length > 50 ? Content.Substring(0, 47) + "..." : Content)}";
    }
}
