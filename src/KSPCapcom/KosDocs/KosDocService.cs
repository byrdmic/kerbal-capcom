using System;
using System.Collections.Generic;
using System.Text;

namespace KSPCapcom.KosDocs
{
    /// <summary>
    /// Service for accessing kOS documentation.
    /// Provides lazy loading and convenient query methods for prompt enrichment.
    /// </summary>
    public class KosDocService
    {
        private static KosDocService _instance;

        /// <summary>
        /// Singleton instance of the documentation service.
        /// </summary>
        public static KosDocService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new KosDocService();
                }
                return _instance;
            }
        }

        private readonly KosDocLoader _loader;
        private bool _loadInitiated;

        /// <summary>
        /// Whether the documentation is loaded and ready for queries.
        /// </summary>
        public bool IsReady => _loader.IsReady;

        /// <summary>
        /// Whether loading is in progress.
        /// </summary>
        public bool IsLoading => _loader.IsLoading;

        /// <summary>
        /// The number of loaded entries.
        /// </summary>
        public int EntryCount => _loader.Index?.Count ?? 0;

        /// <summary>
        /// The content version of the loaded documentation.
        /// </summary>
        public string ContentVersion => _loader.Index?.ContentVersion ?? "";

        private KosDocService()
        {
            _loader = new KosDocLoader();
        }

        /// <summary>
        /// Initialize the service and begin loading documentation.
        /// Safe to call multiple times - will only load once.
        /// </summary>
        /// <param name="onComplete">Optional callback when loading completes.</param>
        public void Initialize(Action<bool> onComplete = null)
        {
            if (_loadInitiated)
            {
                if (onComplete != null)
                {
                    if (_loader.IsReady)
                    {
                        onComplete(true);
                    }
                    else if (_loader.LoadError != null)
                    {
                        onComplete(false);
                    }
                    // If still loading, caller won't get callback
                }
                return;
            }

            _loadInitiated = true;
            _loader.LoadAsync(onComplete);
        }

        /// <summary>
        /// Get a documentation entry by exact ID.
        /// </summary>
        public DocEntry GetById(string id)
        {
            if (!_loader.IsReady) return null;
            return _loader.Index.GetById(id);
        }

        /// <summary>
        /// Get a documentation entry by ID or alias.
        /// </summary>
        public DocEntry GetByIdOrAlias(string idOrAlias)
        {
            if (!_loader.IsReady) return null;
            return _loader.Index.GetByIdOrAlias(idOrAlias);
        }

        /// <summary>
        /// Get all suffixes for a structure.
        /// </summary>
        public IReadOnlyList<DocEntry> GetSuffixes(string structureName)
        {
            if (!_loader.IsReady) return Array.Empty<DocEntry>();
            return _loader.Index.GetByParent(structureName);
        }

        /// <summary>
        /// Get all entries with a specific tag.
        /// </summary>
        public IReadOnlyList<DocEntry> GetByTag(string tag)
        {
            if (!_loader.IsReady) return Array.Empty<DocEntry>();
            return _loader.Index.GetByTag(tag);
        }

        /// <summary>
        /// Search for entries matching a query.
        /// </summary>
        public IReadOnlyList<DocEntry> Search(string query, int maxResults = 10)
        {
            if (!_loader.IsReady) return Array.Empty<DocEntry>();
            return _loader.Index.Search(query, maxResults);
        }

        /// <summary>
        /// Get relevant documentation for a user query.
        /// Attempts to find the most relevant entries based on keywords in the query.
        /// </summary>
        /// <param name="userQuery">The user's question or request.</param>
        /// <param name="maxEntries">Maximum entries to return.</param>
        /// <returns>Relevant documentation entries.</returns>
        public IReadOnlyList<DocEntry> GetRelevantDocs(string userQuery, int maxEntries = 5)
        {
            if (!_loader.IsReady || string.IsNullOrEmpty(userQuery))
            {
                return Array.Empty<DocEntry>();
            }

            var results = new List<DocEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var index = _loader.Index;

            // Extract potential keywords from the query
            var keywords = ExtractKeywords(userQuery);

            foreach (var keyword in keywords)
            {
                if (results.Count >= maxEntries) break;

                // Try exact ID match first
                var entry = index.GetByIdOrAlias(keyword);
                if (entry != null && !seen.Contains(entry.Id))
                {
                    results.Add(entry);
                    seen.Add(entry.Id);
                    continue;
                }

                // Try structure:suffix pattern
                if (keyword.Contains(":"))
                {
                    entry = index.GetById(keyword);
                    if (entry != null && !seen.Contains(entry.Id))
                    {
                        results.Add(entry);
                        seen.Add(entry.Id);
                        continue;
                    }
                }

                // Search for partial matches
                var searchResults = index.Search(keyword, 3);
                foreach (var sr in searchResults)
                {
                    if (results.Count >= maxEntries) break;
                    if (!seen.Contains(sr.Id))
                    {
                        results.Add(sr);
                        seen.Add(sr.Id);
                    }
                }
            }

            // If we found structure references, also include key suffixes
            var structuresToExpand = new List<string>();
            foreach (var entry in results)
            {
                if (entry.Type == DocEntryType.Structure && !string.IsNullOrEmpty(entry.Id))
                {
                    structuresToExpand.Add(entry.Id);
                }
            }

            foreach (var structName in structuresToExpand)
            {
                if (results.Count >= maxEntries) break;

                var suffixes = index.GetByParent(structName);
                int added = 0;
                foreach (var suffix in suffixes)
                {
                    if (added >= 3) break; // Limit suffixes per structure
                    if (results.Count >= maxEntries) break;
                    if (!seen.Contains(suffix.Id))
                    {
                        results.Add(suffix);
                        seen.Add(suffix.Id);
                        added++;
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Format relevant documentation for inclusion in a prompt.
        /// </summary>
        /// <param name="userQuery">The user's question.</param>
        /// <param name="maxEntries">Maximum entries to include.</param>
        /// <returns>Formatted documentation string, or empty if none relevant.</returns>
        public string FormatRelevantDocsForPrompt(string userQuery, int maxEntries = 5)
        {
            var docs = GetRelevantDocs(userQuery, maxEntries);
            if (docs.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine("RELEVANT kOS DOCUMENTATION:");
            sb.AppendLine();

            foreach (var doc in docs)
            {
                sb.AppendLine(doc.ToPromptFormat());
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Extract potential kOS keywords from a user query.
        /// </summary>
        private List<string> ExtractKeywords(string query)
        {
            var keywords = new List<string>();
            var upperQuery = query.ToUpperInvariant();

            // Common kOS terms to look for
            var commonTerms = new[]
            {
                "VESSEL", "SHIP", "ALTITUDE", "APOAPSIS", "PERIAPSIS",
                "STEERING", "THROTTLE", "LOCK", "UNLOCK", "STAGE",
                "PROGRADE", "RETROGRADE", "NORMAL", "RADIAL",
                "HEADING", "WAIT", "WHEN", "SET", "PRINT",
                "ORBIT", "BODY", "KERBIN", "MUN", "MINMUS",
                "VECTOR", "DIRECTION", "ETA", "TIME",
                "CREW", "PARTS", "RESOURCES", "FUEL",
                "ABS", "ROUND", "SQRT", "SIN", "COS"
            };

            foreach (var term in commonTerms)
            {
                if (upperQuery.Contains(term))
                {
                    keywords.Add(term);
                }
            }

            // Also extract words that look like kOS identifiers (ALL_CAPS or CamelCase)
            var words = query.Split(new[] { ' ', ',', '.', '?', '!', '(', ')', '[', ']', '\n', '\r', '\t' },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (var word in words)
            {
                // Check for UPPER_CASE or CamelCase patterns
                if (word.Length >= 3)
                {
                    bool isAllUpper = true;
                    bool hasMixedCase = false;
                    bool hasColon = word.Contains(":");

                    foreach (char c in word)
                    {
                        if (char.IsLetter(c))
                        {
                            if (char.IsLower(c)) isAllUpper = false;
                            if (char.IsUpper(c) && word.IndexOf(c) > 0) hasMixedCase = true;
                        }
                    }

                    if ((isAllUpper || hasMixedCase || hasColon) && !keywords.Contains(word.ToUpperInvariant()))
                    {
                        keywords.Add(word);
                    }
                }
            }

            return keywords;
        }
    }
}
