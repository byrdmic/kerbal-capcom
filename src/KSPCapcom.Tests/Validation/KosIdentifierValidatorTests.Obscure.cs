using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KSPCapcom.KosDocs;
using KSPCapcom.Validation;

namespace KSPCapcom.Tests.Validation
{
    /// <summary>
    /// Obscure/rare API tests for less common kOS identifiers.
    /// </summary>
    public partial class KosIdentifierValidatorTests
    {
        #region Obscure/Rare API Tests

        [Test]
        public void Validate_WheelSteering_RecognizedWhenDocumented()
        {
            // Arrange - rarely-used rover control identifiers
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("WHEELSTEERING", "WHEELSTEERING", DocEntryType.Keyword, null)
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "WHEELSTEERING", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Verified.Any(v => v.Identifier == "WHEELSTEERING"), Is.True);
        }

        [Test]
        public void Validate_WheelThrottle_RecognizedWhenDocumented()
        {
            // Arrange
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("WHEELTHROTTLE", "WHEELTHROTTLE", DocEntryType.Keyword, null)
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "WHEELTHROTTLE", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Verified.Any(v => v.Identifier == "WHEELTHROTTLE"), Is.True);
        }

        [Test]
        public void Validate_DeeplyNestedSuffixChain_ValidatesCorrectly()
        {
            // Arrange - 4-level chain: SHIP:OBT:BODY:ROTATIONPERIOD
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL", "VESSEL", DocEntryType.Structure, null, new List<string> { "SHIP" }),
                CreateDocEntry("VESSEL:OBT", "OBT", DocEntryType.Suffix, "VESSEL"),
                CreateDocEntry("ORBITABLE:BODY", "BODY", DocEntryType.Suffix, "ORBITABLE"),
                CreateDocEntry("BODY:ROTATIONPERIOD", "ROTATIONPERIOD", DocEntryType.Suffix, "BODY")
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:OBT:BODY:ROTATIONPERIOD", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert - deeply nested chain should validate
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void Validate_StructureSpecificSuffix_DistinguishesCorrectly()
        {
            // Arrange - BODY:ROTATIONANGLE vs generic ORBITABLE suffixes
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("BODY", "BODY", DocEntryType.Structure, null),
                CreateDocEntry("BODY:ROTATIONANGLE", "ROTATIONANGLE", DocEntryType.Suffix, "BODY"),
                CreateDocEntry("ORBITABLE:APOAPSIS", "APOAPSIS", DocEntryType.Suffix, "ORBITABLE")
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "BODY:ROTATIONANGLE", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert - BODY-specific suffix should be valid
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Verified.Any(v => v.Identifier == "BODY:ROTATIONANGLE"), Is.True);
        }

        [Test]
        public void Validate_OrbitableVsBody_DifferentSuffixes()
        {
            // Arrange - distinguish between ORBITABLE and BODY suffixes
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("ORBITABLE", "ORBITABLE", DocEntryType.Structure, null),
                CreateDocEntry("BODY", "BODY", DocEntryType.Structure, null),
                CreateDocEntry("ORBITABLE:BODY", "BODY", DocEntryType.Suffix, "ORBITABLE"),
                CreateDocEntry("BODY:ATM", "ATM", DocEntryType.Suffix, "BODY")
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "ORBITABLE:BODY", false, 1);
            AddIdentifier(identifiers, "BODY:ATM", false, 2);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Verified.Count, Is.EqualTo(2));
        }

        [Test]
        public void Validate_TerminalSuffixes_RecognizedWhenDocumented()
        {
            // Arrange - TERMINAL:WIDTH, TERMINAL:HEIGHT
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("TERMINAL", "TERMINAL", DocEntryType.Structure, null),
                CreateDocEntry("TERMINAL:WIDTH", "WIDTH", DocEntryType.Suffix, "TERMINAL"),
                CreateDocEntry("TERMINAL:HEIGHT", "HEIGHT", DocEntryType.Suffix, "TERMINAL")
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "TERMINAL:WIDTH", false, 1);
            AddIdentifier(identifiers, "TERMINAL:HEIGHT", false, 2);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Verified.Any(v => v.Identifier == "TERMINAL:WIDTH"), Is.True);
            Assert.That(result.Verified.Any(v => v.Identifier == "TERMINAL:HEIGHT"), Is.True);
        }

        #endregion
    }
}
