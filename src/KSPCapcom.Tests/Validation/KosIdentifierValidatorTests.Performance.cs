using System.Collections.Generic;
using NUnit.Framework;
using KSPCapcom.KosDocs;
using KSPCapcom.Validation;

namespace KSPCapcom.Tests.Validation
{
    /// <summary>
    /// Performance characteristics and duplicate handling tests.
    /// </summary>
    public partial class KosIdentifierValidatorTests
    {
        #region Performance Characteristics Tests

        [Test]
        public void Validate_LargeIdentifierSet_CompletesQuickly()
        {
            // Arrange - create many docs
            var docs = new List<DocEntry>();
            for (int i = 0; i < 100; i++)
            {
                docs.Add(CreateDocEntry($"STRUCT{i}:SUFFIX{i}", $"SUFFIX{i}", DocEntryType.Suffix, $"STRUCT{i}"));
            }
            var validator = new KosIdentifierValidator(docs, _index);

            // Create identifiers
            var identifiers = new KosIdentifierSet();
            for (int i = 0; i < 200; i++)
            {
                AddIdentifier(identifiers, $"STRUCT{i % 100}:SUFFIX{i % 100}", false, i + 1);
            }

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = validator.Validate(identifiers);
            stopwatch.Stop();

            // Assert - should complete in reasonable time
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000),
                "Validation of 200 identifiers should complete in under 1 second");
        }

        #endregion

        #region Duplicate Handling Tests

        [Test]
        public void Validate_DuplicateIdentifiers_HandledCorrectly()
        {
            var docs = new List<DocEntry>
            {
                CreateDocEntry("SHIP:ALTITUDE", "ALTITUDE", DocEntryType.Suffix, "SHIP")
            };
            var validator = new KosIdentifierValidator(docs);

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:ALTITUDE", false, 1);
            AddIdentifier(identifiers, "SHIP:ALTITUDE", false, 5);
            AddIdentifier(identifiers, "SHIP:ALTITUDE", false, 10);

            var result = validator.Validate(identifiers);

            // Should only have one verified entry (deduplicated)
            Assert.That(result.Verified.Count, Is.EqualTo(1));
        }

        #endregion
    }
}
