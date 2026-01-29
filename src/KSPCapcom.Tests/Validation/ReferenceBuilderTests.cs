using System.Collections.Generic;
using NUnit.Framework;
using KSPCapcom.KosDocs;
using KSPCapcom.Validation;

namespace KSPCapcom.Tests.Validation
{
    [TestFixture]
    public class ReferenceBuilderTests
    {
        #region Basic Reference Generation

        [Test]
        public void Build_WithVerifiedIdentifiers_GeneratesReferences()
        {
            // Arrange
            var validation = new KosValidationResult();
            var docEntry = CreateDocEntry("VESSEL:ALTITUDE", "ALTITUDE", "Gets vessel altitude",
                "https://ksp-kos.github.io/KOS/structures/vessels/vessel.html#ALTITUDE");

            var verified = new VerifiedIdentifier("VESSEL:ALTITUDE", "VESSEL:ALTITUDE", docEntry.SourceRef);
            verified.SourceDoc = docEntry;
            AddVerified(validation, verified);

            var docTracker = new DocEntryTracker();
            docTracker.Add(docEntry);

            // Act
            var result = ReferenceBuilder.Build(validation, docTracker);

            // Assert
            Assert.That(result, Does.Contain("## References"));
            Assert.That(result, Does.Contain("`VESSEL:ALTITUDE`"));
            Assert.That(result, Does.Contain("Gets vessel altitude"));
            Assert.That(result, Does.Contain("[docs]"));
        }

        [Test]
        public void Build_WithMultipleVerifiedIdentifiers_DeduplicatesByDocId()
        {
            // Arrange
            var validation = new KosValidationResult();
            var docEntry = CreateDocEntry("VESSEL:ALTITUDE", "ALTITUDE", "Gets vessel altitude",
                "https://ksp-kos.github.io/KOS/altitude.html");

            // Add same doc entry twice with different identifier text
            var verified1 = new VerifiedIdentifier("SHIP:ALTITUDE", "VESSEL:ALTITUDE", docEntry.SourceRef);
            verified1.SourceDoc = docEntry;
            AddVerified(validation, verified1);

            var verified2 = new VerifiedIdentifier("VESSEL:ALTITUDE", "VESSEL:ALTITUDE", docEntry.SourceRef);
            verified2.SourceDoc = docEntry;
            AddVerified(validation, verified2);

            var docTracker = new DocEntryTracker();
            docTracker.Add(docEntry);

            // Act
            var result = ReferenceBuilder.Build(validation, docTracker);

            // Assert - should only have one reference, not two
            var altitudeCount = System.Text.RegularExpressions.Regex.Matches(result, "ALTITUDE").Count;
            Assert.That(altitudeCount, Is.EqualTo(1), "Should deduplicate references by doc ID");
        }

        #endregion

        #region Null/Empty Input Handling

