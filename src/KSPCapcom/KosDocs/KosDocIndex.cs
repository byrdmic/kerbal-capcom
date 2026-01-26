using System;
using System.Collections.Generic;

namespace KSPCapcom.KosDocs
{
    /// <summary>
    /// In-memory index of kOS documentation for fast retrieval.
    /// Provides O(1) lookups by ID, parent structure, and tags.
    /// </summary>
    public class KosDocIndex
    {
        /// <summary>
        /// Schema version for the index format.
        /// </summary>
        public string SchemaVersion { get; set; }

        /// <summary>
        /// kOS documentation version covered.
        /// </summary>
        public string ContentVersion { get; set; }

        /// <summary>
        /// Minimum compatible kOS version.
        /// </summary>
        public string KosMinVersion { get; set; }

        /// <summary>
        /// When the index was generated.
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// Source URL for the documentation.
        /// </summary>
        public string SourceUrl { get; set; }

        // Primary storage
        private readonly List<DocEntry> _entries = new List<DocEntry>();

        // Indices for fast lookup
        private readonly Dictionary<string, DocEntry> _byId =
            new Dictionary<string, DocEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<DocEntry>> _byParent =
            new Dictionary<string, List<DocEntry>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<DocEntry>> _byTag =
            new Dictionary<string, List<DocEntry>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DocEntry> _byAlias =
            new Dictionary<string, DocEntry>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Total number of entries in the index.
        /// </summary>
        public int Count => _entries.Count;

        /// <summary>
        /// Whether the index has been loaded with entries.
        /// </summary>
        public bool IsLoaded => _entries.Count > 0;

        /// <summary>
        /// All entries in the index.
        /// </summary>
        public IReadOnlyList<DocEntry> Entries => _entries;

        /// <summary>
        /// Add an entry to the index and update all indices.
        /// </summary>
        public void AddEntry(DocEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Id))
            {
                return;
            }

            _entries.Add(entry);

            // Index by ID
            _byId[entry.Id] = entry;

            // Index by parent structure
            if (!string.IsNullOrEmpty(entry.ParentStructure))
            {
                if (!_byParent.TryGetValue(entry.ParentStructure, out var parentList))
                {
                    parentList = new List<DocEntry>();
                    _byParent[entry.ParentStructure] = parentList;
                }
                parentList.Add(entry);
            }

            // Index by tags
            if (entry.Tags != null)
            {
                foreach (var tag in entry.Tags)
                {
                    if (string.IsNullOrEmpty(tag)) continue;

                    if (!_byTag.TryGetValue(tag, out var tagList))
                    {
                        tagList = new List<DocEntry>();
                        _byTag[tag] = tagList;
                    }
                    tagList.Add(entry);
                }
            }

