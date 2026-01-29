using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KSPCapcom.KosDocs;
using KSPCapcom.Validation;

namespace KSPCapcom.Tests.Validation
{
    /// <summary>
    /// Misspelling detection tests for common typos.
    /// </summary>
    public partial class KosIdentifierValidatorTests
    {
        #region Misspelling Detection Tests

        [Test]
        public void Validate_MisspelledVelocty_FlagsAsUnverified()
        {
            // Arrange - VELOCTY (missing I)
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL:VELOCITY", "VELOCITY", DocEntryType.Suffix, "VESSEL")
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:VELOCTY", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.HasUnverifiedIdentifiers, Is.True);
            Assert.That(result.Unverified.Any(u => u.Identifier == "SHIP:VELOCTY"), Is.True);
        }

        [Test]
        public void Validate_MisspelledAltitued_FlagsWithSuggestions()
        {
            // Arrange - ALTITUED (transposed U-E)
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL:ALTITUDE", "ALTITUDE", DocEntryType.Suffix, "VESSEL")
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:ALTITUED", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.HasUnverifiedIdentifiers, Is.True);
            var unverified = result.Unverified.FirstOrDefault(u => u.Identifier == "SHIP:ALTITUED");
            Assert.That(unverified, Is.Not.Null);
        }

        [Test]
        public void Validate_MisspelledThrutle_FlagsAsUnverified()
        {
            // Arrange - THRUTLE (missing T)
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("THROTTLE", "THROTTLE", DocEntryType.Keyword, null)
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "THRUTLE", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.HasUnverifiedIdentifiers, Is.True);
            Assert.That(result.Unverified.Any(u => u.Identifier == "THRUTLE"), Is.True);
        }

        [Test]
        public void Validate_CaseVariations_AllMatchDocumented()
        {
            // Arrange - velocity vs VELOCITY vs Velocity should all match
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL", "VESSEL", DocEntryType.Structure, null, new List<string> { "SHIP" }),
                CreateDocEntry("VESSEL:VELOCITY", "VELOCITY", DocEntryType.Suffix, "VESSEL")
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:velocity", false, 1);
            AddIdentifier(identifiers, "SHIP:VELOCITY", false, 2);
            AddIdentifier(identifiers, "SHIP:Velocity", false, 3);

            // Act
            var result = validator.Validate(identifiers);

            // Assert - all case variations should be valid (identifiers are normalized)
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void Validate_MisspelledApoapis_FlagsAsUnverified()
        {
            // Arrange - APOAPIS (swapped letters)
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL:APOAPSIS", "APOAPSIS", DocEntryType.Suffix, "VESSEL")
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:APOAPIS", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.HasUnverifiedIdentifiers, Is.True);
            Assert.That(result.Unverified.Any(u => u.Identifier == "SHIP:APOAPIS"), Is.True);
        }

        [Test]
        public void Validate_MisspelledPeriaosis_FlagsAsUnverified()
        {
            // Arrange - PERIAOSIS (wrong vowel)
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL:PERIAPSIS", "PERIAPSIS", DocEntryType.Suffix, "VESSEL")
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:PERIAOSIS", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.HasUnverifiedIdentifiers, Is.True);
            Assert.That(result.Unverified.Any(u => u.Identifier == "SHIP:PERIAOSIS"), Is.True);
        }

        #endregion
    }
}
