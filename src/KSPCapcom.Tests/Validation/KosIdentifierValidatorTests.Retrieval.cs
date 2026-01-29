using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KSPCapcom.KosDocs;
using KSPCapcom.Validation;

namespace KSPCapcom.Tests.Validation
{
    /// <summary>
    /// Empty/irrelevant retrieval tests for documentation coverage scenarios.
    /// </summary>
    public partial class KosIdentifierValidatorTests
    {
        #region Empty/Irrelevant Retrieval Tests

        [Test]
        public void Validate_EmptyDocEntryTracker_ReturnsWarning()
        {
            // Arrange - no documentation retrieved
            var validator = new KosIdentifierValidator(new List<DocEntry>());

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:ALTITUDE", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.Warning, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void Validate_IrrelevantDocs_FlagsAllAsUnverified()
        {
            // Arrange - docs don't match any identifiers in the script
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("TERMINAL:WIDTH", "WIDTH", DocEntryType.Suffix, "TERMINAL"),
                CreateDocEntry("TERMINAL:HEIGHT", "HEIGHT", DocEntryType.Suffix, "TERMINAL")
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:ALTITUDE", false, 1);
            AddIdentifier(identifiers, "SHIP:VELOCITY", false, 2);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.HasUnverifiedIdentifiers, Is.True);
            Assert.That(result.Unverified.Count, Is.EqualTo(2));
        }

        [Test]
        public void Validate_PartialDocCoverage_MixedResults()
        {
            // Arrange - some identifiers covered, some not
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL", "VESSEL", DocEntryType.Structure, null, new List<string> { "SHIP" }),
                CreateDocEntry("VESSEL:ALTITUDE", "ALTITUDE", DocEntryType.Suffix, "VESSEL")
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:ALTITUDE", false, 1);  // Covered
            AddIdentifier(identifiers, "SHIP:VELOCITY", false, 2);  // Not covered

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.HasUnverifiedIdentifiers, Is.True);
            Assert.That(result.Verified.Any(v => v.Identifier == "SHIP:ALTITUDE"), Is.True);
            Assert.That(result.Unverified.Any(u => u.Identifier == "SHIP:VELOCITY"), Is.True);
        }

        [Test]
        public void Validate_CompletelyUnrelatedDocs_AllUnverified()
        {
            // Arrange - docs about unrelated topics
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("PRINT", "PRINT", DocEntryType.Command, null),
                CreateDocEntry("WAIT", "WAIT", DocEntryType.Command, null)
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:ALTITUDE", false, 1);
            AddIdentifier(identifiers, "SHIP:VELOCITY", false, 2);
            AddIdentifier(identifiers, "SHIP:MASS", false, 3);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.HasUnverifiedIdentifiers, Is.True);
            Assert.That(result.Unverified.Count, Is.EqualTo(3));
        }

        #endregion
    }
}
