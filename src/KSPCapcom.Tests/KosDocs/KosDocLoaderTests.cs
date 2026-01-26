using System.IO;
using NUnit.Framework;
using KSPCapcom.KosDocs;

namespace KSPCapcom.Tests.KosDocs
{
    [TestFixture]
    public class KosDocLoaderTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"KosDocLoaderTests_{System.Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        #region LoadSync Tests

        [Test]
        public void LoadSync_ValidJson_LoadsEntries()
        {
            // Arrange
            var json = CreateMinimalValidJson();
            var filePath = Path.Combine(_tempDir, "kos_docs.json");
            File.WriteAllText(filePath, json);
            var loader = new KosDocLoader(filePath);

            // Act
            var success = loader.LoadSync();

            // Assert
            Assert.That(success, Is.True);
            Assert.That(loader.IsReady, Is.True);
            Assert.That(loader.Index, Is.Not.Null);
            Assert.That(loader.Index.Count, Is.GreaterThan(0));
        }

        [Test]
        public void LoadSync_FileNotFound_ReturnsFalse()
        {
            // Arrange
            var loader = new KosDocLoader(Path.Combine(_tempDir, "nonexistent.json"));

            // Act
            var success = loader.LoadSync();

            // Assert
            Assert.That(success, Is.False);
            Assert.That(loader.IsReady, Is.False);
            Assert.That(loader.LoadError, Does.Contain("not found"));
        }

        [Test]
        public void LoadSync_InvalidSchemaVersion_ReturnsFalse()
        {
            // Arrange
            var json = @"{
                ""schemaVersion"": ""2.0.0"",
                ""contentVersion"": ""1.0.0"",
                ""entries"": []
            }";
            var filePath = Path.Combine(_tempDir, "kos_docs.json");
            File.WriteAllText(filePath, json);
            var loader = new KosDocLoader(filePath);

            // Act
            var success = loader.LoadSync();

