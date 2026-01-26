using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KSPCapcom.KosDocs;

namespace KSPCapcom.Tests.KosDocs
{
    [TestFixture]
    public class KosDocIndexTests
    {
        private KosDocIndex _index;

        [SetUp]
        public void SetUp()
        {
            _index = new KosDocIndex();
        }

        #region AddEntry Tests

        [Test]
        public void AddEntry_ValidEntry_IncreasesCount()
        {
            // Arrange
            var entry = CreateTestEntry("VESSEL");

            // Act
            _index.AddEntry(entry);

            // Assert
            Assert.That(_index.Count, Is.EqualTo(1));
        }

        [Test]
        public void AddEntry_NullEntry_DoesNotAdd()
        {
            // Act
            _index.AddEntry(null);

            // Assert
            Assert.That(_index.Count, Is.EqualTo(0));
        }

        [Test]
        public void AddEntry_EntryWithNullId_DoesNotAdd()
        {
            // Arrange
            var entry = new DocEntry { Name = "Test" };

            // Act
            _index.AddEntry(entry);

            // Assert
            Assert.That(_index.Count, Is.EqualTo(0));
        }

        [Test]
        public void AddEntry_EntryWithEmptyId_DoesNotAdd()
        {
            // Arrange
            var entry = new DocEntry { Id = "", Name = "Test" };

            // Act
            _index.AddEntry(entry);

            // Assert
            Assert.That(_index.Count, Is.EqualTo(0));
        }

        #endregion

        #region GetById Tests

        [Test]
        public void GetById_ExistingEntry_ReturnsEntry()
        {
            // Arrange
            var entry = CreateTestEntry("VESSEL");
            _index.AddEntry(entry);

            // Act
            var result = _index.GetById("VESSEL");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo("VESSEL"));
        }

