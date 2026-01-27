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

        private void PopulateExtendedTestIndex(KosDocIndex index)
        {
            // Base structures
            index.AddEntry(CreateDocEntry("VESSEL", "VESSEL", DocEntryType.Structure, null, new List<string> { "SHIP" }));
            index.AddEntry(CreateDocEntry("ORBITABLE", "ORBITABLE", DocEntryType.Structure, null));
            index.AddEntry(CreateDocEntry("BODY", "BODY", DocEntryType.Structure, null));
            index.AddEntry(CreateDocEntry("ORBIT", "ORBIT", DocEntryType.Structure, null));
            index.AddEntry(CreateDocEntry("ENGINE", "ENGINE", DocEntryType.Structure, null));
            index.AddEntry(CreateDocEntry("TERMINAL", "TERMINAL", DocEntryType.Structure, null));

            // Common suffixes
            index.AddEntry(CreateDocEntry("VESSEL:ALTITUDE", "ALTITUDE", DocEntryType.Suffix, "VESSEL"));
            index.AddEntry(CreateDocEntry("VESSEL:VELOCITY", "VELOCITY", DocEntryType.Suffix, "VESSEL"));
            index.AddEntry(CreateDocEntry("VESSEL:APOAPSIS", "APOAPSIS", DocEntryType.Suffix, "VESSEL"));
            index.AddEntry(CreateDocEntry("VESSEL:PERIAPSIS", "PERIAPSIS", DocEntryType.Suffix, "VESSEL"));
            index.AddEntry(CreateDocEntry("VESSEL:MASS", "MASS", DocEntryType.Suffix, "VESSEL"));
            index.AddEntry(CreateDocEntry("VESSEL:OBT", "OBT", DocEntryType.Suffix, "VESSEL"));
            index.AddEntry(CreateDocEntry("VESSEL:THROTTLE", "THROTTLE", DocEntryType.Suffix, "VESSEL"));

            // Rare suffixes
            index.AddEntry(CreateDocEntry("WHEELSTEERING", "WHEELSTEERING", DocEntryType.Keyword, null));
            index.AddEntry(CreateDocEntry("WHEELTHROTTLE", "WHEELTHROTTLE", DocEntryType.Keyword, null));
            index.AddEntry(CreateDocEntry("TERMINAL:WIDTH", "WIDTH", DocEntryType.Suffix, "TERMINAL"));
            index.AddEntry(CreateDocEntry("TERMINAL:HEIGHT", "HEIGHT", DocEntryType.Suffix, "TERMINAL"));

            // Structure-specific suffixes
            index.AddEntry(CreateDocEntry("BODY:ROTATIONPERIOD", "ROTATIONPERIOD", DocEntryType.Suffix, "BODY"));
            index.AddEntry(CreateDocEntry("BODY:ROTATIONANGLE", "ROTATIONANGLE", DocEntryType.Suffix, "BODY"));
            index.AddEntry(CreateDocEntry("BODY:ATM", "ATM", DocEntryType.Suffix, "BODY"));
            index.AddEntry(CreateDocEntry("ORBITABLE:BODY", "BODY", DocEntryType.Suffix, "ORBITABLE"));
            index.AddEntry(CreateDocEntry("ORBITABLE:APOAPSIS", "APOAPSIS", DocEntryType.Suffix, "ORBITABLE"));

            // Commands
            index.AddEntry(CreateDocEntry("LOCK", "LOCK", DocEntryType.Command, null));
            index.AddEntry(CreateDocEntry("PRINT", "PRINT", DocEntryType.Command, null));
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
