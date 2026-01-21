using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace KSPCapcom
{
    /// <summary>
    /// Dispatcher for executing actions on the Unity main thread.
    /// Thread-safe enqueue from any thread; dequeue only on main thread.
    /// </summary>
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher _instance;
        private static readonly object _lock = new object();

        private readonly ConcurrentQueue<Action> _pendingActions = new ConcurrentQueue<Action>();

        /// <summary>
        /// Maximum actions to process per frame to avoid hitches.
        /// </summary>
        private const int MAX_ACTIONS_PER_FRAME = 10;

        /// <summary>
        /// Gets or creates the singleton instance.
        /// </summary>
        public static MainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            var go = new GameObject("KSPCapcom_MainThreadDispatcher");
                            _instance = go.AddComponent<MainThreadDispatcher>();
                            DontDestroyOnLoad(go);
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Enqueue an action to be executed on the main thread.
        /// Thread-safe - can be called from any thread.
        /// </summary>
        /// <param name="action">The action to execute on the main thread.</param>
        public void Enqueue(Action action)
        {
            if (action == null) return;
            _pendingActions.Enqueue(action);
        }

        /// <summary>
        /// Returns the number of pending actions in the queue.
        /// </summary>
        public int PendingCount => _pendingActions.Count;

        private void Update()
        {
            ProcessQueue();
        }

        private void ProcessQueue()
        {
            int processed = 0;
            while (processed < MAX_ACTIONS_PER_FRAME &&
                   _pendingActions.TryDequeue(out Action action))
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    CapcomCore.LogError($"MainThreadDispatcher action error: {ex.Message}");
                }
                processed++;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