        [Test]
        public void Build_NullValidation_ReturnsEmpty()
        {
            var docTracker = new DocEntryTracker();

            var result = ReferenceBuilder.Build(null, docTracker);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void Build_EmptyVerifiedList_ReturnsEmpty()
        {
            var validation = new KosValidationResult();
            var docTracker = new DocEntryTracker();

            var result = ReferenceBuilder.Build(validation, docTracker);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void Build_NullDocTracker_StillWorks()
        {
            // Arrange
            var validation = new KosValidationResult();
            var verified = new VerifiedIdentifier("SHIP:ALTITUDE", "VESSEL:ALTITUDE", "https://example.com");
            AddVerified(validation, verified);

            // Act
            var result = ReferenceBuilder.Build(validation, null);

            // Assert - should still generate reference
            Assert.That(result, Does.Contain("`SHIP:ALTITUDE`"));
        }

        #endregion

        #region Description Truncation

        [Test]
        public void Build_LongDescription_Truncates()
        {
            // Arrange
            var longDescription = new string('A', 100); // Way over the 60 char limit
            var validation = new KosValidationResult();
            var docEntry = CreateDocEntry("VESSEL:TEST", "TEST", longDescription, null);

            var verified = new VerifiedIdentifier("VESSEL:TEST", "VESSEL:TEST", null);
            verified.SourceDoc = docEntry;
            AddVerified(validation, verified);

            var docTracker = new DocEntryTracker();
            docTracker.Add(docEntry);

            // Act
            var result = ReferenceBuilder.Build(validation, docTracker);

            // Assert - should have ellipsis and be truncated
            Assert.That(result, Does.Contain("..."));
            Assert.That(result.Length, Is.LessThan(200)); // Much shorter than 100 char description
        }

        [Test]
        public void Build_ShortDescription_NotTruncated()
        {
            // Arrange
            var shortDescription = "Short desc";
            var validation = new KosValidationResult();
            var docEntry = CreateDocEntry("VESSEL:TEST", "TEST", shortDescription, null);

            var verified = new VerifiedIdentifier("VESSEL:TEST", "VESSEL:TEST", null);
            verified.SourceDoc = docEntry;
            AddVerified(validation, verified);

            var docTracker = new DocEntryTracker();
            docTracker.Add(docEntry);

            // Act
            var result = ReferenceBuilder.Build(validation, docTracker);

            // Assert - description should be intact
            Assert.That(result, Does.Contain("Short desc"));
            Assert.That(result, Does.Not.Contain("..."));
        }

        #endregion

        #region Source Reference Handling

        [Test]
        public void Build_WithSourceUrl_IncludesLink()
        {
            // Arrange
            var validation = new KosValidationResult();
            var docEntry = CreateDocEntry("VESSEL:ALTITUDE", "ALTITUDE", "Desc",
                "https://ksp-kos.github.io/KOS/structures/vessels/vessel.html");

            var verified = new VerifiedIdentifier("VESSEL:ALTITUDE", "VESSEL:ALTITUDE", docEntry.SourceRef);
            verified.SourceDoc = docEntry;
            AddVerified(validation, verified);

            var docTracker = new DocEntryTracker();
            docTracker.Add(docEntry);

            // Act
            var result = ReferenceBuilder.Build(validation, docTracker);

            // Assert
            Assert.That(result, Does.Contain("[docs](https://ksp-kos.github.io/KOS/structures/vessels/vessel.html)"));
        }

        [Test]
        public void Build_WithoutSourceUrl_ShowsLocalDocs()
        {
            // Arrange
            var validation = new KosValidationResult();
            var docEntry = CreateDocEntry("VESSEL:ALTITUDE", "ALTITUDE", "Desc", null);

            var verified = new VerifiedIdentifier("VESSEL:ALTITUDE", "VESSEL:ALTITUDE", null);
            verified.SourceDoc = docEntry;
            AddVerified(validation, verified);

            var docTracker = new DocEntryTracker();
            docTracker.Add(docEntry);

            // Act
            var result = ReferenceBuilder.Build(validation, docTracker);

            // Assert
            Assert.That(result, Does.Contain("(local docs)"));
        }

        #endregion

        #region Edge Cases

        [Test]
        public void Build_UnverifiedWithNoVerified_ShowsWarning()
        {
            // Arrange
            var validation = new KosValidationResult();
            AddUnverified(validation, new UnverifiedIdentifier("SHIP:MAGIC", 1, new List<string>()));

            var docTracker = new DocEntryTracker();

            // Act
            var result = ReferenceBuilder.Build(validation, docTracker);

            // Assert
            Assert.That(result, Does.Contain("## References"));
            Assert.That(result, Does.Contain("No documentation references available"));
        }

        [Test]
        public void Build_VerifiedWithoutSourceDoc_UsesDocTracker()
        {
            // Arrange
            var validation = new KosValidationResult();
            var verified = new VerifiedIdentifier("SHIP:ALTITUDE", "VESSEL:ALTITUDE", null);
            // SourceDoc is NOT set
            AddVerified(validation, verified);

            var docEntry = CreateDocEntry("VESSEL:ALTITUDE", "ALTITUDE", "From tracker", "https://example.com");
            var docTracker = new DocEntryTracker();
            docTracker.Add(docEntry);

            // Act
            var result = ReferenceBuilder.Build(validation, docTracker);

            // Assert - should find doc via tracker
            Assert.That(result, Does.Contain("From tracker"));
        }

        #endregion

        #region ParseReferencesSection Tests

        [Test]
        public void ParseReferencesSection_WithReferencesSection_ExtractsSection()
        {
            // Arrange
            var messageText = @"Here is some code:
```kos
PRINT ""Hello"".
```

## References

- `PRINT` - Outputs text ([docs](https://example.com))
- `SHIP` - The vessel object ([docs](https://example.com))";

            // Act
            var result = ReferenceBuilder.ParseReferencesSection(messageText, out var textWithoutRefs);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("## References"));
            Assert.That(result, Does.Contain("`PRINT`"));
            Assert.That(result, Does.Contain("`SHIP`"));
            Assert.That(textWithoutRefs, Does.Not.Contain("## References"));
            Assert.That(textWithoutRefs, Does.Contain("Here is some code"));
        }

        [Test]
        public void ParseReferencesSection_NoReferencesSection_ReturnsNull()
        {
            // Arrange
            var messageText = @"Here is some code:
```kos
PRINT ""Hello"".
```

That's all!";

            // Act
            var result = ReferenceBuilder.ParseReferencesSection(messageText, out var textWithoutRefs);

            // Assert
            Assert.That(result, Is.Null);
            Assert.That(textWithoutRefs, Is.EqualTo(messageText));
        }

        [Test]
        public void ParseReferencesSection_EmptyText_ReturnsNull()
        {
            var result = ReferenceBuilder.ParseReferencesSection("", out var textWithoutRefs);

            Assert.That(result, Is.Null);
            Assert.That(textWithoutRefs, Is.Empty);
        }

        [Test]
        public void ParseReferencesSection_NullText_ReturnsNull()
        {
            var result = ReferenceBuilder.ParseReferencesSection(null, out var textWithoutRefs);

            Assert.That(result, Is.Null);
            Assert.That(textWithoutRefs, Is.Null);
        }

        [Test]
        public void ParseReferencesSection_ReferencesAtEnd_ExtractsToEndOfMessage()
        {
            // Arrange
            var messageText = @"Introduction text.

## References

- `FOO` - Description";

            // Act
            var result = ReferenceBuilder.ParseReferencesSection(messageText, out var textWithoutRefs);

            // Assert
            Assert.That(result, Does.Contain("`FOO`"));
            Assert.That(textWithoutRefs.Trim(), Is.EqualTo("Introduction text."));
        }

        [Test]
        public void ParseReferencesSection_CaseInsensitive_MatchesHeader()
        {
            // Arrange
            var messageText = @"Text

## REFERENCES

- `BAR` - Something";

            // Act
            var result = ReferenceBuilder.ParseReferencesSection(messageText, out var textWithoutRefs);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Contain("`BAR`"));
        }

        #endregion

        #region CountReferences Tests

        [Test]
        public void CountReferences_MultipleReferences_ReturnsCorrectCount()
        {
            var referencesSection = @"## References

- `PRINT` - Outputs text
- `SHIP` - The vessel
- `ALTITUDE` - Height above terrain";

            var count = ReferenceBuilder.CountReferences(referencesSection);

            Assert.That(count, Is.EqualTo(3));
        }

        [Test]
        public void CountReferences_SingleReference_ReturnsOne()
        {
            var referencesSection = @"## References

- `PRINT` - Outputs text";

            var count = ReferenceBuilder.CountReferences(referencesSection);

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void CountReferences_NoReferences_ReturnsZero()
        {
            var referencesSection = @"## References

_No documentation references available._";

            var count = ReferenceBuilder.CountReferences(referencesSection);

            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void CountReferences_EmptyString_ReturnsZero()
        {
            var count = ReferenceBuilder.CountReferences("");
            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void CountReferences_NullString_ReturnsZero()
        {
            var count = ReferenceBuilder.CountReferences(null);
            Assert.That(count, Is.EqualTo(0));
        }

        #endregion

        #region Helper Methods

        private static DocEntry CreateDocEntry(string id, string name, string description, string sourceRef)
        {
            return new DocEntry
            {
                Id = id,
                Name = name,
                Description = description,
                SourceRef = sourceRef,
                Type = DocEntryType.Suffix
            };
        }

        private static void AddVerified(KosValidationResult result, VerifiedIdentifier verified)
        {
            // Use reflection to call internal method
            var method = typeof(KosValidationResult).GetMethod("AddVerified",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(result, new object[] { verified });
        }

        private static void AddUnverified(KosValidationResult result, UnverifiedIdentifier unverified)
        {
            // Use reflection to call internal method
            var method = typeof(KosValidationResult).GetMethod("AddUnverified",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(result, new object[] { unverified });
        }

        #endregion
    }
}
