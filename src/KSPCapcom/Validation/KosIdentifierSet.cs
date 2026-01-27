using System.Collections.Generic;

namespace KSPCapcom.Validation
{
    /// <summary>
    /// Represents a single identifier extracted from kOS script text.
    /// </summary>
    public class ExtractedIdentifier
    {
        /// <summary>
        /// Original text as it appeared in the script (e.g., "SHIP:VELOCITY").
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Uppercase normalized form for comparison.
        /// </summary>
        public string Normalized { get; }

        /// <summary>
        /// Whether this identifier was declared/defined in the script (user variable).
        /// </summary>
        public bool IsUserDefined { get; }

        /// <summary>
        /// Line number where the identifier was found (1-based).
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// Create a new extracted identifier.
        /// </summary>
        /// <param name="text">Original text.</param>
        /// <param name="isUserDefined">Whether it's a user-defined variable.</param>
        /// <param name="line">Line number (1-based).</param>
        public ExtractedIdentifier(string text, bool isUserDefined, int line)
        {
            Text = text ?? string.Empty;
            Normalized = Text.ToUpperInvariant();
            IsUserDefined = isUserDefined;
            Line = line;
        }

        public override string ToString()
        {
            return IsUserDefined ? $"{Text} (user-defined)" : Text;
        }

        public override bool Equals(object obj)
        {
            if (obj is ExtractedIdentifier other)
            {
                return Normalized == other.Normalized && IsUserDefined == other.IsUserDefined;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Normalized.GetHashCode() ^ IsUserDefined.GetHashCode();
        }
    }

    /// <summary>
    /// Collection of identifiers extracted from a kOS script.
    /// </summary>
    public class KosIdentifierSet
    {
        private readonly List<ExtractedIdentifier> _identifiers;
        private readonly HashSet<string> _userDefinedNames;

        /// <summary>
        /// All extracted identifiers.
        /// </summary>
        public IReadOnlyList<ExtractedIdentifier> Identifiers => _identifiers;

        /// <summary>
        /// Set of user-defined variable names (normalized to uppercase).
        /// </summary>
        public IReadOnlyCollection<string> UserDefinedNames => _userDefinedNames;

        /// <summary>
        /// Create a new empty identifier set.
        /// </summary>
        public KosIdentifierSet()
        {
            _identifiers = new List<ExtractedIdentifier>();
            _userDefinedNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Add an identifier to the set.
        /// </summary>
        internal void Add(ExtractedIdentifier identifier)
        {
            if (identifier == null) return;

            _identifiers.Add(identifier);

            if (identifier.IsUserDefined)
            {
                _userDefinedNames.Add(identifier.Normalized);
            }
        }

        /// <summary>
        /// Check if a name is user-defined.
        /// </summary>
        /// <param name="name">The identifier name to check.</param>
        /// <returns>True if the name was declared as a user variable.</returns>
        public bool IsUserDefined(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return _userDefinedNames.Contains(name.ToUpperInvariant());
        }

        /// <summary>
        /// Get all identifiers that are not user-defined (potential kOS API calls).
        /// </summary>
        public IEnumerable<ExtractedIdentifier> GetApiIdentifiers()
        {
            foreach (var id in _identifiers)
            {
                if (!id.IsUserDefined && !_userDefinedNames.Contains(id.Normalized))
                {
                    yield return id;
                }
            }
        }

        /// <summary>
        /// Number of identifiers in the set.
        /// </summary>
        public int Count => _identifiers.Count;

        /// <summary>
        /// Whether the set is empty.
        /// </summary>
        public bool IsEmpty => _identifiers.Count == 0;
    }
}