            // Assert
            Assert.That(success, Is.False);
        }

        [Test]
        public void LoadSync_ParsesMetadata()
        {
            // Arrange
            var json = @"{
                ""schemaVersion"": ""1.0.0"",
                ""contentVersion"": ""1.4.0.0"",
                ""kosMinVersion"": ""1.4.0.0"",
                ""sourceUrl"": ""https://ksp-kos.github.io/KOS/"",
                ""generatedAt"": ""2025-01-15T12:00:00Z"",
                ""entries"": [
                    {
                        ""id"": ""VESSEL"",
                        ""name"": ""Vessel"",
                        ""type"": ""structure"",
                        ""description"": ""A vessel.""
                    }
                ]
            }";
            var filePath = Path.Combine(_tempDir, "kos_docs.json");
            File.WriteAllText(filePath, json);
            var loader = new KosDocLoader(filePath);

            // Act
            loader.LoadSync();

            // Assert
            Assert.That(loader.Index.SchemaVersion, Is.EqualTo("1.0.0"));
            Assert.That(loader.Index.ContentVersion, Is.EqualTo("1.4.0.0"));
            Assert.That(loader.Index.KosMinVersion, Is.EqualTo("1.4.0.0"));
            Assert.That(loader.Index.SourceUrl, Is.EqualTo("https://ksp-kos.github.io/KOS/"));
        }

        [Test]
        public void LoadSync_ParsesEntryFields()
        {
            // Arrange
            var json = @"{
                ""schemaVersion"": ""1.0.0"",
                ""contentVersion"": ""1.0.0"",
                ""entries"": [
                    {
                        ""id"": ""VESSEL:ALTITUDE"",
                        ""name"": ""ALTITUDE"",
                        ""type"": ""suffix"",
                        ""parentStructure"": ""VESSEL"",
                        ""returnType"": ""Scalar"",
                        ""access"": ""get"",
                        ""description"": ""The altitude above sea level."",
                        ""snippet"": ""PRINT SHIP:ALTITUDE."",
                        ""sourceRef"": ""https://example.com"",
                        ""tags"": [""vessel"", ""position""],
                        ""aliases"": [""ALT""],
                        ""related"": [""VESSEL:GEOPOSITION""],
                        ""deprecated"": false
                    }
                ]
            }";
            var filePath = Path.Combine(_tempDir, "kos_docs.json");
            File.WriteAllText(filePath, json);
            var loader = new KosDocLoader(filePath);

            // Act
            loader.LoadSync();
            var entry = loader.Index.GetById("VESSEL:ALTITUDE");

            // Assert
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.Name, Is.EqualTo("ALTITUDE"));
            Assert.That(entry.Type, Is.EqualTo(DocEntryType.Suffix));
            Assert.That(entry.ParentStructure, Is.EqualTo("VESSEL"));
            Assert.That(entry.ReturnType, Is.EqualTo("Scalar"));
            Assert.That(entry.Access, Is.EqualTo(DocAccessMode.Get));
            Assert.That(entry.Description, Is.EqualTo("The altitude above sea level."));
            Assert.That(entry.Snippet, Is.EqualTo("PRINT SHIP:ALTITUDE."));
            Assert.That(entry.SourceRef, Is.EqualTo("https://example.com"));
            Assert.That(entry.Tags, Does.Contain("vessel"));
            Assert.That(entry.Tags, Does.Contain("position"));
            Assert.That(entry.Aliases, Does.Contain("ALT"));
            Assert.That(entry.Related, Does.Contain("VESSEL:GEOPOSITION"));
            Assert.That(entry.Deprecated, Is.False);
        }

        [Test]
        public void LoadSync_ParsesAllEntryTypes()
        {
            // Arrange
            var json = @"{
                ""schemaVersion"": ""1.0.0"",
                ""contentVersion"": ""1.0.0"",
                ""entries"": [
                    { ""id"": ""S1"", ""name"": ""S1"", ""type"": ""structure"", ""description"": ""d"" },
                    { ""id"": ""S2"", ""name"": ""S2"", ""type"": ""suffix"", ""description"": ""d"" },
                    { ""id"": ""S3"", ""name"": ""S3"", ""type"": ""function"", ""description"": ""d"" },
                    { ""id"": ""S4"", ""name"": ""S4"", ""type"": ""keyword"", ""description"": ""d"" },
                    { ""id"": ""S5"", ""name"": ""S5"", ""type"": ""constant"", ""description"": ""d"" },
                    { ""id"": ""S6"", ""name"": ""S6"", ""type"": ""command"", ""description"": ""d"" }
                ]
            }";
            var filePath = Path.Combine(_tempDir, "kos_docs.json");
            File.WriteAllText(filePath, json);
            var loader = new KosDocLoader(filePath);

            // Act
            loader.LoadSync();

            // Assert
            Assert.That(loader.Index.GetById("S1").Type, Is.EqualTo(DocEntryType.Structure));
            Assert.That(loader.Index.GetById("S2").Type, Is.EqualTo(DocEntryType.Suffix));
            Assert.That(loader.Index.GetById("S3").Type, Is.EqualTo(DocEntryType.Function));
            Assert.That(loader.Index.GetById("S4").Type, Is.EqualTo(DocEntryType.Keyword));
            Assert.That(loader.Index.GetById("S5").Type, Is.EqualTo(DocEntryType.Constant));
            Assert.That(loader.Index.GetById("S6").Type, Is.EqualTo(DocEntryType.Command));
        }

        [Test]
        public void LoadSync_ParsesAllAccessModes()
        {
            // Arrange
            var json = @"{
                ""schemaVersion"": ""1.0.0"",
                ""contentVersion"": ""1.0.0"",
                ""entries"": [
                    { ""id"": ""A1"", ""name"": ""A1"", ""type"": ""suffix"", ""access"": ""get"", ""description"": ""d"" },
                    { ""id"": ""A2"", ""name"": ""A2"", ""type"": ""suffix"", ""access"": ""set"", ""description"": ""d"" },
                    { ""id"": ""A3"", ""name"": ""A3"", ""type"": ""suffix"", ""access"": ""get/set"", ""description"": ""d"" },
                    { ""id"": ""A4"", ""name"": ""A4"", ""type"": ""suffix"", ""access"": ""method"", ""description"": ""d"" }
                ]
            }";
            var filePath = Path.Combine(_tempDir, "kos_docs.json");
            File.WriteAllText(filePath, json);
            var loader = new KosDocLoader(filePath);

            // Act
            loader.LoadSync();

            // Assert
            Assert.That(loader.Index.GetById("A1").Access, Is.EqualTo(DocAccessMode.Get));
            Assert.That(loader.Index.GetById("A2").Access, Is.EqualTo(DocAccessMode.Set));
            Assert.That(loader.Index.GetById("A3").Access, Is.EqualTo(DocAccessMode.GetSet));
            Assert.That(loader.Index.GetById("A4").Access, Is.EqualTo(DocAccessMode.Method));
        }

        [Test]
        public void LoadSync_ParsesDeprecatedEntry()
        {
            // Arrange
            var json = @"{
                ""schemaVersion"": ""1.0.0"",
                ""contentVersion"": ""1.0.0"",
                ""entries"": [
                    {
                        ""id"": ""OLD"",
                        ""name"": ""OLD"",
                        ""type"": ""function"",
                        ""description"": ""Old function."",
                        ""deprecated"": true,
                        ""deprecationNote"": ""Use NEW instead.""
                    }
                ]
            }";
            var filePath = Path.Combine(_tempDir, "kos_docs.json");
            File.WriteAllText(filePath, json);
            var loader = new KosDocLoader(filePath);

            // Act
            loader.LoadSync();
            var entry = loader.Index.GetById("OLD");

            // Assert
            Assert.That(entry.Deprecated, Is.True);
            Assert.That(entry.DeprecationNote, Is.EqualTo("Use NEW instead."));
        }

        [Test]
        public void LoadSync_HandlesEscapedStrings()
        {
            // Arrange
            var json = @"{
                ""schemaVersion"": ""1.0.0"",
                ""contentVersion"": ""1.0.0"",
                ""entries"": [
                    {
                        ""id"": ""PRINT"",
                        ""name"": ""PRINT"",
                        ""type"": ""command"",
                        ""description"": ""Prints text with \""quotes\"" and\nnewlines."",
                        ""snippet"": ""PRINT \""Hello\"".\nPRINT \""World\"".""
                    }
                ]
            }";
            var filePath = Path.Combine(_tempDir, "kos_docs.json");
            File.WriteAllText(filePath, json);
            var loader = new KosDocLoader(filePath);

            // Act
            loader.LoadSync();
            var entry = loader.Index.GetById("PRINT");

            // Assert
            Assert.That(entry.Description, Does.Contain("\"quotes\""));
            Assert.That(entry.Description, Does.Contain("\n"));
            Assert.That(entry.Snippet, Does.Contain("\"Hello\""));
        }

        [Test]
        public void LoadSync_SkipsEntriesWithMissingRequiredFields()
        {
            // Arrange
            var json = @"{
                ""schemaVersion"": ""1.0.0"",
                ""contentVersion"": ""1.0.0"",
                ""entries"": [
                    { ""name"": ""NoId"", ""type"": ""structure"", ""description"": ""d"" },
                    { ""id"": ""NoName"", ""type"": ""structure"", ""description"": ""d"" },
                    { ""id"": ""VALID"", ""name"": ""VALID"", ""type"": ""structure"", ""description"": ""d"" }
                ]
            }";
            var filePath = Path.Combine(_tempDir, "kos_docs.json");
            File.WriteAllText(filePath, json);
            var loader = new KosDocLoader(filePath);

            // Act
            loader.LoadSync();

            // Assert - only VALID should be loaded
            Assert.That(loader.Index.Count, Is.EqualTo(1));
            Assert.That(loader.Index.GetById("VALID"), Is.Not.Null);
        }

        #endregion

        #region IsReady Tests

        [Test]
        public void IsReady_BeforeLoad_ReturnsFalse()
        {
            // Arrange
            var loader = new KosDocLoader(Path.Combine(_tempDir, "kos_docs.json"));

            // Assert
            Assert.That(loader.IsReady, Is.False);
        }

        [Test]
        public void IsReady_AfterSuccessfulLoad_ReturnsTrue()
        {
            // Arrange
            var json = CreateMinimalValidJson();
            var filePath = Path.Combine(_tempDir, "kos_docs.json");
            File.WriteAllText(filePath, json);
            var loader = new KosDocLoader(filePath);

            // Act
            loader.LoadSync();

            // Assert
            Assert.That(loader.IsReady, Is.True);
        }

        #endregion

        #region Helper Methods

        private string CreateMinimalValidJson()
        {
            return @"{
                ""schemaVersion"": ""1.0.0"",
                ""contentVersion"": ""1.0.0"",
                ""entries"": [
                    {
                        ""id"": ""VESSEL"",
                        ""name"": ""Vessel"",
                        ""type"": ""structure"",
                        ""description"": ""Represents a vessel.""
                    }
                ]
            }";
        }

        #endregion
    }
}
