using System.Collections.Generic;
using NUnit.Framework;
using KSPCapcom.KosDocs;
using KSPCapcom.Validation;

namespace KSPCapcom.Tests.Validation
{
    /// <summary>
    /// Empty/Null input handling tests.
    /// </summary>
    public partial class KosIdentifierValidatorTests
    {
        #region Empty/Null Input Tests

        [Test]
        public void Validate_NullIdentifiers_ReturnsEmptyResult()
        {
            var validator = new KosIdentifierValidator(new List<DocEntry>());

            var result = validator.Validate(null);

            Assert.That(result.Verified, Is.Empty);
            Assert.That(result.Unverified, Is.Empty);
            Assert.That(result.UserDefined, Is.Empty);
        }

        [Test]
        public void Validate_EmptyIdentifiers_ReturnsEmptyResult()
        {
            var validator = new KosIdentifierValidator(new List<DocEntry>());

            var result = validator.Validate(new KosIdentifierSet());

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Verified, Is.Empty);
        }

        [Test]
        public void Validate_NoDocs_ReturnsWarning()
        {
            var validator = new KosIdentifierValidator(new List<DocEntry>());

            var identifiers = new KosIdentifierSet();
            AddIdentifier(identifiers, "SHIP:ALTITUDE", false, 1);

            var result = validator.Validate(identifiers);

            Assert.That(result.Warning, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Warning, Does.Contain("No documentation"));
        }

        #endregion
    }
}
