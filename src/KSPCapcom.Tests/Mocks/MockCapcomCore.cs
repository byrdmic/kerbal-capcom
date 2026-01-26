using System;

namespace KSPCapcom
{
    /// <summary>
    /// Mock implementation of CapcomCore for unit testing.
    /// Provides the static logging methods that various classes call,
    /// avoiding the Unity dependency in tests.
    /// </summary>
    public static class CapcomCore
    {
        /// <summary>
        /// Mock Log - does nothing in tests.
        /// In production, this would call Unity's Debug.Log.
        /// </summary>
        public static void Log(string message)
        {
            // No-op for tests. Could capture messages if needed for assertions.
        }

        /// <summary>
        /// Mock LogWarning - does nothing in tests.
        /// In production, this would call Unity's Debug.LogWarning.
        /// </summary>
        public static void LogWarning(string message)
        {
            // No-op for tests. Could capture messages if needed for assertions.
        }

        /// <summary>
        /// Mock LogError - does nothing in tests.
        /// In production, this would call Unity's Debug.LogError.
        /// </summary>
        public static void LogError(string message)
        {
            // No-op for tests. Could capture messages if needed for assertions.
        }
    }

    /// <summary>
    /// Mock MainThreadDispatcher for unit testing.
    /// Executes actions immediately since tests run single-threaded.
    /// </summary>
    public class MainThreadDispatcher
    {
        private static MainThreadDispatcher _instance;

        public static MainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new MainThreadDispatcher();
                }
                return _instance;
            }
        }

        /// <summary>
        /// In tests, execute the action immediately.
        /// </summary>
        public void Enqueue(Action action)
        {
            action?.Invoke();
        }
    }
}
