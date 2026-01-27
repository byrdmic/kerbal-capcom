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

        /// <summary>
        /// Find a DocEntry that matches the given identifier.
        /// Tries exact match on ID first, then partial matches.
        /// </summary>
        /// <param name="identifier">The identifier to find (e.g., "SHIP:VELOCITY").</param>
        /// <returns>The matching DocEntry, or null if not found.</returns>
        public DocEntry FindByIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return null;

            var normalizedId = identifier.ToUpperInvariant();

            // Try exact match on ID first
            foreach (var entry in _entries)
            {
                if (string.Equals(entry.Id, normalizedId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            // Try match on Name (for suffixes like VELOCITY -> VESSEL:VELOCITY)
            foreach (var entry in _entries)
            {
                if (string.Equals(entry.Name, normalizedId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            // Try partial match (identifier ends with :Name)
            foreach (var entry in _entries)
            {
                if (!string.IsNullOrEmpty(entry.Name))
                {
                    var suffix = ":" + entry.Name.ToUpperInvariant();
                    if (normalizedId.EndsWith(suffix))
                    {
                        return entry;
                    }
                }
            }

            // Try match where entry ID ends with the identifier
            foreach (var entry in _entries)
            {
                if (!string.IsNullOrEmpty(entry.Id))
                {
                    var entryIdUpper = entry.Id.ToUpperInvariant();
                    if (entryIdUpper.EndsWith(":" + normalizedId) || entryIdUpper == normalizedId)
                    {
                        return entry;
                    }
                }
            }

            return null;
        }
    }
}
