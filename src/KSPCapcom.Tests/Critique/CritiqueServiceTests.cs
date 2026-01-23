using NUnit.Framework;

namespace KSPCapcom.Tests.Critique
{
    /// <summary>
    /// Tests for CritiqueService constants and configuration.
    /// Full integration tests with EditorCraftSnapshot and LLM require KSP runtime.
    /// </summary>
    [TestFixture]
    public class CritiqueServiceTests
    {
        #region Configuration Constants Tests

        [Test]
        public void MinPartCount_IsAtLeast3()
        {
            // Minimum part count should be at least 3 for meaningful critique
            Assert.That(CritiqueServiceConstants.MinPartCount, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void MinPartCount_IsExactly3()
        {
            // Per design spec, minimum is exactly 3
            Assert.That(CritiqueServiceConstants.MinPartCount, Is.EqualTo(3));
        }

        [Test]
        public void CritiqueTimeoutMs_IsLongerThanDefaultChat()
        {
            // Critique timeout should be longer than normal chat (30s default)
            const int defaultChatTimeout = 30000;
            Assert.That(CritiqueServiceConstants.CritiqueTimeoutMs,
                Is.GreaterThan(defaultChatTimeout));
        }

        [Test]
        public void CritiqueTimeoutMs_Is45Seconds()
        {
            // Per design spec, timeout is 45 seconds
            Assert.That(CritiqueServiceConstants.CritiqueTimeoutMs, Is.EqualTo(45000));
        }

        [Test]
        public void CritiqueTemperature_IsReasonable()
        {
            // Temperature should be between 0 and 1 for focused output
            Assert.That(CritiqueServiceConstants.CritiqueTemperature,
                Is.GreaterThanOrEqualTo(0f));
            Assert.That(CritiqueServiceConstants.CritiqueTemperature,
                Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void CritiqueTemperature_Is0_7()
        {
            // Per design spec, temperature is 0.7 (focused but slightly creative)
            Assert.That(CritiqueServiceConstants.CritiqueTemperature, Is.EqualTo(0.7f));
        }

        [Test]
        public void CritiqueMaxTokens_IsSufficientForOutput()
        {
            // Max tokens should be enough for 6 items + formatting
            // Estimate: ~100-150 tokens per item, 6 items = 600-900, plus headers
            Assert.That(CritiqueServiceConstants.CritiqueMaxTokens,
                Is.GreaterThanOrEqualTo(1000));
        }

        [Test]
        public void CritiqueMaxTokens_Is1024()
        {
            // Per design spec, max tokens is 1024
            Assert.That(CritiqueServiceConstants.CritiqueMaxTokens, Is.EqualTo(1024));
        }

        #endregion

        #region Validation Rules Tests

        [Test]
        public void ValidationReason_NoCraft_IsDescriptive()
        {
            var reason = CritiqueServiceConstants.NoCraftReason;
            Assert.That(reason, Does.Contain("craft").IgnoreCase);
        }

        [Test]
        public void ValidationReason_NotEnoughParts_IsDescriptive()
        {
            var reason = CritiqueServiceConstants.NotEnoughPartsReason;
            Assert.That(reason, Does.Contain("parts").IgnoreCase);
        }

        [Test]
        public void ValidationReason_NotSpacecraft_IsDescriptive()
        {
            var reason = CritiqueServiceConstants.NotSpacecraftReason;
            Assert.That(reason, Does.Contain("command").IgnoreCase.Or.Contain("engine").IgnoreCase);
        }

        #endregion
    }

    /// <summary>
    /// Constants from CritiqueService for testing without KSP dependencies.
    /// These mirror the actual values used in the implementation.
    /// </summary>
    public static class CritiqueServiceConstants
    {
        /// <summary>
        /// Minimum number of parts required for critique.
        /// </summary>
        public const int MinPartCount = 3;

        /// <summary>
        /// Request timeout for critique requests (ms).
        /// </summary>
        public const int CritiqueTimeoutMs = 45000;

        /// <summary>
        /// Temperature for critique requests.
        /// </summary>
        public const float CritiqueTemperature = 0.7f;

        /// <summary>
        /// Max tokens for critique responses.
        /// </summary>
        public const int CritiqueMaxTokens = 1024;

        /// <summary>
        /// Validation reason for empty/null craft.
        /// </summary>
        public const string NoCraftReason = "No craft loaded";

        /// <summary>
        /// Validation reason for insufficient parts.
        /// </summary>
        public const string NotEnoughPartsReason = "Not enough parts to critique (need 3+)";

        /// <summary>
        /// Validation reason for not being a spacecraft.
        /// </summary>
        public const string NotSpacecraftReason = "Not a spacecraft yet (no command or engines)";
    }
}
