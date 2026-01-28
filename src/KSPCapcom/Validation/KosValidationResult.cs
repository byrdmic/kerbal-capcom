using System.Collections.Generic;
using System.Text;
using KSPCapcom.KosDocs;

namespace KSPCapcom.Validation
{
    /// <summary>
    /// An identifier that was verified against documentation.
    /// </summary>
    public class VerifiedIdentifier
    {
        /// <summary>
        /// The identifier text (e.g., "SHIP:VELOCITY").
        /// </summary>
        public string Identifier { get; }

        /// <summary>
        /// The documentation entry ID that matched.
        /// </summary>
        public string DocEntryId { get; }

        /// <summary>
        /// URL to official documentation, if available.
        /// </summary>
        public string SourceRef { get; }

        /// <summary>
        /// The documentation entry that verified this identifier, if available.
        /// </summary>
        public DocEntry SourceDoc { get; internal set; }

        public VerifiedIdentifier(string identifier, string docEntryId, string sourceRef)
        {
            Identifier = identifier ?? string.Empty;
            DocEntryId = docEntryId ?? string.Empty;
            SourceRef = sourceRef;
        }

        public override string ToString()
        {
            return $"{Identifier} -> {DocEntryId}";
        }
    }

    /// <summary>
    /// An identifier that could not be verified against documentation.
    /// </summary>
    public class UnverifiedIdentifier
    {
        /// <summary>
        /// The identifier text that could not be verified.
        /// </summary>
        public string Identifier { get; }

        /// <summary>
        /// Line number where the identifier was found (1-based).
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// Suggested similar identifiers from documentation ("did you mean").
        /// </summary>
        public IReadOnlyList<string> SuggestedMatches { get; }

        public UnverifiedIdentifier(string identifier, int line, IReadOnlyList<string> suggestedMatches)
        {
            Identifier = identifier ?? string.Empty;
            Line = line;
            SuggestedMatches = suggestedMatches ?? new List<string>();
        }

        public override string ToString()
        {
            if (SuggestedMatches.Count > 0)
            {
                return $"{Identifier} (line {Line}) - did you mean: {string.Join(", ", SuggestedMatches)}?";
            }
            return $"{Identifier} (line {Line})";
        }
    }

    /// <summary>
    /// Result of validating kOS identifiers against documentation.
    /// </summary>
    public class KosValidationResult
    {
        private readonly List<VerifiedIdentifier> _verified;
        private readonly List<UnverifiedIdentifier> _unverified;
        private readonly List<string> _userDefined;

        /// <summary>
        /// Identifiers that were verified against documentation.
        /// </summary>
        public IReadOnlyList<VerifiedIdentifier> Verified => _verified;

        /// <summary>
        /// Identifiers that could not be verified (potential hallucinations).
        /// </summary>
        public IReadOnlyList<UnverifiedIdentifier> Unverified => _unverified;

        /// <summary>
        /// User-defined variable names that were skipped during validation.
        /// </summary>
        public IReadOnlyList<string> UserDefined => _userDefined;

        /// <summary>
        /// Whether there are any unverified identifiers.
        /// </summary>
        public bool HasUnverifiedIdentifiers => _unverified.Count > 0;

        /// <summary>
        /// Whether validation was successful (all identifiers verified or user-defined).
        /// </summary>
        public bool IsValid => !HasUnverifiedIdentifiers;

        /// <summary>
        /// Warning message if validation was not run (e.g., docs not loaded).
        /// </summary>
        public string Warning { get; private set; }

        /// <summary>
        /// Result of syntax checking, if performed.
        /// </summary>
        public KosSyntaxResult SyntaxResult { get; private set; }

        /// <summary>
        /// Whether there are any syntax issues detected.
        /// </summary>
        public bool HasSyntaxIssues => SyntaxResult?.HasIssues ?? false;

        /// <summary>
        /// Whether the script needs revision (has syntax issues or unverified identifiers).
        /// </summary>
        public bool NeedsRevision => HasUnverifiedIdentifiers || HasSyntaxIssues;

        /// <summary>
        /// Create a new validation result.
        /// </summary>
        public KosValidationResult()
        {
            _verified = new List<VerifiedIdentifier>();
            _unverified = new List<UnverifiedIdentifier>();
            _userDefined = new List<string>();
        }

        /// <summary>
        /// Add a verified identifier.
        /// </summary>
        internal void AddVerified(VerifiedIdentifier identifier)
        {
            if (identifier != null)
            {
                _verified.Add(identifier);
            }
        }

        /// <summary>
        /// Add an unverified identifier.
        /// </summary>
        internal void AddUnverified(UnverifiedIdentifier identifier)
        {
            if (identifier != null)
            {
                _unverified.Add(identifier);
            }
        }

        /// <summary>
        /// Add a user-defined variable name.
        /// </summary>
        internal void AddUserDefined(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                _userDefined.Add(name);
            }
        }

        /// <summary>
        /// Set a warning message.
        /// </summary>
        internal void SetWarning(string warning)
        {
            Warning = warning;
        }

        /// <summary>
        /// Set the syntax check result.
        /// </summary>
        internal void SetSyntaxResult(KosSyntaxResult syntaxResult)
        {
            SyntaxResult = syntaxResult;
        }

        /// <summary>
        /// Generate a user-friendly summary of the validation result.
        /// </summary>
        public string ToSummary()
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(Warning))
            {
                sb.AppendLine($"Warning: {Warning}");
                sb.AppendLine();
            }

            if (IsValid)
            {
                sb.AppendLine("All kOS identifiers verified against documentation.");
                sb.AppendLine($"  Verified: {_verified.Count}");
                if (_userDefined.Count > 0)
                {
                    sb.AppendLine($"  User-defined: {_userDefined.Count}");
                }
                return sb.ToString();
            }

            sb.AppendLine("VALIDATION WARNING: Unverified kOS identifiers detected.");
            sb.AppendLine();
            sb.AppendLine("The following identifiers could not be verified against kOS documentation:");

            foreach (var unverified in _unverified)
            {
                sb.AppendLine($"  - {unverified}");
            }

            sb.AppendLine();
            sb.AppendLine("These may be hallucinated or invalid. Please verify manually before use.");

            if (_verified.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"({_verified.Count} identifier(s) verified successfully)");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Create an empty result (no identifiers to validate).
        /// </summary>
        public static KosValidationResult Empty()
        {
            return new KosValidationResult();
        }

        /// <summary>
        /// Create a result indicating validation was skipped with a warning.
        /// </summary>
        public static KosValidationResult Skipped(string reason)
        {
            var result = new KosValidationResult();
            result.SetWarning(reason);
            return result;
        }
    }
}
