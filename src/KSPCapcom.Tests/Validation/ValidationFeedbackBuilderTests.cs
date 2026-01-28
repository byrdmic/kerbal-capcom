using System.Collections.Generic;
using NUnit.Framework;
using KSPCapcom.Validation;

namespace KSPCapcom.Tests.Validation
{
    [TestFixture]
    public class ValidationFeedbackBuilderTests
    {
        #region Null/Empty Input Handling

        [Test]
        public void Build_NullValidation_ReturnsEmpty()
        {
            var result = ValidationFeedbackBuilder.Build(null);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void Build_EmptyValidation_ReturnsEmpty()
        {
            var validation = new KosValidationResult();

            var result = ValidationFeedbackBuilder.Build(validation);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void Build_SkippedValidation_ReturnsEmpty()
        {
            var validation = KosValidationResult.Skipped("Test skip reason");

            var result = ValidationFeedbackBuilder.Build(validation);

            Assert.That(result, Is.Empty);
        }

        #endregion

        #region Warning Block Generation

        [Test]
        public void Build_WithUnverifiedIdentifiers_ReturnsWarning()
        {
            var validation = new KosValidationResult();
            AddUnverified(validation, new UnverifiedIdentifier("SHIP:MAGIC", 5, new List<string>()));

            var result = ValidationFeedbackBuilder.Build(validation);

            Assert.That(result, Does.Contain("---"));
            Assert.That(result, Does.Contain("Script Validation Failed"));
            Assert.That(result, Does.Contain("`SHIP:MAGIC`"));
            Assert.That(result, Does.Contain("(line 5)"));
            Assert.That(result, Does.Contain("To fix:"));
        }

        [Test]
        public void Build_WithMultipleUnverified_ListsAll()
        {
            var validation = new KosValidationResult();
            AddUnverified(validation, new UnverifiedIdentifier("SHIP:MAGIC", 5, new List<string>()));
            AddUnverified(validation, new UnverifiedIdentifier("VELOCITY:FAKE", 12, new List<string>()));

            var result = ValidationFeedbackBuilder.Build(validation);

            Assert.That(result, Does.Contain("`SHIP:MAGIC`"));
            Assert.That(result, Does.Contain("(line 5)"));
            Assert.That(result, Does.Contain("`VELOCITY:FAKE`"));
            Assert.That(result, Does.Contain("(line 12)"));
        }

        [Test]
        public void Build_IncludesSuggestionsInWarning()
        {
            var suggestions = new List<string> { "SHIP:MASS", "SHIP:MAXTHRUST" };
            var validation = new KosValidationResult();
            AddUnverified(validation, new UnverifiedIdentifier("SHIP:MAGIC", 5, suggestions));

            var result = ValidationFeedbackBuilder.Build(validation);

            Assert.That(result, Does.Contain("did you mean:"));
            Assert.That(result, Does.Contain("SHIP:MASS"));
            Assert.That(result, Does.Contain("SHIP:MAXTHRUST"));
        }

        [Test]
        public void Build_TruncatesSuggestionsToMax()
        {
            var suggestions = new List<string> { "SHIP:MASS", "SHIP:MAXTHRUST", "SHIP:MAXAIR", "SHIP:MAINTHROTTLE", "SHIP:MODULESNAMED" };
            var validation = new KosValidationResult();
            AddUnverified(validation, new UnverifiedIdentifier("SHIP:MAGIC", 5, suggestions));

            var result = ValidationFeedbackBuilder.Build(validation);

            // Should only show first 3 suggestions (MaxSuggestionsPerIdentifier)
            Assert.That(result, Does.Contain("SHIP:MASS"));
            Assert.That(result, Does.Contain("SHIP:MAXTHRUST"));
            Assert.That(result, Does.Contain("SHIP:MAXAIR"));
            Assert.That(result, Does.Not.Contain("SHIP:MAINTHROTTLE"));
            Assert.That(result, Does.Not.Contain("SHIP:MODULESNAMED"));
        }

        [Test]
        public void Build_TruncatesUnverifiedListWhenOverMax()
        {
            var validation = new KosValidationResult();

            // Add more than MaxUnverifiedToShow (10) items
            for (int i = 1; i <= 15; i++)
            {
                AddUnverified(validation, new UnverifiedIdentifier($"FAKE:ITEM{i}", i, new List<string>()));
            }

            var result = ValidationFeedbackBuilder.Build(validation);

            // Should show first 10 and a "...and N more" message
            Assert.That(result, Does.Contain("`FAKE:ITEM1`"));
            Assert.That(result, Does.Contain("`FAKE:ITEM10`"));
            Assert.That(result, Does.Not.Contain("`FAKE:ITEM11`"));
            Assert.That(result, Does.Contain("...and 5 more"));
        }

        #endregion

        #region Success Footer Generation

        [Test]
        public void Build_WithOnlyVerified_ReturnsSuccessFooter()
        {
            var validation = new KosValidationResult();
            AddVerified(validation, new VerifiedIdentifier("SHIP:ALTITUDE", "VESSEL:ALTITUDE", null));

            var result = ValidationFeedbackBuilder.Build(validation);

            Assert.That(result, Does.Contain("---"));
            Assert.That(result, Does.Contain("**Grounded**"));
            Assert.That(result, Does.Contain("All kOS identifiers verified"));
        }

        [Test]
        public void Build_WithMultipleVerified_ReturnsSuccessFooter()
        {
            var validation = new KosValidationResult();
            AddVerified(validation, new VerifiedIdentifier("SHIP:ALTITUDE", "VESSEL:ALTITUDE", null));
            AddVerified(validation, new VerifiedIdentifier("SHIP:VELOCITY", "VESSEL:VELOCITY", null));
            AddVerified(validation, new VerifiedIdentifier("THROTTLE", "THROTTLE", null));

            var result = ValidationFeedbackBuilder.Build(validation);

            Assert.That(result, Does.Contain("**Grounded**"));
        }

        #endregion

        #region Mixed Results (Verified + Unverified)

        [Test]
        public void Build_WithMixedResults_ReturnsWarning()
        {
            // When there are both verified and unverified, warning takes precedence
            var validation = new KosValidationResult();
            AddVerified(validation, new VerifiedIdentifier("SHIP:ALTITUDE", "VESSEL:ALTITUDE", null));
            AddUnverified(validation, new UnverifiedIdentifier("SHIP:MAGIC", 5, new List<string>()));

            var result = ValidationFeedbackBuilder.Build(validation);

            // Should show warning, not success
            Assert.That(result, Does.Contain("Script Validation Failed"));
            Assert.That(result, Does.Not.Contain("All kOS identifiers verified"));
        }

        #endregion

        #region Format Verification

        [Test]
        public void Build_Warning_StartsWithHorizontalRule()
        {
            var validation = new KosValidationResult();
            AddUnverified(validation, new UnverifiedIdentifier("FAKE:ID", 1, new List<string>()));

            var result = ValidationFeedbackBuilder.Build(validation);

            Assert.That(result, Does.StartWith("---"));
        }

        [Test]
        public void Build_Success_StartsWithHorizontalRule()
        {
            var validation = new KosValidationResult();
            AddVerified(validation, new VerifiedIdentifier("SHIP:ALTITUDE", "VESSEL:ALTITUDE", null));

            var result = ValidationFeedbackBuilder.Build(validation);

            Assert.That(result, Does.StartWith("---"));
        }

        [Test]
        public void Build_Warning_UsesMarkdownBold()
        {
            var validation = new KosValidationResult();
            AddUnverified(validation, new UnverifiedIdentifier("FAKE:ID", 1, new List<string>()));

            var result = ValidationFeedbackBuilder.Build(validation);

            Assert.That(result, Does.Contain("**Script Validation Failed**"));
        }

        [Test]
        public void Build_Warning_UsesBackticksForIdentifiers()
        {
            var validation = new KosValidationResult();
            AddUnverified(validation, new UnverifiedIdentifier("SHIP:MAGIC", 1, new List<string>()));

            var result = ValidationFeedbackBuilder.Build(validation);

            Assert.That(result, Does.Contain("`SHIP:MAGIC`"));
        }

        #endregion

        #region Edge Case Feedback Tests

        [Test]
        public void Build_NoDocumentationWarning_ReturnsEmpty()
        {
            // Arrange - validation with warning but no unverified items
            var validation = new KosValidationResult();
            SetWarning(validation, "No documentation was retrieved.");

            // Act
            var result = ValidationFeedbackBuilder.Build(validation);

            // Assert - warning state with no unverified items returns empty
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void Build_VeryLongIdentifierName_HandlesCorrectly()
        {
            // Arrange - 100+ character identifier
            var longIdentifier = "SHIP:" + new string('A', 100);
            var validation = new KosValidationResult();
            AddUnverified(validation, new UnverifiedIdentifier(longIdentifier, 1, new List<string>()));

            // Act
            var result = ValidationFeedbackBuilder.Build(validation);

            // Assert - should handle without error and include the identifier
            Assert.That(result, Does.Contain(longIdentifier));
            Assert.That(result, Does.Contain("Script Validation Failed"));
        }

        [Test]
        public void Build_LongSuggestionNames_IncludesAll()
        {
            // Arrange - long suggestion names
            var suggestions = new List<string>
            {
                "VERYLONGSTRUCTURENAME:VERYLONGSUFFIXNAME",
                "ANOTHERLONGNAME:WITHLONGSUFFIX",
                "THIRDLONGIDENTIFIER:NAME"
            };
            var validation = new KosValidationResult();
            AddUnverified(validation, new UnverifiedIdentifier("FAKE:ID", 1, suggestions));

            // Act
            var result = ValidationFeedbackBuilder.Build(validation);

            // Assert - all suggestions should be included
            Assert.That(result, Does.Contain("VERYLONGSTRUCTURENAME:VERYLONGSUFFIXNAME"));
            Assert.That(result, Does.Contain("ANOTHERLONGNAME:WITHLONGSUFFIX"));
            Assert.That(result, Does.Contain("THIRDLONGIDENTIFIER:NAME"));
        }

        [Test]
        public void Build_ExactlyMaxUnverified_ShowsAllNoTruncation()
        {
            // Arrange - exactly 10 items (MaxUnverifiedToShow)
            var validation = new KosValidationResult();
            for (int i = 1; i <= 10; i++)
            {
                AddUnverified(validation, new UnverifiedIdentifier($"FAKE:ITEM{i}", i, new List<string>()));
            }

            // Act
            var result = ValidationFeedbackBuilder.Build(validation);

            // Assert - all 10 should be shown, no truncation message
            for (int i = 1; i <= 10; i++)
            {
                Assert.That(result, Does.Contain($"`FAKE:ITEM{i}`"));
            }
            Assert.That(result, Does.Not.Contain("...and"));
            Assert.That(result, Does.Not.Contain("more"));
        }

        [Test]
        public void Build_OneOverMaxUnverified_ShowsTruncation()
        {
            // Arrange - exactly 11 items (one over max)
            var validation = new KosValidationResult();
            for (int i = 1; i <= 11; i++)
            {
                AddUnverified(validation, new UnverifiedIdentifier($"FAKE:ITEM{i}", i, new List<string>()));
            }

            // Act
            var result = ValidationFeedbackBuilder.Build(validation);

            // Assert - first 10 shown, item 11 hidden, "...and 1 more" message
            for (int i = 1; i <= 10; i++)
            {
                Assert.That(result, Does.Contain($"`FAKE:ITEM{i}`"));
            }
            Assert.That(result, Does.Not.Contain("`FAKE:ITEM11`"));
            Assert.That(result, Does.Contain("...and 1 more"));
        }

        #endregion

        #region Helper Methods

        private static void SetWarning(KosValidationResult result, string warning)
        {
            var property = typeof(KosValidationResult).GetProperty("Warning",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            property?.SetValue(result, warning);
        }

        private static void AddVerified(KosValidationResult result, VerifiedIdentifier verified)
        {
            var method = typeof(KosValidationResult).GetMethod("AddVerified",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(result, new object[] { verified });
        }

        private static void AddUnverified(KosValidationResult result, UnverifiedIdentifier unverified)
        {
            var method = typeof(KosValidationResult).GetMethod("AddUnverified",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(result, new object[] { unverified });
        }

        #endregion
    }
}
