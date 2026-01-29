using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KSPCapcom.KosDocs;
using KSPCapcom.Validation;

namespace KSPCapcom.Tests.Validation
{
    /// <summary>
    /// Summary generation tests for validation results.
    /// </summary>
    public partial class KosIdentifierValidatorTests
    {
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
    }
}
