using System.Linq;
using NUnit.Framework;
using KSPCapcom.Validation;

namespace KSPCapcom.Tests.Validation
{
    [TestFixture]
    public class KosIdentifierExtractorTests
    {
        private KosIdentifierExtractor _extractor;

        [SetUp]
        public void SetUp()
        {
            _extractor = new KosIdentifierExtractor();
        }

        #region Basic Extraction Tests

        [Test]
        public void Extract_EmptyScript_ReturnsEmptySet()
        {
            var result = _extractor.Extract("");

            Assert.That(result.IsEmpty, Is.True);
            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public void Extract_NullScript_ReturnsEmptySet()
        {
            var result = _extractor.Extract(null);

            Assert.That(result.IsEmpty, Is.True);
        }

        [Test]
        public void Extract_SimpleIdentifier_ExtractsCorrectly()
        {
            var result = _extractor.Extract("PRINT ALTITUDE.");

            Assert.That(result.Identifiers.Any(i => i.Normalized == "ALTITUDE"), Is.True);
            // PRINT is a keyword and correctly not extracted as an identifier
            Assert.That(result.Identifiers.Any(i => i.Normalized == "PRINT"), Is.False);
        }

        [Test]
        public void Extract_ColonSeparatedIdentifier_ExtractsFullPath()
        {
            var result = _extractor.Extract("PRINT SHIP:ALTITUDE.");

            Assert.That(result.Identifiers.Any(i => i.Normalized == "SHIP:ALTITUDE"), Is.True);
        }

        [Test]
        public void Extract_ColonSeparatedIdentifier_ExtractsParts()
        {
            var result = _extractor.Extract("SET v TO SHIP:VELOCITY.");

            // Should have both the full path and individual parts
            Assert.That(result.Identifiers.Any(i => i.Normalized == "SHIP:VELOCITY"), Is.True);
            Assert.That(result.Identifiers.Any(i => i.Normalized == "SHIP"), Is.True);
            Assert.That(result.Identifiers.Any(i => i.Normalized == "VELOCITY"), Is.True);
        }

        [Test]
        public void Extract_NestedSuffixes_ExtractsCorrectly()
        {
            var result = _extractor.Extract("PRINT SHIP:OBT:APOAPSIS.");

            Assert.That(result.Identifiers.Any(i => i.Normalized == "SHIP:OBT:APOAPSIS"), Is.True);
            Assert.That(result.Identifiers.Any(i => i.Normalized == "SHIP"), Is.True);
            Assert.That(result.Identifiers.Any(i => i.Normalized == "OBT"), Is.True);
            Assert.That(result.Identifiers.Any(i => i.Normalized == "APOAPSIS"), Is.True);
        }

        #endregion

        #region User-Defined Variable Tests

        [Test]
        public void Extract_SetVariable_MarksAsUserDefined()
        {
            var result = _extractor.Extract("SET x TO 5.");

            Assert.That(result.IsUserDefined("x"), Is.True);
            Assert.That(result.Identifiers.Any(i => i.Normalized == "X" && i.IsUserDefined), Is.True);
        }

        [Test]
        public void Extract_DeclareVariable_MarksAsUserDefined()
        {
            var result = _extractor.Extract("DECLARE myVar.");

            Assert.That(result.IsUserDefined("myVar"), Is.True);
        }

        [Test]
        public void Extract_LocalVariable_MarksAsUserDefined()
        {
            var result = _extractor.Extract("LOCAL myVar IS 10.");

            Assert.That(result.IsUserDefined("myVar"), Is.True);
        }

        [Test]
        public void Extract_LocalVariableWithTo_MarksAsUserDefined()
        {
            var result = _extractor.Extract("LOCAL myVar TO 10.");

            Assert.That(result.IsUserDefined("myVar"), Is.True);
        }

        [Test]
        public void Extract_GlobalVariable_MarksAsUserDefined()
        {
            var result = _extractor.Extract("GLOBAL myVar IS 10.");

            Assert.That(result.IsUserDefined("myVar"), Is.True);
        }

        [Test]
        public void Extract_Parameter_MarksAsUserDefined()
        {
            var result = _extractor.Extract("PARAMETER x.");

            Assert.That(result.IsUserDefined("x"), Is.True);
        }

        [Test]
        public void Extract_ForLoop_MarksIteratorAsUserDefined()
        {
            var result = _extractor.Extract("FOR p IN SHIP:PARTS { PRINT p. }");

            Assert.That(result.IsUserDefined("p"), Is.True);
            // SHIP:PARTS should NOT be user-defined
            Assert.That(result.IsUserDefined("SHIP"), Is.False);
            Assert.That(result.Identifiers.Any(i => i.Normalized == "SHIP:PARTS" && !i.IsUserDefined), Is.True);
        }

        [Test]
        public void Extract_Function_MarksNameAsUserDefined()
        {
            var result = _extractor.Extract("FUNCTION myFunc { RETURN 1. }");

            Assert.That(result.IsUserDefined("myFunc"), Is.True);
        }

        [Test]
        public void Extract_LockCustomVariable_MarksAsUserDefined()
        {
            var result = _extractor.Extract("LOCK myHeading TO HEADING(90, 10).");

            Assert.That(result.IsUserDefined("myHeading"), Is.True);
        }

        [Test]
        public void Extract_LockSteering_NotMarkedAsUserDefined()
        {
            var result = _extractor.Extract("LOCK STEERING TO PROGRADE.");

            // STEERING is a built-in, not user-defined
            Assert.That(result.IsUserDefined("STEERING"), Is.False);
        }

        [Test]
        public void Extract_LockThrottle_NotMarkedAsUserDefined()
        {
            var result = _extractor.Extract("LOCK THROTTLE TO 1.");

            Assert.That(result.IsUserDefined("THROTTLE"), Is.False);
        }

        [Test]
        public void Extract_LockWheelSteering_NotMarkedAsUserDefined()
        {
            var result = _extractor.Extract("LOCK WHEELSTEERING TO 0.");

            Assert.That(result.IsUserDefined("WHEELSTEERING"), Is.False);
        }

        [Test]
        public void Extract_LockWheelThrottle_NotMarkedAsUserDefined()
        {
            var result = _extractor.Extract("LOCK WHEELTHROTTLE TO 0.5.");

            Assert.That(result.IsUserDefined("WHEELTHROTTLE"), Is.False);
        }

        [Test]
        public void Extract_DeclareLocalParameter_MarksAsUserDefined()
        {
            var result = _extractor.Extract("DECLARE LOCAL myParam.");

            Assert.That(result.IsUserDefined("myParam"), Is.True);
        }

        [Test]
        public void Extract_DeclareParameter_MarksAsUserDefined()
        {
            var result = _extractor.Extract("DECLARE PARAMETER input.");

            Assert.That(result.IsUserDefined("input"), Is.True);
        }

        #endregion

        #region Comment Handling Tests

        [Test]
        public void Extract_LineComment_IgnoresCommentContent()
        {
            var result = _extractor.Extract("PRINT x. // This is SHIP:VELOCITY");

            // SHIP:VELOCITY should NOT be extracted (it's in a comment)
            Assert.That(result.Identifiers.Any(i => i.Normalized == "SHIP:VELOCITY"), Is.False);
            // x and PRINT should be extracted
            Assert.That(result.Identifiers.Any(i => i.Normalized == "X"), Is.True);
        }

        [Test]
        public void Extract_BlockComment_IgnoresCommentContent()
        {
            var result = _extractor.Extract("PRINT x. /* VESSEL comment SHIP:MAGIC */");

            // Items in block comment should not be extracted
            Assert.That(result.Identifiers.Any(i => i.Normalized == "VESSEL"), Is.False);
            Assert.That(result.Identifiers.Any(i => i.Normalized == "SHIP:MAGIC"), Is.False);
        }

        [Test]
        public void Extract_MultiLineBlockComment_IgnoresAllContent()
        {
            var script = @"PRINT start.
/* This is a
   multi-line comment
   with SHIP:MAGIC inside */
PRINT end.";

            var result = _extractor.Extract(script);

            Assert.That(result.Identifiers.Any(i => i.Normalized == "SHIP:MAGIC"), Is.False);
            Assert.That(result.Identifiers.Any(i => i.Normalized == "START"), Is.True);
            Assert.That(result.Identifiers.Any(i => i.Normalized == "END"), Is.True);
        }

        #endregion

        #region String Literal Tests

        [Test]
        public void Extract_StringLiteral_IgnoresStringContent()
        {
            var result = _extractor.Extract("PRINT \"VESSEL\".");

            // VESSEL inside string should not be extracted as identifier
            // The test should verify the string content is ignored
            // Note: We need to check that the identifier list doesn't have duplicate PRINT
            Assert.That(result.Identifiers.Count(i => i.Normalized == "VESSEL"), Is.EqualTo(0));
        }

        [Test]
        public void Extract_StringWithEscapedQuote_HandlesCorrectly()
        {
            var result = _extractor.Extract("PRINT \"He said \\\"SHIP\\\" here\". SET x TO 1.");

            // SHIP in the string should be ignored
            Assert.That(result.Identifiers.Any(i => i.Normalized == "SHIP"), Is.False);
            // x should be captured as user-defined
            Assert.That(result.IsUserDefined("x"), Is.True);
        }

        [Test]
        public void Extract_EmptyString_HandlesCorrectly()
        {
            var result = _extractor.Extract("PRINT \"\".");

            // PRINT is a keyword and correctly not extracted; test verifies empty string parsing doesn't crash
            Assert.That(result.Identifiers.Any(i => i.Normalized == "PRINT"), Is.False);
        }

        #endregion

        #region API Identifier Tests

        [Test]
        public void GetApiIdentifiers_ExcludesUserDefined()
        {
            var result = _extractor.Extract("SET x TO SHIP:ALTITUDE.");

            var apiIds = result.GetApiIdentifiers().ToList();

            // x is user-defined, should not be in API identifiers
            Assert.That(apiIds.Any(i => i.Normalized == "X"), Is.False);
            // SHIP:ALTITUDE should be in API identifiers
            Assert.That(apiIds.Any(i => i.Normalized == "SHIP:ALTITUDE"), Is.True);
        }

        [Test]
        public void GetApiIdentifiers_ExcludesKeywords()
        {
            var result = _extractor.Extract("SET x TO 5.");

            var apiIds = result.GetApiIdentifiers().ToList();

            // SET and TO are keywords
            Assert.That(apiIds.Any(i => i.Normalized == "SET"), Is.False);
            Assert.That(apiIds.Any(i => i.Normalized == "TO"), Is.False);
        }

        #endregion

        #region Line Number Tests

        [Test]
        public void Extract_TracksLineNumbers()
        {
            var script = @"PRINT line1.
SET x TO SHIP:ALTITUDE.
PRINT line3.";

            var result = _extractor.Extract(script);

            var line1Id = result.Identifiers.FirstOrDefault(i => i.Normalized == "LINE1");
            var shipAlt = result.Identifiers.FirstOrDefault(i => i.Normalized == "SHIP:ALTITUDE");
            var line3Id = result.Identifiers.FirstOrDefault(i => i.Normalized == "LINE3");

            Assert.That(line1Id, Is.Not.Null);
            Assert.That(line1Id.Line, Is.EqualTo(1));

            Assert.That(shipAlt, Is.Not.Null);
            Assert.That(shipAlt.Line, Is.EqualTo(2));

            Assert.That(line3Id, Is.Not.Null);
            Assert.That(line3Id.Line, Is.EqualTo(3));
        }

        #endregion

        #region Normalization Tests

        [Test]
        public void Extract_NormalizesToUppercase()
        {
            var result = _extractor.Extract("print ship:altitude.");

            Assert.That(result.Identifiers.Any(i => i.Normalized == "SHIP:ALTITUDE"), Is.True);
            // PRINT is a keyword and correctly not extracted as an identifier
            Assert.That(result.Identifiers.Any(i => i.Normalized == "PRINT"), Is.False);
        }

        [Test]
        public void Extract_PreservesOriginalCase()
        {
            var result = _extractor.Extract("print Ship:Altitude.");

            var id = result.Identifiers.FirstOrDefault(i => i.Normalized == "SHIP:ALTITUDE");
            Assert.That(id, Is.Not.Null);
            Assert.That(id.Text, Is.EqualTo("Ship:Altitude"));
        }

        #endregion

        #region Complex Script Tests

        [Test]
        public void Extract_ComplexScript_HandlesAllPatterns()
        {
            var script = @"// Launch script
PARAMETER targetAlt.
LOCAL g IS SHIP:BODY:MU / SHIP:BODY:RADIUS^2.
SET myThrottle TO 1.

LOCK STEERING TO HEADING(90, 90).
LOCK THROTTLE TO myThrottle.

FUNCTION calculateTWR {
    RETURN SHIP:MAXTHRUST / (SHIP:MASS * g).
}

UNTIL SHIP:ALTITUDE > targetAlt {
    SET myThrottle TO MAX(0.1, 1 - SHIP:ALTITUDE/targetAlt).
    WAIT 0.1.
}

/* End of script
   SHIP:MAGIC should not appear */
PRINT ""Done"".";

            var result = _extractor.Extract(script);

            // User-defined variables
            Assert.That(result.IsUserDefined("targetAlt"), Is.True);
            Assert.That(result.IsUserDefined("g"), Is.True);
            Assert.That(result.IsUserDefined("myThrottle"), Is.True);
            Assert.That(result.IsUserDefined("calculateTWR"), Is.True);

            // Built-in locks not user-defined
            Assert.That(result.IsUserDefined("STEERING"), Is.False);
            Assert.That(result.IsUserDefined("THROTTLE"), Is.False);

            // Comment content ignored
            Assert.That(result.Identifiers.Any(i => i.Normalized == "SHIP:MAGIC"), Is.False);

            // API identifiers present
            Assert.That(result.Identifiers.Any(i => i.Normalized == "SHIP:ALTITUDE"), Is.True);
            Assert.That(result.Identifiers.Any(i => i.Normalized == "SHIP:BODY:MU"), Is.True);
            Assert.That(result.Identifiers.Any(i => i.Normalized == "SHIP:MAXTHRUST"), Is.True);
            Assert.That(result.Identifiers.Any(i => i.Normalized == "SHIP:MASS"), Is.True);
        }

        #endregion

        #region Boundary Case Tests

        [Test]
        public void Extract_WhitespaceOnlyScript_ReturnsEmptySet()
        {
            // Arrange - only spaces, tabs, newlines
            var script = "   \t  \n  \r\n   \t\t  ";

            // Act
            var result = _extractor.Extract(script);

            // Assert
            Assert.That(result.IsEmpty, Is.True);
        }

        [Test]
        public void Extract_CommentsOnlyScript_ReturnsEmptySet()
        {
            // Arrange - only // and /* */ comments
            var script = @"// This is a comment
/* This is
   a block comment */
// Another line comment";

            // Act
            var result = _extractor.Extract(script);

            // Assert
            Assert.That(result.IsEmpty, Is.True);
        }

        [Test]
        public void Extract_ExtremelyLongIdentifierChain_HandlesCorrectly()
        {
            // Arrange - 10-level chain A:B:C:D:E:F:G:H:I:J
            var script = "PRINT A:B:C:D:E:F:G:H:I:J.";

            // Act
            var result = _extractor.Extract(script);

            // Assert - should extract the full chain
            Assert.That(result.Identifiers.Any(i => i.Normalized == "A:B:C:D:E:F:G:H:I:J"), Is.True);
            // Should also extract individual parts
            Assert.That(result.Identifiers.Any(i => i.Normalized == "A"), Is.True);
            Assert.That(result.Identifiers.Any(i => i.Normalized == "J"), Is.True);
        }

        [Test]
        public void Extract_IdentifierWithUnderscore_ExtractsCorrectly()
        {
            // Arrange
            var script = "SET my_var TO 5.";

            // Act
            var result = _extractor.Extract(script);

            // Assert
            Assert.That(result.Identifiers.Any(i => i.Normalized == "MY_VAR"), Is.True);
            Assert.That(result.IsUserDefined("my_var"), Is.True);
        }

        [Test]
        public void Extract_IdentifierWithNumbers_ExtractsCorrectly()
        {
            // Arrange
            var script = "SET stage2Thrust TO SHIP:MAXTHRUST.";

            // Act
            var result = _extractor.Extract(script);

            // Assert
            Assert.That(result.Identifiers.Any(i => i.Normalized == "STAGE2THRUST"), Is.True);
            Assert.That(result.IsUserDefined("stage2Thrust"), Is.True);
        }

        [Test]
        public void Extract_IdentifierStartingWithUnderscore_ExtractsCorrectly()
        {
            // Arrange
            var script = "SET _private TO 10.";

            // Act
            var result = _extractor.Extract(script);

            // Assert
            Assert.That(result.Identifiers.Any(i => i.Normalized == "_PRIVATE"), Is.True);
            Assert.That(result.IsUserDefined("_private"), Is.True);
        }

        [Test]
        public void Extract_MixedCommentsAndCode_ExtractsOnlyCode()
        {
            // Arrange - code with inline comments
            var script = @"SET x TO 5. // Set x to five
PRINT x. /* print it */
// FAKE:IDENTIFIER should not appear
SET y TO /* HIDDEN:VALUE */ 10.";

            // Act
            var result = _extractor.Extract(script);

            // Assert
            Assert.That(result.Identifiers.Any(i => i.Normalized == "X"), Is.True);
            Assert.That(result.Identifiers.Any(i => i.Normalized == "Y"), Is.True);
            Assert.That(result.Identifiers.Any(i => i.Normalized == "FAKE:IDENTIFIER"), Is.False);
            Assert.That(result.Identifiers.Any(i => i.Normalized == "HIDDEN:VALUE"), Is.False);
        }

        [Test]
        public void Extract_OnlyNumericLiterals_ReturnsEmpty()
        {
            // Arrange - script with just numbers (no identifiers)
            // Note: This tests that numbers don't get extracted as identifiers
            var script = "123 456.789 0 -5 1.5";

            // Act
            var result = _extractor.Extract(script);

            // Assert - numbers should not be extracted as identifiers
            Assert.That(result.Identifiers.Any(i => i.Normalized == "123"), Is.False);
            Assert.That(result.Identifiers.Any(i => i.Normalized == "456"), Is.False);
        }

        [Test]
        public void Extract_ScientificNotation_NotExtractedAsIdentifier()
        {
            // Arrange - 1.5e10 should not be an identifier
            var script = "SET x TO 1.5e10.";

            // Act
            var result = _extractor.Extract(script);

            // Assert
            Assert.That(result.IsUserDefined("x"), Is.True);
            // 1.5e10 should not appear as identifier (it's a number literal)
            Assert.That(result.Identifiers.Any(i => i.Normalized.Contains("1.5E10")), Is.False);
            Assert.That(result.Identifiers.Any(i => i.Normalized == "E10"), Is.False);
        }

        [Test]
        public void Extract_NullScript_ReturnsEmptySet_Verify()
        {
            // This test already exists but we verify the specific behavior
            var result = _extractor.Extract(null);

            Assert.That(result.IsEmpty, Is.True);
            Assert.That(result.Count, Is.EqualTo(0));
            Assert.That(result.Identifiers, Is.Empty);
        }

        #endregion
    }
}
