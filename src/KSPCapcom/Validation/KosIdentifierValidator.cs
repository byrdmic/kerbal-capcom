using System;
using System.Collections.Generic;
using KSPCapcom.KosDocs;

namespace KSPCapcom.Validation
{
    /// <summary>
    /// Validates extracted kOS identifiers against documentation.
    /// Used in Grounded Mode to detect hallucinated/invented identifiers.
    /// </summary>
    public class KosIdentifierValidator
    {
        /// <summary>
        /// Maximum number of "did you mean" suggestions to provide.
        /// </summary>
        private const int MaxSuggestions = 3;

        private readonly HashSet<string> _validIdentifiers;
        private readonly Dictionary<string, DocEntry> _entryById;
        private readonly KosDocIndex _searchIndex;

        /// <summary>
        /// Create a validator with the given documentation entries.
        /// </summary>
        /// <param name="docEntries">Documentation entries retrieved during the conversation.</param>
        /// <param name="searchIndex">Optional index for fuzzy search suggestions.</param>
        public KosIdentifierValidator(IReadOnlyList<DocEntry> docEntries, KosDocIndex searchIndex = null)
        {
            _validIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _entryById = new Dictionary<string, DocEntry>(StringComparer.OrdinalIgnoreCase);
            _searchIndex = searchIndex;

            if (docEntries != null)
            {
                foreach (var entry in docEntries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.Id))
                        continue;

                    // Add the entry ID
                    _validIdentifiers.Add(entry.Id);
                    _entryById[entry.Id] = entry;

                    // Add entry name if different from ID
                    if (!string.IsNullOrEmpty(entry.Name) &&
                        !entry.Name.Equals(entry.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        _validIdentifiers.Add(entry.Name);
                        if (!_entryById.ContainsKey(entry.Name))
                        {
                            _entryById[entry.Name] = entry;
                        }
                    }

                    // Add aliases
                    if (entry.Aliases != null)
                    {
                        foreach (var alias in entry.Aliases)
                        {
                            if (!string.IsNullOrEmpty(alias))
                            {
                                _validIdentifiers.Add(alias);
                                if (!_entryById.ContainsKey(alias))
                                {
                                    _entryById[alias] = entry;
                                }
                            }
                        }
                    }

                    // Add parent structure name (for SHIP:VELOCITY, add SHIP)
                    if (!string.IsNullOrEmpty(entry.ParentStructure))
                    {
                        _validIdentifiers.Add(entry.ParentStructure);
                    }

                    // For structure entries, also add the structure name alone
                    if (entry.Type == DocEntryType.Structure)
                    {
                        _validIdentifiers.Add(entry.Name);
                    }

                    // For suffix entries with PARENT:SUFFIX format, add both parts
                    if (entry.Id.Contains(":"))
                    {
                        var parts = entry.Id.Split(':');
                        foreach (var part in parts)
                        {
                            if (!string.IsNullOrEmpty(part))
                            {
                                _validIdentifiers.Add(part);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Validate a set of extracted identifiers.
        /// </summary>
        /// <param name="identifiers">The identifiers to validate.</param>
        /// <returns>Validation result with verified, unverified, and user-defined lists.</returns>
        public KosValidationResult Validate(KosIdentifierSet identifiers)
        {
            var result = new KosValidationResult();

            if (identifiers == null || identifiers.IsEmpty)
            {
                return result;
            }

            // If no documentation was loaded, return a warning
            if (_validIdentifiers.Count == 0)
            {
                result.SetWarning("No documentation was available for validation. All identifiers are unverified.");

                // Mark all non-user-defined as unverified
                foreach (var id in identifiers.GetApiIdentifiers())
                {
                    result.AddUnverified(new UnverifiedIdentifier(id.Text, id.Line, new List<string>()));
                }

                return result;
            }

            // Track which identifiers we've already processed (to avoid duplicates)
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add user-defined names
            foreach (var name in identifiers.UserDefinedNames)
            {
                result.AddUserDefined(name);
                processed.Add(name);
            }

            // Process API identifiers
            foreach (var id in identifiers.GetApiIdentifiers())
            {
                // Skip if already processed
                if (processed.Contains(id.Normalized))
                {
                    continue;
                }
                processed.Add(id.Normalized);

                // Skip if it's a user-defined variable
                if (identifiers.IsUserDefined(id.Text))
                {
                    continue;
                }

                // Check if valid
                if (IsValidIdentifier(id.Normalized))
                {
                    var entry = GetMatchingEntry(id.Normalized);
                    result.AddVerified(new VerifiedIdentifier(
                        id.Text,
                        entry?.Id ?? id.Text,
                        entry?.SourceRef
                    ));
                }
                else
                {
                    // Get suggestions for unverified identifier
                    var suggestions = GetSuggestions(id.Normalized);
                    result.AddUnverified(new UnverifiedIdentifier(id.Text, id.Line, suggestions));
                }
            }

            return result;
        }

        /// <summary>
        /// Check if an identifier is valid (exists in documentation).
        /// </summary>
        private bool IsValidIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return false;

            // Direct match
            if (_validIdentifiers.Contains(identifier))
                return true;

            // For colon-separated identifiers (SHIP:VELOCITY), check if the full path exists
            // or if all parts are valid
            if (identifier.Contains(":"))
            {
                // First check full path match
                if (_validIdentifiers.Contains(identifier))
                    return true;

                // Check if it's a documented suffix path
                // For SHIP:OBT:APOAPSIS, we need to verify the chain is valid
                var parts = identifier.Split(':');
                if (parts.Length >= 2)
                {
                    // Check if base structure and suffix combination exists
                    // e.g., for SHIP:VELOCITY, check VESSEL:VELOCITY (SHIP is alias)
                    string basePart = parts[0];
                    string suffixPath = string.Join(":", parts, 1, parts.Length - 1);

                    // Try direct combination
                    if (_validIdentifiers.Contains($"{basePart}:{suffixPath}"))
                        return true;

                    // Check if base is valid and suffix chain exists
                    if (_validIdentifiers.Contains(basePart))
                    {
                        // Check nested suffixes
                        return ValidateSuffixChain(basePart, parts, 1);
                    }
                }

                return false;
            }

            return false;
        }

        /// <summary>
        /// Validate a chain of suffixes starting from a base structure.
        /// </summary>
        private bool ValidateSuffixChain(string basePart, string[] parts, int startIndex)
        {
            string currentPath = basePart;

            for (int i = startIndex; i < parts.Length; i++)
            {
                string suffix = parts[i];
                string fullPath = $"{currentPath}:{suffix}";

                // Check if this path exists
                if (_validIdentifiers.Contains(fullPath))
                {
                    currentPath = fullPath;
                    continue;
                }

                // Check if just the suffix name is valid (might be on a different base)
                if (_validIdentifiers.Contains(suffix))
                {
                    currentPath = fullPath;
                    continue;
                }

                // Check common suffix pattern: STRUCTURE:SUFFIX
                // e.g., ORBITABLE:APOAPSIS might be documented but accessed via SHIP:OBT:APOAPSIS
                bool foundSuffix = false;
                foreach (var validId in _validIdentifiers)
                {
                    if (validId.EndsWith(":" + suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        foundSuffix = true;
                        break;
                    }
                }

                if (!foundSuffix)
                {
                    return false;
                }

                currentPath = fullPath;
            }

            return true;
        }

        /// <summary>
        /// Get the DocEntry matching an identifier.
        /// </summary>
        private DocEntry GetMatchingEntry(string identifier)
        {
            if (_entryById.TryGetValue(identifier, out var entry))
            {
                return entry;
            }

            // Try partial match for colon-separated
            if (identifier.Contains(":"))
            {
                if (_entryById.TryGetValue(identifier, out entry))
                    return entry;

                // Try just the last part
                var parts = identifier.Split(':');
                var lastPart = parts[parts.Length - 1];
                if (_entryById.TryGetValue(lastPart, out entry))
                    return entry;
            }

            return null;
        }

        /// <summary>
        /// Get "did you mean" suggestions for an invalid identifier.
        /// </summary>
        private List<string> GetSuggestions(string identifier)
        {
            var suggestions = new List<string>();

            if (_searchIndex != null)
            {
                // Use the search index for fuzzy matching
                var results = _searchIndex.Search(identifier, MaxSuggestions);
                foreach (var entry in results)
                {
                    if (!string.IsNullOrEmpty(entry.Id))
                    {
                        suggestions.Add(entry.Id);
                    }
                }
            }
            else
            {
                // Fall back to simple substring matching within our valid set
                foreach (var valid in _validIdentifiers)
                {
                    if (suggestions.Count >= MaxSuggestions)
                        break;

                    // Check if the valid identifier contains or is contained by the input
                    if (valid.IndexOf(identifier, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        identifier.IndexOf(valid, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        suggestions.Add(valid);
                    }
                }
            }

            return suggestions;
        }
    }
}
