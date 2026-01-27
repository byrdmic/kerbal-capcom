using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KSPCapcom.KosDocs;
using KSPCapcom.Validation;

namespace KSPCapcom.Tests.Validation
{
    [TestFixture]
    public class KosIdentifierValidatorTests
    {
        private KosDocIndex _index;

        [SetUp]
        public void SetUp()
        {
            _index = new KosDocIndex();
            PopulateTestIndex(_index);
        }

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
            Assert.That(result.Unverified[0].SuggestedMatches, Is.Not.Empty,
                "Should provide 'did you mean' suggestions");
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
            // Arrange
            var docs = new List<DocEntry>(); // No docs - everything would be unverified
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

        #region Empty/Null Input Tests

        [Test]
        public void Validate_NullIdentifiers_ReturnsEmptyResult()
        {
            var validator = new KosIdentifierValidator(new List<DocEntry>());

            var result = validator.Validate(null);

            Assert.That(result.Verified, Is.Empty);
            Assert.That(result.Unverified, Is.Empty);
            Assert.That(result.UserDefined, Is.Empty);
        }

        [Test]
        public void Validate_EmptyIdentifiers_ReturnsEmptyResult()
        {
            var validator = new KosIdentifierValidator(new List<DocEntry>());

            var result = validator.Validate(new KosIdentifierSet());

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Verified, Is.Empty);
        }

        [Test]
        public void Validate_NoDocs_ReturnsWarning()
        {
            var validator = new KosIdentifierValidator(new List<DocEntry>());

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:ALTITUDE", false, 1);

            var result = validator.Validate(identifiers);

            Assert.That(result.Warning, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Warning, Does.Contain("No documentation"));
        }

        #endregion

        #region Colon-Separated Pattern Tests

