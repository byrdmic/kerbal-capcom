using System.Collections.Generic;
using NUnit.Framework;
using KSPCapcom.KosDocs;
using KSPCapcom.Validation;

namespace KSPCapcom.Tests.Validation
{
    /// <summary>
    /// Colon-separated pattern tests for suffix chains.
    /// </summary>
    public partial class KosIdentifierValidatorTests
    {
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
    }
}
