namespace KSPCapcom
{
    /// <summary>
    /// Mock implementation of CapcomCore for unit testing.
    /// Provides the static LogWarning method that PromptBuilder calls,
    /// avoiding the Unity dependency in tests.
    /// </summary>
    public static class CapcomCore
    {
        /// <summary>
        /// Mock LogWarning - does nothing in tests.
        /// In production, this would call Unity's Debug.LogWarning.
        /// </summary>
        public static void LogWarning(string message)
        {
            // No-op for tests. Could capture messages if needed for assertions.
        }
    }
}
