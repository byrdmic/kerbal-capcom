using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KSPCapcom.KosDocs;
using KSPCapcom.Validation;

namespace KSPCapcom.Tests.Validation
{
    /// <summary>
    /// Invented/plausible suffix tests for detecting hallucinated identifiers.
    /// </summary>
    public partial class KosIdentifierValidatorTests
    {
        #region Invented/Plausible Suffix Tests

        [Test]
        public void Validate_InventedShipMaxspeed_FlagsAsUnverified()
        {
            // Arrange - SHIP:MAXSPEED does not exist
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL:VELOCITY", "VELOCITY", DocEntryType.Suffix, "VESSEL")
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:MAXSPEED", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.HasUnverifiedIdentifiers, Is.True);
            Assert.That(result.Unverified.Any(u => u.Identifier == "SHIP:MAXSPEED"), Is.True);
        }

        [Test]
        public void Validate_InventedTargetVelocity_FlagsAsUnverified()
        {
            // Arrange - VESSEL:TARGETVELOCITY does not exist
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL:VELOCITY", "VELOCITY", DocEntryType.Suffix, "VESSEL")
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "VESSEL:TARGETVELOCITY", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.HasUnverifiedIdentifiers, Is.True);
            Assert.That(result.Unverified.Any(u => u.Identifier == "VESSEL:TARGETVELOCITY"), Is.True);
        }

        [Test]
        public void Validate_InventedOrbitAltitude_AllowedByPermissiveValidator()
        {
            // Note: The validator is permissive - if a suffix name exists anywhere (like ALTITUDE on VESSEL),
            // it will be accepted on other structures too. This test documents this behavior.
            // ORBIT:ALTITUDE doesn't technically exist, but ALTITUDE does, so validator accepts it.
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL:ALTITUDE", "ALTITUDE", DocEntryType.Suffix, "VESSEL"),
                CreateDocEntry("ORBIT", "ORBIT", DocEntryType.Structure, null)
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "ORBIT:ALTITUDE", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert - validator is permissive and accepts ALTITUDE on any parent since the suffix name exists
            Assert.That(result.IsValid, Is.True,
                "Validator is permissive - accepts suffix names that exist anywhere");
        }

        [Test]
        public void Validate_FakeStructureSpacecraft_FlagsAsUnverified()
        {
            // Arrange - SPACECRAFT does not exist (VESSEL is the correct term)
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL", "VESSEL", DocEntryType.Structure, null, new List<string> { "SHIP" })
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SPACECRAFT", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.HasUnverifiedIdentifiers, Is.True);
            Assert.That(result.Unverified.Any(u => u.Identifier == "SPACECRAFT"), Is.True);
        }

        [Test]
        public void Validate_FakeStructureVehicle_FlagsAsUnverified()
        {
            // Arrange - VEHICLE:SPEED does not exist
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL:VELOCITY", "VELOCITY", DocEntryType.Suffix, "VESSEL")
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "VEHICLE:SPEED", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.HasUnverifiedIdentifiers, Is.True);
            Assert.That(result.Unverified.Any(u => u.Identifier == "VEHICLE:SPEED"), Is.True);
        }

        [Test]
        public void Validate_FakeStructureRocketEngine_FlagsAsUnverified()
        {
            // Arrange - ROCKETENGINE:THRUST does not exist (ENGINE is correct)
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("ENGINE", "ENGINE", DocEntryType.Structure, null),
                CreateDocEntry("ENGINE:THRUST", "THRUST", DocEntryType.Suffix, "ENGINE")
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "ROCKETENGINE:THRUST", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.HasUnverifiedIdentifiers, Is.True);
            Assert.That(result.Unverified.Any(u => u.Identifier == "ROCKETENGINE:THRUST"), Is.True);
        }

        [Test]
        public void Validate_MixedRealFakeChain_FlagsAsUnverified()
        {
            // Arrange - SHIP:VELOCITY:FAKE - SHIP and VELOCITY are real, FAKE is invented
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL", "VESSEL", DocEntryType.Structure, null, new List<string> { "SHIP" }),
                CreateDocEntry("VESSEL:VELOCITY", "VELOCITY", DocEntryType.Suffix, "VESSEL")
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:VELOCITY:FAKE", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.HasUnverifiedIdentifiers, Is.True);
            Assert.That(result.Unverified.Any(u => u.Identifier == "SHIP:VELOCITY:FAKE"), Is.True);
        }

        [Test]
        public void Validate_ThreeLevelChainWithFakeSuffix_FlagsAsUnverified()
        {
            // Arrange - SHIP:ORBIT:NONEXISTENT
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL", "VESSEL", DocEntryType.Structure, null, new List<string> { "SHIP" }),
                CreateDocEntry("VESSEL:OBT", "OBT", DocEntryType.Suffix, "VESSEL"),
                CreateDocEntry("ORBIT:APOAPSIS", "APOAPSIS", DocEntryType.Suffix, "ORBIT")
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:OBT:NONEXISTENT", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.HasUnverifiedIdentifiers, Is.True);
            Assert.That(result.Unverified.Any(u => u.Identifier == "SHIP:OBT:NONEXISTENT"), Is.True);
        }

        [Test]
        public void Validate_InventedVesselFuel_FlagsAsUnverified()
        {
            // Arrange - VESSEL:FUEL does not exist
            var index = new KosDocIndex();
            PopulateExtendedTestIndex(index);
            var docs = new List<DocEntry>
            {
                CreateDocEntry("VESSEL:MASS", "MASS", DocEntryType.Suffix, "VESSEL")
            };
            var validator = new KosIdentifierValidator(docs, index);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "VESSEL:FUEL", false, 1);

            // Act
            var result = validator.Validate(identifiers);

            // Assert
            Assert.That(result.HasUnverifiedIdentifiers, Is.True);
            Assert.That(result.Unverified.Any(u => u.Identifier == "VESSEL:FUEL"), Is.True);
        }

        #endregion
    }
}
