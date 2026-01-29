using System;
using System.IO;
using System.Text.RegularExpressions;

namespace KSPCapcom.IO
{
    /// <summary>
    /// Result of a save operation.
    /// </summary>
    public class SaveResult
    {
        /// <summary>
        /// Whether the save succeeded.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Full path to the saved file (on success).
        /// </summary>
        public string FullPath { get; }

        /// <summary>
        /// Error message (on failure).
        /// </summary>
        public string Error { get; }

        /// <summary>
        /// Whether an existing file was overwritten.
        /// </summary>
        public bool WasOverwritten { get; }

        private SaveResult(bool success, string fullPath, string error, bool wasOverwritten = false)
        {
            Success = success;
            FullPath = fullPath;
            Error = error;
            WasOverwritten = wasOverwritten;
        }

        public static SaveResult Ok(string fullPath, bool wasOverwritten = false)
            => new SaveResult(true, fullPath, null, wasOverwritten);
        public static SaveResult Fail(string error) => new SaveResult(false, null, error);
    }

    /// <summary>
    /// Result of filename validation.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Whether the filename is valid.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Error message if invalid.
        /// </summary>
        public string Error { get; }

        private ValidationResult(bool isValid, string error)
        {
            IsValid = isValid;
            Error = error;
        }

        public static ValidationResult Ok() => new ValidationResult(true, null);
        public static ValidationResult Invalid(string error) => new ValidationResult(false, error);
    }

    /// <summary>
    /// Handles file I/O operations for saving kOS scripts to the archive.
    /// </summary>
    public class ScriptSaver
    {
        /// <summary>
        /// Reserved Windows filenames that cannot be used.
        /// </summary>
        private static readonly string[] ReservedNames = new string[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>
        /// Invalid characters for Windows filenames.
        /// </summary>
        private static readonly char[] InvalidChars = new char[] { '<', '>', ':', '"', '|', '?', '*' };

        /// <summary>
        /// Maximum length for filename (before extension).
        /// </summary>
        private const int MaxFilenameLength = 64;

        /// <summary>
        /// Pattern for extracting script type from prompts.
        /// </summary>
        private static readonly Regex ScriptTypePattern = new Regex(
            @"\b(ascent|circularize|maneuver|landing|hover|docking|rendezvous|orbit|launch)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Save a script to the archive folder.
        /// </summary>
        /// <param name="archivePath">Path to the kOS archive folder.</param>
        /// <param name="filename">The filename to save as (with or without .ks extension).</param>
        /// <param name="content">The script content.</param>
        /// <param name="isOverwrite">Whether this save is overwriting an existing file.</param>
        /// <returns>Result indicating success or failure.</returns>
        public SaveResult Save(string archivePath, string filename, string content, bool isOverwrite = false)
        {
            // Validate archive path
            if (string.IsNullOrWhiteSpace(archivePath))
            {
                return SaveResult.Fail("Configure kOS archive path in Settings");
            }

            if (!Directory.Exists(archivePath))
            {
                return SaveResult.Fail($"Archive folder not found: {archivePath}");
            }

            // Validate and normalize filename
            var validation = ValidateFilename(filename);
            if (!validation.IsValid)
            {
                return SaveResult.Fail(validation.Error);
            }

            // Ensure .ks extension
            string normalizedFilename = NormalizeFilename(filename);
            string fullPath = Path.Combine(archivePath, normalizedFilename);

            // Validate path is within archive (defense-in-depth against path traversal)
            var pathValidation = ValidateSavePath(archivePath, fullPath);
            if (!pathValidation.IsValid)
            {
                return SaveResult.Fail(pathValidation.Error);
            }

            try
            {
                File.WriteAllText(fullPath, content);
                CapcomCore.Log($"ScriptSaver: Saved script to {fullPath}");
                return SaveResult.Ok(fullPath, isOverwrite);
            }
            catch (UnauthorizedAccessException)
            {
                return SaveResult.Fail("Cannot write to archive: access denied");
            }
            catch (IOException ex)
            {
                return SaveResult.Fail($"Save failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                return SaveResult.Fail($"Save failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a file already exists in the archive.
        /// </summary>
        /// <param name="archivePath">Path to the kOS archive folder.</param>
        /// <param name="filename">The filename to check.</param>
        /// <returns>True if the file exists.</returns>
        public bool FileExists(string archivePath, string filename)
        {
            if (string.IsNullOrWhiteSpace(archivePath) || string.IsNullOrWhiteSpace(filename))
            {
                return false;
            }

            var validation = ValidateFilename(filename);
            if (!validation.IsValid)
            {
                return false;
            }

            string normalizedFilename = NormalizeFilename(filename);
            string fullPath = Path.Combine(archivePath, normalizedFilename);

            var pathValidation = ValidateSavePath(archivePath, fullPath);
            if (!pathValidation.IsValid)
            {
                return false;
            }

            return File.Exists(fullPath);
        }

        /// <summary>
        /// Validate a filename for saving.
        /// </summary>
        /// <param name="filename">The filename to validate.</param>
        /// <returns>Validation result.</returns>
        public ValidationResult ValidateFilename(string filename)
        {
            // Empty or whitespace
            if (string.IsNullOrWhiteSpace(filename))
            {
                return ValidationResult.Invalid("Filename cannot be empty");
            }

            string trimmed = filename.Trim();

            // Leading/trailing whitespace in original
            if (trimmed != filename)
            {
                return ValidationResult.Invalid("Filename cannot have leading/trailing whitespace");
            }

            // Path separators
            if (filename.Contains("/") || filename.Contains("\\") || filename.Contains(".."))
            {
                return ValidationResult.Invalid("Filename cannot contain path separators");
            }

            // Control characters (ASCII 0-31)
            foreach (char c in filename)
            {
                if (c < 32)
                {
                    return ValidationResult.Invalid("Filename cannot contain control characters");
                }
            }

            // Invalid Windows characters
            foreach (char c in InvalidChars)
            {
                if (filename.Contains(c.ToString()))
                {
                    return ValidationResult.Invalid($"Filename cannot contain: {c}");
                }
            }

            // Get name without extension for remaining checks
            string nameWithoutExt = Path.GetFileNameWithoutExtension(filename);

            // Windows silently strips trailing dots and spaces
            if (nameWithoutExt.EndsWith(".") || nameWithoutExt.EndsWith(" "))
            {
                return ValidationResult.Invalid("Filename cannot end with a dot or space");
            }

            // Length check
            if (nameWithoutExt.Length > MaxFilenameLength)
            {
                return ValidationResult.Invalid($"Filename too long (max {MaxFilenameLength} characters before extension)");
            }

            // Reserved Windows filenames
            string nameUpper = nameWithoutExt.ToUpperInvariant();
            foreach (string reserved in ReservedNames)
            {
                if (nameUpper == reserved)
                {
                    return ValidationResult.Invalid($"'{reserved}' is a reserved filename");
                }
            }

            return ValidationResult.Ok();
        }

        /// <summary>
        /// Validate that the resolved save path is safely within the archive directory.
        /// </summary>
        private ValidationResult ValidateSavePath(string archivePath, string resolvedPath)
        {
            try
            {
                string canonicalArchive = Path.GetFullPath(archivePath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string canonicalResolved = Path.GetFullPath(resolvedPath);
                string archivePrefix = canonicalArchive + Path.DirectorySeparatorChar;

                if (!canonicalResolved.StartsWith(archivePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return ValidationResult.Invalid("Save path must be within the archive folder");
                }
                return ValidationResult.Ok();
            }
            catch (Exception)
            {
                return ValidationResult.Invalid("Invalid save path");
            }
        }

        /// <summary>
        /// Generate a default filename based on script type detection.
        /// </summary>
        /// <param name="prompt">The original prompt or script content to analyze.</param>
        /// <returns>A default filename in the format {type}_{timestamp}.ks</returns>
        public string GenerateDefaultFilename(string prompt)
        {
            string scriptType = "script";

            if (!string.IsNullOrEmpty(prompt))
            {
                var match = ScriptTypePattern.Match(prompt);
                if (match.Success)
                {
                    scriptType = match.Groups[1].Value.ToLowerInvariant();
                }
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
            return $"{scriptType}_{timestamp}.ks";
        }

        /// <summary>
        /// Normalize a filename by ensuring it has the .ks extension.
        /// </summary>
        private string NormalizeFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return filename;
            }

            // Add .ks extension if missing
            if (!filename.EndsWith(".ks", StringComparison.OrdinalIgnoreCase))
            {
                return filename + ".ks";
            }

            return filename;
        }
    }
}
