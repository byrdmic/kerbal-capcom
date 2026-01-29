using System.Collections.Generic;
using NUnit.Framework;
using KSPCapcom.KosDocs;
using KSPCapcom.Validation;

namespace KSPCapcom.Tests.Validation
{
    /// <summary>
    /// Tests for the KosIdentifierValidator.
    /// Split into partial classes by test category.
    /// </summary>
    [TestFixture]
    public partial class KosIdentifierValidatorTests
    {
        private KosDocIndex _index;

        [SetUp]
        public void SetUp()
        {
            _index = new KosDocIndex();
            PopulateTestIndex(_index);
        }

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
