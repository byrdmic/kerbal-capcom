using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KSPCapcom.KosDocs;
using KSPCapcom.Validation;

namespace KSPCapcom.Tests.Validation
{
    /// <summary>
    /// Core validation tests: Critical, User-Defined, and Alias tests.
    /// </summary>
    public partial class KosIdentifierValidatorTests
    {
        #region Critical Test: Invented Suffix Detection

        /// <summary>
        /// CRITICAL TEST: Validates that invented/hallucinated suffixes are flagged as unverified.
        /// This is the core purpose of the validation system - detecting when the LLM
        /// invents kOS identifiers that don't exist in the documentation.
        /// </summary>
        [Test]
        public void Validate_InventedSuffix_FlagsAsUnverified()
        {
            // Arrange: docs only contain SHIP:VELOCITY, not SHIP:MAGIC
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL:VELOCITY", "VELOCITY", DocEntryType.Suffix, "VESSEL")
            };
            var validator = new KosIdentifierValidator(docs, _index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:MAGIC", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.HasUnverifiedIdentifiers, Is.True,
                "SHIP:MAGIC should be flagged as unverified");
            Assert.That(result.Unverified.Any(u => u.Identifier == "SHIP:MAGIC"), Is.True,
                "SHIP:MAGIC should appear in unverified list");
            // Note: Suggestions may or may not be provided depending on search algorithm
            // The critical behavior is that invented suffixes are flagged as unverified
        }

        [Test]
        public void Validate_ValidSuffix_MarksAsVerified()
        {
            // Arrange
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL:VELOCITY", "VELOCITY", DocEntryType.Suffix, "VESSEL")
            };
            var validator = new KosIdentifierValidator(docs, _index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "VESSEL:VELOCITY", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Verified.Any(v => v.Identifier == "VESSEL:VELOCITY"), Is.True);
        }

        #endregion

        #region User-Defined Variable Tests

        [Test]
        public void Validate_UserDefinedVariable_SkipsValidation()
        {
            // Arrange - provide docs so validation actually runs (not early warning return)
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL", "VESSEL", DocEntryType.Structure, null)
            };
            var validator = new KosIdentifierValidator(docs);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "myVar", true, 1); // User-defined

            // Act
            var result = validator.Validate(identifiers);

            // Assert - user-defined variables should not be marked as unverified
            Assert.That(result.UserDefined.Contains("MYVAR"), Is.True);
            Assert.That(result.Unverified.Any(u => u.Identifier.Equals("myVar", System.StringComparison.OrdinalIgnoreCase)), Is.False);
        }

        [Test]
        public void Validate_MixedUserAndApi_ValidatesOnlyApi()
        {
            // Arrange
            var docs = new List<DocEntry>
            {
                CreateDocEntry("SHIP:ALTITUDE", "ALTITUDE", DocEntryType.Suffix, "SHIP")
            };
            var validator = new KosIdentifierValidator(docs);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "x", true, 1);            // User-defined
            AddIdentifier(identifiers, "SHIP:ALTITUDE", false, 2); // Valid API
            AddIdentifier(identifiers, "SHIP:FAKE", false, 3);     // Invalid API

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.UserDefined.Contains("X"), Is.True);
            Assert.That(result.Verified.Any(v => v.Identifier == "SHIP:ALTITUDE"), Is.True);
            Assert.That(result.Unverified.Any(u => u.Identifier == "SHIP:FAKE"), Is.True);
        }

        #endregion

        #region Alias Tests

        [Test]
        public void Validate_AliasIdentifier_MarksAsVerified()
        {
            // Arrange: SHIP is an alias for VESSEL
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL", "VESSEL", DocEntryType.Structure, null,
                    new List<string> { "SHIP" })
            };
            var validator = new KosIdentifierValidator(docs);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Verified.Any(v => v.Identifier == "SHIP"), Is.True);
        }

        #endregion
    }
}
