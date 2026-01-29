using System;
using System.Collections.Generic;
using KSPCapcom.Responders;

namespace KSPCapcom
{
    /// <summary>
    /// Request object for queued messages.
    /// </summary>
    public class MessageRequest
    {
        public string UserMessage { get; }
        public IReadOnlyList<ChatMessage> History { get; }
        public Action<ResponderResult> OnComplete { get; }
        public DateTime QueuedAt { get; }

        /// <summary>
        /// Reference to the user's ChatMessage in the UI for state updates.
        /// </summary>
        public ChatMessage UserChatMessage { get; }

        public MessageRequest(
            string userMessage,
            IReadOnlyList<ChatMessage> history,
            Action<ResponderResult> onComplete,
            ChatMessage userChatMessage = null)
        {
            UserMessage = userMessage;
            History = history;
            OnComplete = onComplete;
            QueuedAt = DateTime.UtcNow;
            UserChatMessage = userChatMessage;
        }
    }

    /// <summary>
    /// Queue for managing message requests with bounded size.
    /// </summary>
    public class MessageQueue
    {
        private readonly Queue<MessageRequest> _queue = new Queue<MessageRequest>();
        private readonly int _maxQueueSize;

        /// <summary>
        /// Default maximum queue size.
        /// </summary>
        public const int DEFAULT_MAX_QUEUE_SIZE = 5;

        public MessageQueue(int maxQueueSize = DEFAULT_MAX_QUEUE_SIZE)
        {
            _maxQueueSize = maxQueueSize;
        }

        public int Count => _queue.Count;
        public bool HasPending => _queue.Count > 0;

        /// <summary>
        /// Enqueue a message request. Returns true if a message was dropped due to overflow.
        /// </summary>
        public bool Enqueue(MessageRequest request)
        {
            bool wasDropped = false;

            // Drop oldest if at capacity
            while (_queue.Count >= _maxQueueSize)
            {
                var dropped = _queue.Dequeue();
                wasDropped = true;

                // Mark the user's chat message as dropped
                dropped.UserChatMessage?.MarkDropped();

                CapcomCore.LogWarning("Message queue full, dropping oldest request");
                // Notify dropped request
                dropped.OnComplete?.Invoke(
                    ResponderResult.Fail("Request dropped - queue overflow"));
            }

            _queue.Enqueue(request);
            return wasDropped;
        }

        public MessageRequest Dequeue()
        {
            return _queue.Count > 0 ? _queue.Dequeue() : null;
        }

        public MessageRequest Peek()
        {
            return _queue.Count > 0 ? _queue.Peek() : null;
        }

        public void Clear()
        {
            while (_queue.Count > 0)
            {
                var request = _queue.Dequeue();
                // Mark the user message as no longer queued
                request.UserChatMessage?.MarkDequeued();
                request.OnComplete?.Invoke(
                    ResponderResult.Fail("Request cancelled - queue cleared"));
            }
        }
    }
}
