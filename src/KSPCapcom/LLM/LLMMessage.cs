using System.Collections.Generic;
using KSPCapcom.LLM.OpenAI;

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

        /// <summary>
        /// Tool calls requested by the assistant (for assistant messages).
        /// </summary>
        public IReadOnlyList<ToolCall> ToolCalls { get; }

        /// <summary>
        /// The ID of the tool call this message is responding to (for tool messages).
        /// </summary>
        public string ToolCallId { get; }

        /// <summary>
        /// The name of the tool (for tool response messages).
        /// </summary>
        public string Name { get; }

        public LLMMessage(MessageRole role, string content)
            : this(role, content, null, null, null)
        {
        }

        public LLMMessage(
            MessageRole role,
            string content,
            IReadOnlyList<ToolCall> toolCalls = null,
            string toolCallId = null,
            string name = null)
        {
            Role = role;
            Content = content ?? string.Empty;
            ToolCalls = toolCalls;
            ToolCallId = toolCallId;
            Name = name;
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
        /// Create an assistant message with tool calls.
        /// </summary>
        public static LLMMessage AssistantWithToolCalls(string content, IReadOnlyList<ToolCall> toolCalls) =>
            new LLMMessage(MessageRole.Assistant, content, toolCalls: toolCalls);

        /// <summary>
        /// Create a system message.
        /// </summary>
        public static LLMMessage System(string content) =>
            new LLMMessage(MessageRole.System, content);

        /// <summary>
        /// Create a tool response message.
        /// </summary>
        public static LLMMessage ToolResponse(string toolCallId, string content, string name = null) =>
            new LLMMessage(MessageRole.Tool, content, toolCallId: toolCallId, name: name);

        public override string ToString() =>
            $"[{Role}] {(Content.Length > 50 ? Content.Substring(0, 47) + "..." : Content)}";
    }
}
