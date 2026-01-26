namespace KSPCapcom
{
    /// <summary>
    /// Identifies the sender/role of a chat message.
    /// Follows standard LLM conversation role conventions.
    /// </summary>
    public enum MessageRole
    {
        /// <summary>User-submitted message.</summary>
        User,

        /// <summary>Assistant (CAPCOM) response.</summary>
        Assistant,

        /// <summary>System message (status, errors, internal notes).</summary>
        System,

        /// <summary>Tool response message (result of a function call).</summary>
        Tool
    }
}
