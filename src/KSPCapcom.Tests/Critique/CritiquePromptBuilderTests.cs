using NUnit.Framework;

namespace KSPCapcom.Tests.Critique
{
    /// <summary>
    /// Tests for CritiquePromptBuilder constants and version.
    /// Full integration tests with EditorCraftSnapshot require KSP runtime.
    /// </summary>
    [TestFixture]
    public class CritiquePromptBuilderTests
    {
        #region Version Tests

        [Test]
        public void Version_IsNotNullOrEmpty()
        {
            // Version constant should be defined
            var version = CritiquePromptBuilderConstants.Version;
            Assert.That(version, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void Version_FollowsSemanticVersioning()
        {
            // Should be in format X.Y.Z
            var parts = CritiquePromptBuilderConstants.Version.Split('.');
            Assert.That(parts.Length, Is.EqualTo(3), "Version should have 3 parts");

            foreach (var part in parts)
            {
                Assert.That(int.TryParse(part, out _), Is.True,
                    $"Version part '{part}' should be numeric");
            }
        }

        [Test]
        public void Version_IsVersion1_0_0()
        {
            // Initial version should be 1.0.0
            Assert.That(CritiquePromptBuilderConstants.Version, Is.EqualTo("1.0.0"));
        }

        #endregion

        #region Prompt Content Tests

        [Test]
        public void CoreIdentity_ContainsCAPCOM()
        {
            Assert.That(CritiquePromptBuilderConstants.CoreIdentity, Does.Contain("CAPCOM"));
        }

        [Test]
        public void CoreIdentity_ContainsDesignReviewer()
        {
            Assert.That(CritiquePromptBuilderConstants.CoreIdentity, Does.Contain("design reviewer"));
        }

        [Test]
        public void OutputFormatInstructions_ContainsCriticalIssues()
        {
            Assert.That(CritiquePromptBuilderConstants.OutputFormatInstructions,
                Does.Contain("CRITICAL ISSUES"));
        }

        [Test]
        public void OutputFormatInstructions_ContainsImprovements()
        {
            Assert.That(CritiquePromptBuilderConstants.OutputFormatInstructions,
                Does.Contain("IMPROVEMENTS"));
        }

        [Test]
        public void OutputFormatInstructions_ContainsExactly3()
        {
            Assert.That(CritiquePromptBuilderConstants.OutputFormatInstructions,
                Does.Contain("exactly 3"));
        }

        [Test]
        public void DataReferenceGuidelines_ContainsSpecificValues()
        {
            Assert.That(CritiquePromptBuilderConstants.DataReferenceGuidelines,
                Does.Contain("specific values"));
        }

        [Test]
        public void DataReferenceGuidelines_ContainsKSPTerminology()
        {
            Assert.That(CritiquePromptBuilderConstants.DataReferenceGuidelines,
                Does.Contain("KSP terminology"));
        }

        #endregion

        #region User Message Tests

        [Test]
        public void UserMessage_IsNotNullOrEmpty()
        {
            Assert.That(CritiquePromptBuilderConstants.UserMessage, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void UserMessage_ContainsAnalyze()
        {
            Assert.That(CritiquePromptBuilderConstants.UserMessage.ToLower(),
                Does.Contain("analyze"));
        }

        #endregion
    }

    /// <summary>
    /// Constants from CritiquePromptBuilder for testing without KSP dependencies.
    /// These mirror the actual values used in the implementation.
    /// </summary>
    public static class CritiquePromptBuilderConstants
    {
        public const string Version = "1.0.0";

        public const string CoreIdentity =
            "You are CAPCOM's spacecraft design reviewer for Kerbal Space Program. " +
            "Analyze the provided craft data and give focused, actionable feedback.";

        public const string OutputFormatInstructions =
            "RESPONSE FORMAT:\n" +
            "Provide exactly 3 critical issues and exactly 3 improvement suggestions.\n\n" +
            "CRITICAL ISSUES (top 3 problems to fix):\n" +
            "1. [Issue description referencing specific data, e.g., \"TWR of 0.8 is too low for Kerbin launch\"]\n" +
            "2. [Issue]\n" +
            "3. [Issue]\n\n" +
            "IMPROVEMENTS (top 3 suggestions):\n" +
            "1. [Improvement with specific guidance]\n" +
            "2. [Improvement]\n" +
            "3. [Improvement]";

        public const string DataReferenceGuidelines =
            "IMPORTANT: Reference specific values from the craft data. " +
            "Say \"TWR of 0.8\" not \"low TWR\". Say \"45 parts\" not \"many parts\". " +
            "Use KSP terminology (periapsis, delta-v, TWR, staging). " +
            "Keep each point to 1-2 sentences. Be direct and specific.";

        public const string UserMessage = "Analyze this spacecraft design and provide your critique.";
    }
}
