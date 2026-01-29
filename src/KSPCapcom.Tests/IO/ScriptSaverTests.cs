using System;
using System.IO;
using NUnit.Framework;
using KSPCapcom.IO;

namespace KSPCapcom.Tests.IO
{
    [TestFixture]
    public class ScriptSaverTests
    {
        private ScriptSaver _saver;
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _saver = new ScriptSaver();
            _tempDir = Path.Combine(Path.GetTempPath(), "ScriptSaverTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        #region ValidateFilename - Empty/Whitespace

        [Test]
        public void ValidateFilename_Null_ReturnsInvalid()
        {
            var result = _saver.ValidateFilename(null);
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Filename cannot be empty", result.Error);
        }

        [Test]
        public void ValidateFilename_Empty_ReturnsInvalid()
        {
            var result = _saver.ValidateFilename("");
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Filename cannot be empty", result.Error);
        }

        [Test]
        public void ValidateFilename_Whitespace_ReturnsInvalid()
        {
            var result = _saver.ValidateFilename("   ");
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Filename cannot be empty", result.Error);
        }

        [Test]
        public void ValidateFilename_LeadingWhitespace_ReturnsInvalid()
        {
            var result = _saver.ValidateFilename(" script.ks");
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Filename cannot have leading/trailing whitespace", result.Error);
        }

        [Test]
        public void ValidateFilename_TrailingWhitespace_ReturnsInvalid()
        {
            var result = _saver.ValidateFilename("script.ks ");
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Filename cannot have leading/trailing whitespace", result.Error);
        }

        #endregion

        #region ValidateFilename - Path Separators

        [Test]
        public void ValidateFilename_ForwardSlash_ReturnsInvalid()
        {
            var result = _saver.ValidateFilename("dir/script.ks");
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Filename cannot contain path separators", result.Error);
        }

        [Test]
        public void ValidateFilename_Backslash_ReturnsInvalid()
        {
            var result = _saver.ValidateFilename("dir\\script.ks");
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Filename cannot contain path separators", result.Error);
        }

        [Test]
        public void ValidateFilename_DoubleDot_ReturnsInvalid()
        {
            var result = _saver.ValidateFilename("..script.ks");
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Filename cannot contain path separators", result.Error);
        }

        [Test]
        public void ValidateFilename_ParentDirectory_ReturnsInvalid()
        {
            var result = _saver.ValidateFilename("../script.ks");
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Filename cannot contain path separators", result.Error);
        }

        #endregion

        #region ValidateFilename - Control Characters

        [Test]
        public void ValidateFilename_NullChar_ReturnsInvalid()
        {
            var result = _saver.ValidateFilename("script\0.ks");
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Filename cannot contain control characters", result.Error);
        }

        [Test]
        public void ValidateFilename_Tab_ReturnsInvalid()
        {
            var result = _saver.ValidateFilename("script\t.ks");
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Filename cannot contain control characters", result.Error);
        }

        [Test]
        public void ValidateFilename_Newline_ReturnsInvalid()
        {
            var result = _saver.ValidateFilename("script\n.ks");
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Filename cannot contain control characters", result.Error);
        }

        [Test]
        public void ValidateFilename_CarriageReturn_ReturnsInvalid()
        {
            var result = _saver.ValidateFilename("script\r.ks");
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Filename cannot contain control characters", result.Error);
        }

        [Test]
        public void ValidateFilename_Bell_ReturnsInvalid()
        {
            var result = _saver.ValidateFilename("script\x07.ks");
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Filename cannot contain control characters", result.Error);
        }

        #endregion

        #region ValidateFilename - Invalid Windows Characters

        [TestCase('<')]
        [TestCase('>')]
        [TestCase(':')]
        [TestCase('"')]
        [TestCase('|')]
        [TestCase('?')]
        [TestCase('*')]
        public void ValidateFilename_InvalidWindowsChar_ReturnsInvalid(char invalidChar)
        {
            var result = _saver.ValidateFilename($"script{invalidChar}.ks");
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual($"Filename cannot contain: {invalidChar}", result.Error);
        }

        #endregion

        #region ValidateFilename - Trailing Dots and Spaces

        [Test]
        public void ValidateFilename_TrailingDotBeforeExtension_ReturnsInvalid()
        {
            var result = _saver.ValidateFilename("script..ks");
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Filename cannot end with a dot or space", result.Error);
        }

        [Test]
        public void ValidateFilename_TrailingSpaceBeforeExtension_ReturnsInvalid()
        {
            var result = _saver.ValidateFilename("script .ks");
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Filename cannot end with a dot or space", result.Error);
        }

        [Test]
        public void ValidateFilename_NameEndingWithDotNoExtension_ReturnsInvalid()
        {
            var result = _saver.ValidateFilename("script.");
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Filename cannot end with a dot or space", result.Error);
        }

        #endregion

        #region ValidateFilename - Length Limits

        [Test]
        public void ValidateFilename_Exactly64Chars_ReturnsValid()
        {
            string name = new string('a', 64) + ".ks";
            var result = _saver.ValidateFilename(name);
            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void ValidateFilename_65Chars_ReturnsInvalid()
        {
            string name = new string('a', 65) + ".ks";
            var result = _saver.ValidateFilename(name);
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Filename too long (max 64 characters before extension)", result.Error);
        }

        [Test]
        public void ValidateFilename_100Chars_ReturnsInvalid()
        {
            string name = new string('x', 100) + ".ks";
            var result = _saver.ValidateFilename(name);
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Filename too long (max 64 characters before extension)", result.Error);
        }

        #endregion

        #region ValidateFilename - Reserved Names

        [TestCase("CON")]
        [TestCase("PRN")]
        [TestCase("AUX")]
        [TestCase("NUL")]
        [TestCase("COM1")]
        [TestCase("COM9")]
        [TestCase("LPT1")]
        [TestCase("LPT9")]
        public void ValidateFilename_ReservedName_ReturnsInvalid(string reserved)
        {
            var result = _saver.ValidateFilename(reserved + ".ks");
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual($"'{reserved}' is a reserved filename", result.Error);
        }

        [Test]
        public void ValidateFilename_ReservedNameLowerCase_ReturnsInvalid()
        {
            var result = _saver.ValidateFilename("con.ks");
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("'CON' is a reserved filename", result.Error);
        }

        [Test]
        public void ValidateFilename_ReservedNameMixedCase_ReturnsInvalid()
        {
            var result = _saver.ValidateFilename("CoN.ks");
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("'CON' is a reserved filename", result.Error);
        }

        #endregion

        #region ValidateFilename - Valid Names

        [Test]
        public void ValidateFilename_SimpleValid_ReturnsValid()
        {
            var result = _saver.ValidateFilename("script.ks");
            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.Error);
        }

        [Test]
        public void ValidateFilename_WithoutExtension_ReturnsValid()
        {
            var result = _saver.ValidateFilename("ascent");
            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void ValidateFilename_WithUnderscore_ReturnsValid()
        {
            var result = _saver.ValidateFilename("my_script.ks");
            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void ValidateFilename_WithHyphen_ReturnsValid()
        {
            var result = _saver.ValidateFilename("my-script.ks");
            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void ValidateFilename_WithNumbers_ReturnsValid()
        {
            var result = _saver.ValidateFilename("script123.ks");
            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void ValidateFilename_ContainsReservedNameAsSubstring_ReturnsValid()
        {
            // "condition" contains "CON" but is not a reserved name
            var result = _saver.ValidateFilename("condition.ks");
            Assert.IsTrue(result.IsValid);
        }

        #endregion

        #region Save - Integration Tests

        [Test]
        public void Save_ValidFilename_CreatesFile()
        {
            var result = _saver.Save(_tempDir, "test_script.ks", "PRINT \"Hello\".");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(File.Exists(result.FullPath));
            Assert.AreEqual("PRINT \"Hello\".", File.ReadAllText(result.FullPath));
        }

        [Test]
        public void Save_AddsKsExtension_WhenMissing()
        {
            var result = _saver.Save(_tempDir, "test_script", "PRINT \"Hello\".");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.FullPath.EndsWith(".ks"));
        }

        [Test]
        public void Save_InvalidFilename_ReturnsError()
        {
            var result = _saver.Save(_tempDir, "test/script.ks", "PRINT \"Hello\".");

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Filename cannot contain path separators", result.Error);
        }

        [Test]
        public void Save_EmptyArchivePath_ReturnsError()
        {
            var result = _saver.Save("", "script.ks", "PRINT \"Hello\".");

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Configure kOS archive path in Settings", result.Error);
        }

        [Test]
        public void Save_NonExistentArchive_ReturnsError()
        {
            var result = _saver.Save("/nonexistent/path", "script.ks", "PRINT \"Hello\".");

            Assert.IsFalse(result.Success);
            Assert.That(result.Error, Does.Contain("Archive folder not found"));
        }

        #endregion

        #region FileExists - Tests

        [Test]
        public void FileExists_ExistingFile_ReturnsTrue()
        {
            string filePath = Path.Combine(_tempDir, "existing.ks");
            File.WriteAllText(filePath, "// test");

            Assert.IsTrue(_saver.FileExists(_tempDir, "existing.ks"));
        }

        [Test]
        public void FileExists_NonExistingFile_ReturnsFalse()
        {
            Assert.IsFalse(_saver.FileExists(_tempDir, "nonexistent.ks"));
        }

        [Test]
        public void FileExists_AddsKsExtension()
        {
            string filePath = Path.Combine(_tempDir, "existing.ks");
            File.WriteAllText(filePath, "// test");

            Assert.IsTrue(_saver.FileExists(_tempDir, "existing"));
        }

        [Test]
        public void FileExists_InvalidFilename_ReturnsFalse()
        {
            // Should not throw, just return false
            Assert.IsFalse(_saver.FileExists(_tempDir, "invalid/path.ks"));
        }

        [Test]
        public void FileExists_NullArchive_ReturnsFalse()
        {
            Assert.IsFalse(_saver.FileExists(null, "script.ks"));
        }

        [Test]
        public void FileExists_NullFilename_ReturnsFalse()
        {
            Assert.IsFalse(_saver.FileExists(_tempDir, null));
        }

        #endregion

        #region GenerateDefaultFilename - Tests

        [Test]
        public void GenerateDefaultFilename_DetectsAscent()
        {
            string filename = _saver.GenerateDefaultFilename("Write an ascent script");
            Assert.That(filename, Does.StartWith("ascent_"));
            Assert.That(filename, Does.EndWith(".ks"));
        }

        [Test]
        public void GenerateDefaultFilename_DetectsLanding()
        {
            string filename = _saver.GenerateDefaultFilename("Script for landing on Mun");
            Assert.That(filename, Does.StartWith("landing_"));
        }

        [Test]
        public void GenerateDefaultFilename_DefaultsToScript()
        {
            string filename = _saver.GenerateDefaultFilename("Do something cool");
            Assert.That(filename, Does.StartWith("script_"));
        }

        [Test]
        public void GenerateDefaultFilename_NullPrompt_ReturnsScript()
        {
            string filename = _saver.GenerateDefaultFilename(null);
            Assert.That(filename, Does.StartWith("script_"));
        }

        #endregion
    }
}
