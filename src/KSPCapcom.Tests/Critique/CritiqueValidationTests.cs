using NUnit.Framework;

namespace KSPCapcom.Tests.Critique
{
    /// <summary>
    /// Tests for CritiqueValidation class.
    /// This tests the validation result type without KSP dependencies.
    /// </summary>
    [TestFixture]
    public class CritiqueValidationTests
    {
        #region Valid Result Tests

        [Test]
        public void Valid_IsValid_ReturnsTrue()
        {
            var validation = CritiqueValidation.Valid();

            Assert.That(validation.IsValid, Is.True);
        }

        [Test]
        public void Valid_Reason_IsNull()
        {
            var validation = CritiqueValidation.Valid();

            Assert.That(validation.Reason, Is.Null);
        }

        #endregion

        #region Invalid Result Tests

        [Test]
        public void Invalid_IsValid_ReturnsFalse()
        {
            var validation = CritiqueValidation.Invalid("Test reason");

            Assert.That(validation.IsValid, Is.False);
        }

        [Test]
        public void Invalid_Reason_ReturnsProvidedReason()
        {
            var reason = "No craft loaded";
            var validation = CritiqueValidation.Invalid(reason);

            Assert.That(validation.Reason, Is.EqualTo(reason));
        }

        [Test]
        public void Invalid_WithEmptyReason_StillInvalid()
        {
            var validation = CritiqueValidation.Invalid("");

            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Reason, Is.EqualTo(""));
        }

        [Test]
        public void Invalid_WithNullReason_StillInvalid()
        {
            var validation = CritiqueValidation.Invalid(null);

            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Reason, Is.Null);
        }

        #endregion

        #region Multiple Invocation Tests

        [Test]
        public void Valid_MultipleCalls_ReturnEqualResults()
        {
            var v1 = CritiqueValidation.Valid();
            var v2 = CritiqueValidation.Valid();

            Assert.That(v1.IsValid, Is.EqualTo(v2.IsValid));
            Assert.That(v1.Reason, Is.EqualTo(v2.Reason));
        }

        [Test]
        public void Invalid_SameReason_ReturnEqualResults()
        {
            var reason = "Same reason";
            var v1 = CritiqueValidation.Invalid(reason);
            var v2 = CritiqueValidation.Invalid(reason);

            Assert.That(v1.IsValid, Is.EqualTo(v2.IsValid));
            Assert.That(v1.Reason, Is.EqualTo(v2.Reason));
        }

        #endregion

        #region Common Validation Reasons Tests

        [Test]
        public void Invalid_NoCraftLoaded_HasDescriptiveReason()
        {
            var validation = CritiqueValidation.Invalid("No craft loaded");

            Assert.That(validation.Reason, Does.Contain("craft"));
        }

        [Test]
        public void Invalid_NotEnoughParts_HasDescriptiveReason()
        {
            var validation = CritiqueValidation.Invalid("Not enough parts to critique");

            Assert.That(validation.Reason, Does.Contain("parts"));
        }

        [Test]
        public void Invalid_NotASpacecraft_HasDescriptiveReason()
        {
            var validation = CritiqueValidation.Invalid("Not a spacecraft yet");

            Assert.That(validation.Reason, Does.Contain("spacecraft"));
        }

        #endregion
    }

    /// <summary>
    /// Copy of CritiqueValidation for testing without KSP dependencies.
    /// This mirrors the actual implementation in KSPCapcom.Critique.
    /// </summary>
    public class CritiqueValidation
    {
        public bool IsValid { get; }
        public string Reason { get; }

        private CritiqueValidation(bool isValid, string reason)
        {
            IsValid = isValid;
            Reason = reason;
        }

        public static CritiqueValidation Valid() => new CritiqueValidation(true, null);
        public static CritiqueValidation Invalid(string reason) => new CritiqueValidation(false, reason);
    }
}