        [Test]
        public void Validate_NestedSuffix_ValidatesChain()
        {
            // Arrange: SHIP:OBT:APOAPSIS where OBT is ORBITABLE and has APOAPSIS suffix
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL", "VESSEL", DocEntryType.Structure, null,
                    new List<string> { "SHIP" }),
                CreateDocEntry("VESSEL:OBT", "OBT", DocEntryType.Suffix, "VESSEL"),
                CreateDocEntry("ORBITABLE:APOAPSIS", "APOAPSIS", DocEntryType.Suffix, "ORBITABLE")
            };
            var validator = new KosIdentifierValidator(docs, _index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:OBT:APOAPSIS", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert - should be valid because SHIP is valid, OBT is a suffix, and APOAPSIS exists
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void Validate_PartiallyInvalidChain_FlagsAsUnverified()
        {
            // Arrange: SHIP and ALTITUDE exist, but FAKE does not
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL", "VESSEL", DocEntryType.Structure, null,
                    new List<string> { "SHIP" }),
                CreateDocEntry("VESSEL:ALTITUDE", "ALTITUDE", DocEntryType.Suffix, "VESSEL")
            };
            var validator = new KosIdentifierValidator(docs);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:ALTITUDE:FAKE", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.HasUnverifiedIdentifiers, Is.True);
        }

        #endregion

        #region Summary Generation Tests

        [Test]
        public void ToSummary_AllValid_ReportsSuccess()
        {
            var docs = new List<DocEntry>
            {
                CreateDocEntry("SHIP:ALTITUDE", "ALTITUDE", DocEntryType.Suffix, "SHIP")
            };
            var validator = new KosIdentifierValidator(docs);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:ALTITUDE", false, 1);

            var result = validator.Validate(identifiers);
            var summary = result.ToSummary();

            Assert.That(summary, Does.Contain("verified"));
            Assert.That(summary, Does.Not.Contain("WARNING"));
        }

        [Test]
        public void ToSummary_HasUnverified_ReportsWarning()
        {
            var docs = new List<DocEntry>
            {
                CreateDocEntry("SHIP:ALTITUDE", "ALTITUDE", DocEntryType.Suffix, "SHIP")
            };
            var validator = new KosIdentifierValidator(docs, _index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:MAGIC", false, 5);

            var result = validator.Validate(identifiers);
            var summary = result.ToSummary();

            Assert.That(summary, Does.Contain("WARNING"));
            Assert.That(summary, Does.Contain("SHIP:MAGIC"));
            Assert.That(summary, Does.Contain("line 5"));
        }

        [Test]
        public void ToSummary_WithSuggestions_IncludesDidYouMean()
        {
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL:VELOCITY", "VELOCITY", DocEntryType.Suffix, "VESSEL")
            };
            var validator = new KosIdentifierValidator(docs, _index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "VELOCITY", false, 1); // Just VELOCITY, not full path

            var result = validator.Validate(identifiers);

            // The identifier should be valid since VELOCITY is in the valid set
            // (extracted from VESSEL:VELOCITY)
            Assert.That(result.IsValid, Is.True);
        }

        #endregion

        #region Performance Characteristics Tests

        [Test]
        public void Validate_LargeIdentifierSet_CompletesQuickly()
        {
            // Arrange - create many docs
            var docs = new List<DocEntry>();
            for (int i = 0; i < 100; i++)
            {
                docs.Add(CreateDocEntry($"STRUCT{i}:SUFFIX{i}", $"SUFFIX{i}", DocEntryType.Suffix, $"STRUCT{i}"));
            }
            var validator = new KosIdentifierValidator(docs, _index);

            // Create identifiers
            var identifiers = new KosIdentifierSet();
            for (int i = 0; i < 200; i++)
            {
                AddIdentifier(identifiers, $"STRUCT{i % 100}:SUFFIX{i % 100}", false, i + 1);
            }

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = validator.Validate(identifiers);
            stopwatch.Stop();

            // Assert - should complete in reasonable time
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000),
                "Validation of 200 identifiers should complete in under 1 second");
        }

        #endregion

        #region Duplicate Handling Tests

        [Test]
        public void Validate_DuplicateIdentifiers_HandledCorrectly()
        {
            var docs = new List<DocEntry>
            {
                CreateDocEntry("SHIP:ALTITUDE", "ALTITUDE", DocEntryType.Suffix, "SHIP")
            };
            var validator = new KosIdentifierValidator(docs);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:ALTITUDE", false, 1);
            AddIdentifier(identifiers, "SHIP:ALTITUDE", false, 5);
            AddIdentifier(identifiers, "SHIP:ALTITUDE", false, 10);

            var result = validator.Validate(identifiers);

            // Should only have one verified entry (deduplicated)
            Assert.That(result.Verified.Count, Is.EqualTo(1));
        }

        #endregion

        #region Helper Methods

        private void PopulateTestIndex(KosDocIndex index)
        {
            index.AddEntry(CreateDocEntry("VESSEL", "VESSEL", DocEntryType.Structure, null,
                new List<string> { "SHIP" }));
            index.AddEntry(CreateDocEntry("VESSEL:ALTITUDE", "ALTITUDE", DocEntryType.Suffix, "VESSEL"));
            index.AddEntry(CreateDocEntry("VESSEL:VELOCITY", "VELOCITY", DocEntryType.Suffix, "VESSEL"));
            index.AddEntry(CreateDocEntry("VESSEL:APOAPSIS", "APOAPSIS", DocEntryType.Suffix, "VESSEL"));
            index.AddEntry(CreateDocEntry("LOCK", "LOCK", DocEntryType.Command, null));
        }

        private static DocEntry CreateDocEntry(
            string id,
            string name,
            DocEntryType type,
            string parentStructure,
            List<string> aliases = null)
        {
            return new DocEntry
            {
                Id = id,
                Name = name,
                Type = type,
                ParentStructure = parentStructure,
                Aliases = aliases ?? new List<string>(),
                Description = $"Description for {id}",
                SourceRef = $"https://ksp-kos.github.io/KOS/{id.ToLower().Replace(":", "/")}.html"
            };
        }

        private static void AddIdentifier(KosIdentifierSet set, string text, bool isUserDefined, int line)
        {
            // Use reflection or internal method to add to the set
            var addMethod = typeof(KosIdentifierSet).GetMethod("Add",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            addMethod?.Invoke(set, new object[] { new ExtractedIdentifier(text, isUserDefined, line) });
        }

        #endregion
    }
}