        [Test]
        public void GetById_CaseInsensitive_ReturnsEntry()
        {
            // Arrange
            var entry = CreateTestEntry("VESSEL");
            _index.AddEntry(entry);

            // Act
            var result = _index.GetById("vessel");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo("VESSEL"));
        }

        [Test]
        public void GetById_NonExistingEntry_ReturnsNull()
        {
            // Arrange
            var entry = CreateTestEntry("VESSEL");
            _index.AddEntry(entry);

            // Act
            var result = _index.GetById("NONEXISTENT");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetById_NullOrEmptyId_ReturnsNull()
        {
            Assert.That(_index.GetById(null), Is.Null);
            Assert.That(_index.GetById(""), Is.Null);
        }

        #endregion

        #region GetByIdOrAlias Tests

        [Test]
        public void GetByIdOrAlias_ByAlias_ReturnsEntry()
        {
            // Arrange
            var entry = CreateTestEntry("VESSEL");
            entry.Aliases = new List<string> { "SHIP" };
            _index.AddEntry(entry);

            // Act
            var result = _index.GetByIdOrAlias("SHIP");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo("VESSEL"));
        }

        [Test]
        public void GetByIdOrAlias_ById_ReturnsEntry()
        {
            // Arrange
            var entry = CreateTestEntry("VESSEL");
            entry.Aliases = new List<string> { "SHIP" };
            _index.AddEntry(entry);

            // Act
            var result = _index.GetByIdOrAlias("VESSEL");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo("VESSEL"));
        }

        [Test]
        public void GetByIdOrAlias_AliasCaseInsensitive_ReturnsEntry()
        {
            // Arrange
            var entry = CreateTestEntry("VESSEL");
            entry.Aliases = new List<string> { "SHIP" };
            _index.AddEntry(entry);

            // Act
            var result = _index.GetByIdOrAlias("ship");

            // Assert
            Assert.That(result, Is.Not.Null);
        }

        #endregion

        #region GetByParent Tests

        [Test]
        public void GetByParent_WithSuffixes_ReturnsSuffixes()
        {
            // Arrange
            var vessel = CreateTestEntry("VESSEL", DocEntryType.Structure);
            var altitude = CreateTestEntry("VESSEL:ALTITUDE", DocEntryType.Suffix);
            altitude.ParentStructure = "VESSEL";
            var apoapsis = CreateTestEntry("VESSEL:APOAPSIS", DocEntryType.Suffix);
            apoapsis.ParentStructure = "VESSEL";

            _index.AddEntry(vessel);
            _index.AddEntry(altitude);
            _index.AddEntry(apoapsis);

            // Act
            var result = _index.GetByParent("VESSEL");

            // Assert
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result.Select(e => e.Id), Does.Contain("VESSEL:ALTITUDE"));
            Assert.That(result.Select(e => e.Id), Does.Contain("VESSEL:APOAPSIS"));
        }

        [Test]
        public void GetByParent_CaseInsensitive_ReturnsSuffixes()
        {
            // Arrange
            var suffix = CreateTestEntry("VESSEL:ALTITUDE", DocEntryType.Suffix);
            suffix.ParentStructure = "VESSEL";
            _index.AddEntry(suffix);

            // Act
            var result = _index.GetByParent("vessel");

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
        }

        [Test]
        public void GetByParent_NoMatch_ReturnsEmptyList()
        {
            // Act
            var result = _index.GetByParent("NONEXISTENT");

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void GetByParent_NullOrEmpty_ReturnsEmptyList()
        {
            Assert.That(_index.GetByParent(null), Is.Empty);
            Assert.That(_index.GetByParent(""), Is.Empty);
        }

        #endregion

        #region GetByTag Tests

        [Test]
        public void GetByTag_WithMatchingEntries_ReturnsEntries()
        {
            // Arrange
            var entry1 = CreateTestEntry("VESSEL");
            entry1.Tags = new List<string> { "vessel", "core" };
            var entry2 = CreateTestEntry("VESSEL:ALTITUDE");
            entry2.Tags = new List<string> { "vessel", "position" };

            _index.AddEntry(entry1);
            _index.AddEntry(entry2);

            // Act
            var result = _index.GetByTag("vessel");

            // Assert
            Assert.That(result, Has.Count.EqualTo(2));
        }

        [Test]
        public void GetByTag_CaseInsensitive_ReturnsEntries()
        {
            // Arrange
            var entry = CreateTestEntry("VESSEL");
            entry.Tags = new List<string> { "vessel" };
            _index.AddEntry(entry);

            // Act
            var result = _index.GetByTag("VESSEL");

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
        }

        [Test]
        public void GetByTag_NoMatch_ReturnsEmptyList()
        {
            // Act
            var result = _index.GetByTag("nonexistent");

            // Assert
            Assert.That(result, Is.Empty);
        }

        #endregion

        #region Search Tests

        [Test]
        public void Search_ExactIdMatch_ReturnsFirstWithHighestScore()
        {
            // Arrange
            var vessel = CreateTestEntry("VESSEL");
            var vesselAlt = CreateTestEntry("VESSEL:ALTITUDE");
            _index.AddEntry(vessel);
            _index.AddEntry(vesselAlt);

            // Act
            var result = _index.Search("VESSEL", 10);

            // Assert
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].Id, Is.EqualTo("VESSEL")); // Exact match should be first
        }

        [Test]
        public void Search_PartialMatch_ReturnsContainingEntries()
        {
            // Arrange
            var altitude = CreateTestEntry("VESSEL:ALTITUDE");
            altitude.Description = "The altitude above sea level.";
            _index.AddEntry(altitude);

            // Act
            var result = _index.Search("altitude");

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
        }

        [Test]
        public void Search_DescriptionMatch_ReturnsEntry()
        {
            // Arrange
            var entry = CreateTestEntry("STAGE");
            entry.Description = "Activates the next stage.";
            _index.AddEntry(entry);

            // Act
            var result = _index.Search("activates");

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
        }

        [Test]
        public void Search_RespectsMaxResults()
        {
            // Arrange
            for (int i = 0; i < 20; i++)
            {
                _index.AddEntry(CreateTestEntry($"TEST_{i}"));
            }

            // Act
            var result = _index.Search("TEST", 5);

            // Assert
            Assert.That(result, Has.Count.EqualTo(5));
        }

        [Test]
        public void Search_EmptyQuery_ReturnsEmptyList()
        {
            // Arrange
            _index.AddEntry(CreateTestEntry("VESSEL"));

            // Act & Assert
            Assert.That(_index.Search(""), Is.Empty);
            Assert.That(_index.Search(null), Is.Empty);
        }

        #endregion

        #region SearchWithFilters Tests

        [Test]
        public void SearchWithFilters_ByType_ReturnsMatchingType()
        {
            // Arrange
            var structure = CreateTestEntry("VESSEL", DocEntryType.Structure);
            var suffix = CreateTestEntry("VESSEL:ALTITUDE", DocEntryType.Suffix);
            _index.AddEntry(structure);
            _index.AddEntry(suffix);

            // Act
            var result = _index.SearchWithFilters(type: DocEntryType.Structure);

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Type, Is.EqualTo(DocEntryType.Structure));
        }

        [Test]
        public void SearchWithFilters_ByTag_ReturnsMatchingTag()
        {
            // Arrange
            var entry1 = CreateTestEntry("VESSEL");
            entry1.Tags = new List<string> { "vessel" };
            var entry2 = CreateTestEntry("ABS");
            entry2.Tags = new List<string> { "math" };
            _index.AddEntry(entry1);
            _index.AddEntry(entry2);

            // Act
            var result = _index.SearchWithFilters(tag: "vessel");

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Id, Is.EqualTo("VESSEL"));
        }

        [Test]
        public void SearchWithFilters_Combined_ReturnsMatchingAll()
        {
            // Arrange
            var entry1 = CreateTestEntry("VESSEL", DocEntryType.Structure);
            entry1.Tags = new List<string> { "vessel" };
            var entry2 = CreateTestEntry("VESSEL:ALTITUDE", DocEntryType.Suffix);
            entry2.Tags = new List<string> { "vessel" };
            _index.AddEntry(entry1);
            _index.AddEntry(entry2);

            // Act
            var result = _index.SearchWithFilters(
                query: "altitude",
                type: DocEntryType.Suffix,
                tag: "vessel");

            // Assert
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Id, Is.EqualTo("VESSEL:ALTITUDE"));
        }

        #endregion

        #region Utility Method Tests

        [Test]
        public void GetAllTags_ReturnsAllUniqueTags()
        {
            // Arrange
            var entry1 = CreateTestEntry("VESSEL");
            entry1.Tags = new List<string> { "vessel", "core" };
            var entry2 = CreateTestEntry("ABS");
            entry2.Tags = new List<string> { "math", "function" };
            _index.AddEntry(entry1);
            _index.AddEntry(entry2);

            // Act
            var tags = _index.GetAllTags().ToList();

            // Assert
            Assert.That(tags, Has.Count.EqualTo(4));
            Assert.That(tags, Does.Contain("vessel"));
            Assert.That(tags, Does.Contain("core"));
            Assert.That(tags, Does.Contain("math"));
            Assert.That(tags, Does.Contain("function"));
        }

        [Test]
        public void GetAllParents_ReturnsAllUniqueParents()
        {
            // Arrange
            var suffix1 = CreateTestEntry("VESSEL:ALTITUDE", DocEntryType.Suffix);
            suffix1.ParentStructure = "VESSEL";
            var suffix2 = CreateTestEntry("BODY:RADIUS", DocEntryType.Suffix);
            suffix2.ParentStructure = "BODY";
            _index.AddEntry(suffix1);
            _index.AddEntry(suffix2);

            // Act
            var parents = _index.GetAllParents().ToList();

            // Assert
            Assert.That(parents, Has.Count.EqualTo(2));
            Assert.That(parents, Does.Contain("VESSEL"));
            Assert.That(parents, Does.Contain("BODY"));
        }

        [Test]
        public void Clear_RemovesAllEntries()
        {
            // Arrange
            _index.AddEntry(CreateTestEntry("VESSEL"));
            _index.AddEntry(CreateTestEntry("BODY"));

            // Act
            _index.Clear();

            // Assert
            Assert.That(_index.Count, Is.EqualTo(0));
            Assert.That(_index.IsLoaded, Is.False);
            Assert.That(_index.GetById("VESSEL"), Is.Null);
        }

        [Test]
        public void IsLoaded_EmptyIndex_ReturnsFalse()
        {
            Assert.That(_index.IsLoaded, Is.False);
        }

        [Test]
        public void IsLoaded_WithEntries_ReturnsTrue()
        {
            // Arrange
            _index.AddEntry(CreateTestEntry("VESSEL"));

            // Assert
            Assert.That(_index.IsLoaded, Is.True);
        }

        [Test]
        public void Entries_ReturnsReadOnlyList()
        {
            // Arrange
            _index.AddEntry(CreateTestEntry("VESSEL"));
            _index.AddEntry(CreateTestEntry("BODY"));

            // Act
            var entries = _index.Entries;

            // Assert
            Assert.That(entries, Has.Count.EqualTo(2));
        }

        #endregion

        #region Helper Methods

        private DocEntry CreateTestEntry(string id, DocEntryType type = DocEntryType.Structure)
        {
            return new DocEntry
            {
                Id = id,
                Name = id,
                Type = type,
                Description = $"Test entry for {id}"
            };
        }

        #endregion
    }
}
