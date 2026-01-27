using System.Collections.Generic;
using KSPCapcom.KosDocs;

namespace KSPCapcom.Validation
{
    /// <summary>
    /// Tracks DocEntry objects retrieved during tool calls in a conversation.
    /// Used for grounded mode validation to verify LLM-generated identifiers.
    /// </summary>
    public class DocEntryTracker
    {
        private readonly List<DocEntry> _entries;
        private readonly HashSet<string> _seenIds;

        /// <summary>
        /// Create a new empty tracker.
        /// </summary>
        public DocEntryTracker()
        {
            _entries = new List<DocEntry>();
            _seenIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Add entries from a tool call result.
        /// Duplicates (by ID) are ignored.
        /// </summary>
        /// <param name="entries">The entries to add.</param>
        public void Add(IReadOnlyList<DocEntry> entries)
        {
            if (entries == null) return;

            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Id)) continue;

                if (_seenIds.Add(entry.Id))
                {
                    _entries.Add(entry);
                }
            }
        }

        /// <summary>
        /// Add a single entry.
        /// </summary>
        /// <param name="entry">The entry to add.</param>
        public void Add(DocEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Id)) return;

            if (_seenIds.Add(entry.Id))
            {
                _entries.Add(entry);
            }
        }

        /// <summary>
        /// Get all tracked entries.
        /// </summary>
        public IReadOnlyList<DocEntry> GetAll()
        {
            return _entries;
        }

        /// <summary>
        /// Check if a specific entry ID has been tracked.
        /// </summary>
        /// <param name="id">The entry ID to check.</param>
        /// <returns>True if an entry with this ID has been added.</returns>
        public bool Contains(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return _seenIds.Contains(id);
        }

        /// <summary>
        /// Number of tracked entries.
        /// </summary>
        public int Count => _entries.Count;

        /// <summary>
        /// Whether any entries have been tracked.
        /// </summary>
        public bool HasEntries => _entries.Count > 0;

        /// <summary>
        /// Clear all tracked entries.
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
            _seenIds.Clear();
        }
    }
}