            // Index by aliases
            if (entry.Aliases != null)
            {
                foreach (var alias in entry.Aliases)
                {
                    if (!string.IsNullOrEmpty(alias))
                    {
                        _byAlias[alias] = entry;
                    }
                }
            }
        }

        /// <summary>
        /// Get an entry by its exact ID. O(1).
        /// </summary>
        /// <param name="id">Entry ID (case-insensitive).</param>
        /// <returns>The entry, or null if not found.</returns>
        public DocEntry GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            _byId.TryGetValue(id, out var entry);
            return entry;
        }

        /// <summary>
        /// Get an entry by ID or alias. O(1).
        /// </summary>
        /// <param name="idOrAlias">Entry ID or alias (case-insensitive).</param>
        /// <returns>The entry, or null if not found.</returns>
        public DocEntry GetByIdOrAlias(string idOrAlias)
        {
            if (string.IsNullOrEmpty(idOrAlias)) return null;

            // Try ID first
            if (_byId.TryGetValue(idOrAlias, out var entry))
            {
                return entry;
            }

            // Try alias
            _byAlias.TryGetValue(idOrAlias, out entry);
            return entry;
        }

        /// <summary>
        /// Get all suffixes of a parent structure. O(1).
        /// </summary>
        /// <param name="parentStructure">Parent structure name (case-insensitive).</param>
        /// <returns>List of suffix entries, or empty list if none found.</returns>
        public IReadOnlyList<DocEntry> GetByParent(string parentStructure)
        {
            if (string.IsNullOrEmpty(parentStructure))
            {
                return Array.Empty<DocEntry>();
            }

            if (_byParent.TryGetValue(parentStructure, out var list))
            {
                return list;
            }

            return Array.Empty<DocEntry>();
        }

        /// <summary>
        /// Get all entries with a specific tag. O(1).
        /// </summary>
        /// <param name="tag">Tag name (case-insensitive).</param>
        /// <returns>List of entries with that tag, or empty list if none found.</returns>
        public IReadOnlyList<DocEntry> GetByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return Array.Empty<DocEntry>();
            }

            if (_byTag.TryGetValue(tag, out var list))
            {
                return list;
            }

            return Array.Empty<DocEntry>();
        }

        /// <summary>
        /// Get all available tags.
        /// </summary>
        public IEnumerable<string> GetAllTags()
        {
            return _byTag.Keys;
        }

        /// <summary>
        /// Get all parent structures.
        /// </summary>
        public IEnumerable<string> GetAllParents()
        {
            return _byParent.Keys;
        }

        /// <summary>
        /// Search entries by text in ID, name, or description. O(n).
        /// </summary>
        /// <param name="query">Search text (case-insensitive).</param>
        /// <param name="maxResults">Maximum results to return.</param>
        /// <returns>Matching entries, ordered by relevance (ID match > name match > description match).</returns>
        /// <remarks>
        /// Scoring algorithm:
        /// - 100: Exact ID match
        /// - 90: ID starts with query (prefix match)
        /// - 80: ID contains query
        /// - 75: Name starts with query (prefix match)
        /// - 70: Exact name match
        /// - 60: Name contains query
        /// - 50: Alias contains query
        /// - 40: Tag exact match
        /// - 30: Description contains query
        /// </remarks>
        public IReadOnlyList<DocEntry> Search(string query, int maxResults = 10)
        {
            if (string.IsNullOrEmpty(query) || maxResults <= 0)
            {
                return Array.Empty<DocEntry>();
            }

            var results = new List<(DocEntry entry, int score)>();
            var lowerQuery = query.ToLowerInvariant();

            foreach (var entry in _entries)
            {
                int score = 0;
                var lowerId = entry.Id?.ToLowerInvariant();
                var lowerName = entry.Name?.ToLowerInvariant();

                // Exact ID match (highest priority)
                if (lowerId != null && lowerId == lowerQuery)
                {
                    score = 100;
                }
                // ID starts with query (prefix match)
                else if (lowerId != null && lowerId.StartsWith(lowerQuery))
                {
                    score = 90;
                }
                // ID contains query
                else if (lowerId != null && lowerId.Contains(lowerQuery))
                {
                    score = 80;
                }
                // Name starts with query (prefix match)
                else if (lowerName != null && lowerName.StartsWith(lowerQuery))
                {
                    score = 75;
                }
                // Exact name match
                else if (lowerName != null && lowerName == lowerQuery)
                {
                    score = 70;
                }
                // Name contains query
                else if (lowerName != null && lowerName.Contains(lowerQuery))
                {
                    score = 60;
                }
                // Alias contains query
                else if (entry.Aliases != null)
                {
                    foreach (var alias in entry.Aliases)
                    {
                        if (alias != null && alias.ToLowerInvariant().Contains(lowerQuery))
                        {
                            score = 50;
                            break;
                        }
                    }
                }

                // Tag exact match (check independently if no match yet)
                if (score == 0 && entry.Tags != null)
                {
                    foreach (var tag in entry.Tags)
                    {
                        if (tag != null && tag.Equals(query, StringComparison.OrdinalIgnoreCase))
                        {
                            score = 40;
                            break;
                        }
                    }
                }

                // Description contains query (lowest priority)
                if (score == 0 && entry.Description != null &&
                    entry.Description.ToLowerInvariant().Contains(lowerQuery))
                {
                    score = 30;
                }

                if (score > 0)
                {
                    results.Add((entry, score));
                }
            }

            // Sort by score descending
            results.Sort((a, b) => b.score.CompareTo(a.score));

            // Take top results
            var output = new List<DocEntry>();
            for (int i = 0; i < Math.Min(results.Count, maxResults); i++)
            {
                output.Add(results[i].entry);
            }

            return output;
        }

        /// <summary>
        /// Search for entries matching multiple criteria.
        /// </summary>
        /// <param name="query">Text to search for.</param>
        /// <param name="type">Filter by entry type (null for any).</param>
        /// <param name="tag">Filter by tag (null for any).</param>
        /// <param name="maxResults">Maximum results.</param>
        public IReadOnlyList<DocEntry> SearchWithFilters(
            string query = null,
            DocEntryType? type = null,
            string tag = null,
            int maxResults = 10)
        {
            var candidates = _entries;

            // Start with tag-filtered set if specified
            if (!string.IsNullOrEmpty(tag))
            {
                var taggedEntries = GetByTag(tag);
                if (taggedEntries.Count == 0)
                {
                    return Array.Empty<DocEntry>();
                }
                candidates = new List<DocEntry>(taggedEntries);
            }

            var results = new List<DocEntry>();

            foreach (var entry in candidates)
            {
                // Type filter
                if (type.HasValue && entry.Type != type.Value)
                {
                    continue;
                }

                // Text filter
                if (!string.IsNullOrEmpty(query))
                {
                    var lowerQuery = query.ToLowerInvariant();
                    bool matches =
                        (entry.Id != null && entry.Id.ToLowerInvariant().Contains(lowerQuery)) ||
                        (entry.Name != null && entry.Name.ToLowerInvariant().Contains(lowerQuery)) ||
                        (entry.Description != null && entry.Description.ToLowerInvariant().Contains(lowerQuery));

                    if (!matches)
                    {
                        continue;
                    }
                }

                results.Add(entry);

                if (results.Count >= maxResults)
                {
                    break;
                }
            }

            return results;
        }

        /// <summary>
        /// Clear all entries and indices.
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
            _byId.Clear();
            _byParent.Clear();
            _byTag.Clear();
            _byAlias.Clear();
        }
    }
}
