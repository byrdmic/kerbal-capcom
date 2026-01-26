using System.Collections.Generic;
using NUnit.Framework;
using KSPCapcom.KosDocs;

namespace KSPCapcom.Tests.KosDocs
{
    [TestFixture]
    public class DocEntryTests
    {
        #region ToPromptFormat Tests

        [Test]
        public void ToPromptFormat_Structure_IncludesTypeAndName()
        {
            // Arrange
            var entry = new DocEntry
            {
                Id = "VESSEL",
                Name = "Vessel",
                Type = DocEntryType.Structure,
                Description = "Represents a vessel in the game."
            };

            // Act
            var formatted = entry.ToPromptFormat();

            // Assert
            Assert.That(formatted, Does.Contain("[STRUCTURE]"));
            Assert.That(formatted, Does.Contain("Vessel"));
            Assert.That(formatted, Does.Contain("Represents a vessel"));
        }

        [Test]
        public void ToPromptFormat_Suffix_IncludesParentStructure()
        {
            // Arrange
            var entry = new DocEntry
            {
                Id = "VESSEL:ALTITUDE",
                Name = "ALTITUDE",
                Type = DocEntryType.Suffix,
                ParentStructure = "VESSEL",
                ReturnType = "Scalar",
                Access = DocAccessMode.Get,
                Description = "The altitude above sea level."
            };

            // Act
            var formatted = entry.ToPromptFormat();

            // Assert
            Assert.That(formatted, Does.Contain("[SUFFIX]"));
            Assert.That(formatted, Does.Contain("VESSEL:ALTITUDE"));
            Assert.That(formatted, Does.Contain("Returns: Scalar"));
            Assert.That(formatted, Does.Contain("Access: get"));
        }

        [Test]
        public void ToPromptFormat_Method_IncludesSignature()
        {
            // Arrange
            var entry = new DocEntry
            {
                Id = "FUNCTION:ABS",
                Name = "ABS",
                Type = DocEntryType.Function,
                Signature = "ABS(value)",
                ReturnType = "Scalar",
                Access = DocAccessMode.Method,
                Description = "Returns the absolute value."
            };

            // Act
            var formatted = entry.ToPromptFormat();

            // Assert
            Assert.That(formatted, Does.Contain("Signature: ABS(value)"));
        }

        [Test]
        public void ToPromptFormat_WithSnippet_IncludesExample()
        {
            // Arrange
            var entry = new DocEntry
            {
                Id = "PRINT",
                Name = "PRINT",
                Type = DocEntryType.Command,
                Description = "Outputs text to the terminal.",
                Snippet = "PRINT \"Hello, Kerbal!\"."
            };

            // Act
            var formatted = entry.ToPromptFormat();

            // Assert
            Assert.That(formatted, Does.Contain("Example:"));
            Assert.That(formatted, Does.Contain("PRINT \"Hello, Kerbal!\"."));
        }

        [Test]
        public void ToPromptFormat_Deprecated_IncludesWarning()
        {
            // Arrange
            var entry = new DocEntry
            {
                Id = "OLD_FUNCTION",
                Name = "OLD_FUNCTION",
                Type = DocEntryType.Function,
                Description = "An old function.",
                Deprecated = true,
                DeprecationNote = "Use NEW_FUNCTION instead."
            };

            // Act
            var formatted = entry.ToPromptFormat();

            // Assert
            Assert.That(formatted, Does.Contain("DEPRECATED"));
            Assert.That(formatted, Does.Contain("Use NEW_FUNCTION instead."));
        }

        [Test]
        public void ToPromptFormat_AllAccessModes_FormatCorrectly()
        {
            var testCases = new[]
            {
                (DocAccessMode.Get, "get"),
                (DocAccessMode.Set, "set"),
                (DocAccessMode.GetSet, "get/set"),
                (DocAccessMode.Method, "method")
            };

            foreach (var (access, expected) in testCases)
            {
                // Arrange
                var entry = new DocEntry
                {
                    Id = "TEST",
                    Name = "TEST",
                    Type = DocEntryType.Suffix,
                    Access = access,
                    Description = "Test entry."
                };

                // Act
                var formatted = entry.ToPromptFormat();

                // Assert
                Assert.That(formatted, Does.Contain($"Access: {expected}"),
                    $"Access mode {access} should format as '{expected}'");
            }
        }

        #endregion

        #region ToString Tests

        [Test]
        public void ToString_ReturnsTypeAndId()
        {
            // Arrange
            var entry = new DocEntry
            {
                Id = "VESSEL:ALTITUDE",
                Type = DocEntryType.Suffix
            };

            // Act
            var result = entry.ToString();

            // Assert
            Assert.That(result, Is.EqualTo("Suffix: VESSEL:ALTITUDE"));
        }

        #endregion

        #region Property Tests

        [Test]
        public void DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var entry = new DocEntry();

            // Assert
            Assert.That(entry.Id, Is.Null);
            Assert.That(entry.Name, Is.Null);
            Assert.That(entry.Type, Is.EqualTo(DocEntryType.Structure));
            Assert.That(entry.Access, Is.EqualTo(DocAccessMode.None));
            Assert.That(entry.Deprecated, Is.False);
            Assert.That(entry.Tags, Is.Not.Null);
            Assert.That(entry.Tags, Is.Empty);
            Assert.That(entry.Aliases, Is.Not.Null);
            Assert.That(entry.Aliases, Is.Empty);
            Assert.That(entry.Related, Is.Not.Null);
            Assert.That(entry.Related, Is.Empty);
        }

        [Test]
        public void AllProperties_CanBeSetAndRead()
        {
            // Arrange
            var entry = new DocEntry
            {
                Id = "TEST:ID",
                Name = "TestName",
                Type = DocEntryType.Function,
                ParentStructure = "TestParent",
                ReturnType = "Scalar",
                Access = DocAccessMode.GetSet,
                Signature = "TEST(arg)",
                Description = "Test description",
                Snippet = "TEST CODE",
                SourceRef = "https://example.com",
                Tags = new List<string> { "tag1", "tag2" },
                Aliases = new List<string> { "alias1" },
                Related = new List<string> { "RELATED:ID" },
                Deprecated = true,
                DeprecationNote = "Use something else"
            };

            // Assert
            Assert.That(entry.Id, Is.EqualTo("TEST:ID"));
            Assert.That(entry.Name, Is.EqualTo("TestName"));
            Assert.That(entry.Type, Is.EqualTo(DocEntryType.Function));
            Assert.That(entry.ParentStructure, Is.EqualTo("TestParent"));
            Assert.That(entry.ReturnType, Is.EqualTo("Scalar"));
            Assert.That(entry.Access, Is.EqualTo(DocAccessMode.GetSet));
            Assert.That(entry.Signature, Is.EqualTo("TEST(arg)"));
            Assert.That(entry.Description, Is.EqualTo("Test description"));
            Assert.That(entry.Snippet, Is.EqualTo("TEST CODE"));
            Assert.That(entry.SourceRef, Is.EqualTo("https://example.com"));
            Assert.That(entry.Tags, Has.Count.EqualTo(2));
            Assert.That(entry.Aliases, Has.Count.EqualTo(1));
            Assert.That(entry.Related, Has.Count.EqualTo(1));
            Assert.That(entry.Deprecated, Is.True);
            Assert.That(entry.DeprecationNote, Is.EqualTo("Use something else"));
        }

        #endregion
    }
}
