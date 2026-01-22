using System;
using NUnit.Framework;

namespace KSPCapcom.Tests
{
    [TestFixture]
    public class PromptBuilderTests
    {
        #region Teach Mode Tests

        [Test]
        public void BuildSystemPrompt_TeachMode_ContainsTeachInstructions()
        {
            // Arrange
            var settings = new CapcomSettings { Mode = OperationMode.Teach };
            var builder = new PromptBuilder(settings);

            // Act
            var prompt = builder.BuildSystemPrompt();

            // Assert
            Assert.That(prompt, Does.Contain("MODE: TEACH"));
            Assert.That(prompt, Does.Contain("player wants to learn"));
            Assert.That(prompt, Does.Contain("explanatory responses"));
        }

        [Test]
        public void BuildSystemPrompt_TeachMode_DoesNotContainDoInstructions()
        {
            // Arrange
            var settings = new CapcomSettings { Mode = OperationMode.Teach };
            var builder = new PromptBuilder(settings);

            // Act
            var prompt = builder.BuildSystemPrompt();

            // Assert
            Assert.That(prompt, Does.Not.Contain("MODE: DO"));
        }

        #endregion

        #region Do Mode Tests

        [Test]
        public void BuildSystemPrompt_DoMode_ContainsDoInstructions()
        {
            // Arrange
            var settings = new CapcomSettings { Mode = OperationMode.Do };
            var builder = new PromptBuilder(settings);

            // Act
            var prompt = builder.BuildSystemPrompt();

            // Assert
            Assert.That(prompt, Does.Contain("MODE: DO"));
            Assert.That(prompt, Does.Contain("player wants actionable steps"));
            Assert.That(prompt, Does.Contain("concise checklists"));
        }

        [Test]
        public void BuildSystemPrompt_DoMode_DoesNotContainTeachInstructions()
        {
            // Arrange
            var settings = new CapcomSettings { Mode = OperationMode.Do };
            var builder = new PromptBuilder(settings);

            // Act
            var prompt = builder.BuildSystemPrompt();

            // Assert
            Assert.That(prompt, Does.Not.Contain("MODE: TEACH"));
        }

        #endregion

        #region Core Content Tests

        [Test]
        public void BuildSystemPrompt_AlwaysContainsNoAutopilotConstraint()
        {
            // Test both modes to ensure constraint is always present
            var modes = new[] { OperationMode.Teach, OperationMode.Do };

            foreach (var mode in modes)
            {
                // Arrange
                var settings = new CapcomSettings { Mode = mode };
                var builder = new PromptBuilder(settings);

                // Act
                var prompt = builder.BuildSystemPrompt();

                // Assert
                Assert.That(prompt, Does.Contain("IMPORTANT CONSTRAINT"),
                    $"No-autopilot constraint missing in {mode} mode");
                Assert.That(prompt, Does.Contain("do NOT pilot the spacecraft directly"),
                    $"No-autopilot wording missing in {mode} mode");
            }
        }

        [Test]
        public void BuildSystemPrompt_AlwaysContainsCoreIdentity()
        {
            // Test both modes
            var modes = new[] { OperationMode.Teach, OperationMode.Do };

            foreach (var mode in modes)
            {
                // Arrange
                var settings = new CapcomSettings { Mode = mode };
                var builder = new PromptBuilder(settings);

                // Act
                var prompt = builder.BuildSystemPrompt();

                // Assert
                Assert.That(prompt, Does.Contain("CAPCOM"),
                    $"CAPCOM identity missing in {mode} mode");
                Assert.That(prompt, Does.Contain("capsule communicator"),
                    $"Capsule communicator role missing in {mode} mode");
                Assert.That(prompt, Does.Contain("Mission Control"),
                    $"Mission Control reference missing in {mode} mode");
            }
        }

        [Test]
        public void BuildSystemPrompt_ContainsSubjectMatterScope()
        {
            // Arrange
            var settings = new CapcomSettings { Mode = OperationMode.Teach };
            var builder = new PromptBuilder(settings);

            // Act
            var prompt = builder.BuildSystemPrompt();

            // Assert - check for key expertise areas
            Assert.That(prompt, Does.Contain("orbital mechanics"));
            Assert.That(prompt, Does.Contain("kOS"));
            Assert.That(prompt, Does.Contain("delta-v"));
        }

        [Test]
        public void BuildSystemPrompt_ContainsStyleGuidelines()
        {
            // Arrange
            var settings = new CapcomSettings { Mode = OperationMode.Teach };
            var builder = new PromptBuilder(settings);

            // Act
            var prompt = builder.BuildSystemPrompt();

            // Assert
            Assert.That(prompt, Does.Contain("concise but complete"));
            Assert.That(prompt, Does.Contain("KSP players"));
            Assert.That(prompt, Does.Contain("Kerbals"));
        }

        #endregion

        #region Null/Error Handling Tests

        [Test]
        public void BuildSystemPrompt_NullSettings_ReturnsFallbackPrompt()
        {
            // Arrange - pass null settings
            var builder = new PromptBuilder((CapcomSettings)null);

            // Act
            var prompt = builder.BuildSystemPrompt();

            // Assert
            Assert.That(prompt, Is.EqualTo(PromptBuilder.FallbackPrompt));
        }

        [Test]
        public void BuildSystemPrompt_SettingsThrows_ReturnsFallbackPrompt()
        {
            // Arrange - settings provider that throws
            var builder = new PromptBuilder(() => throw new InvalidOperationException("Test exception"));

            // Act
            var prompt = builder.BuildSystemPrompt();

            // Assert
            Assert.That(prompt, Is.EqualTo(PromptBuilder.FallbackPrompt));
        }

        [Test]
        public void Constructor_NullGetSettingsFunc_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new PromptBuilder((Func<CapcomSettings>)null));
        }

        #endregion

        #region Determinism Tests

        [Test]
        public void BuildSystemPrompt_IsDeterministic_SameInputSameOutput()
        {
            // Arrange
            var settings = new CapcomSettings { Mode = OperationMode.Teach };
            var builder = new PromptBuilder(settings);

            // Act - build multiple times
            var prompt1 = builder.BuildSystemPrompt();
            var prompt2 = builder.BuildSystemPrompt();
            var prompt3 = builder.BuildSystemPrompt();

            // Assert - all outputs should be identical
            Assert.That(prompt2, Is.EqualTo(prompt1));
            Assert.That(prompt3, Is.EqualTo(prompt1));
        }

        [Test]
        public void BuildSystemPrompt_DifferentInstances_SameSettingsProduceSameOutput()
        {
            // Arrange
            var settings1 = new CapcomSettings { Mode = OperationMode.Do };
            var settings2 = new CapcomSettings { Mode = OperationMode.Do };
            var builder1 = new PromptBuilder(settings1);
            var builder2 = new PromptBuilder(settings2);

            // Act
            var prompt1 = builder1.BuildSystemPrompt();
            var prompt2 = builder2.BuildSystemPrompt();

            // Assert
            Assert.That(prompt2, Is.EqualTo(prompt1));
        }

        #endregion

        #region Static BuildPromptForMode Tests

        [Test]
        public void BuildPromptForMode_Teach_MatchesInstanceBuild()
        {
            // Arrange
            var settings = new CapcomSettings { Mode = OperationMode.Teach };
            var builder = new PromptBuilder(settings);

            // Act
            var instancePrompt = builder.BuildSystemPrompt();
            var staticPrompt = PromptBuilder.BuildPromptForMode(OperationMode.Teach);

            // Assert
            Assert.That(staticPrompt, Is.EqualTo(instancePrompt));
        }

        [Test]
        public void BuildPromptForMode_Do_MatchesInstanceBuild()
        {
            // Arrange
            var settings = new CapcomSettings { Mode = OperationMode.Do };
            var builder = new PromptBuilder(settings);

            // Act
            var instancePrompt = builder.BuildSystemPrompt();
            var staticPrompt = PromptBuilder.BuildPromptForMode(OperationMode.Do);

            // Assert
            Assert.That(staticPrompt, Is.EqualTo(instancePrompt));
        }

        [Test]
        public void BuildPromptForMode_TeachAndDo_ProduceDifferentPrompts()
        {
            // Act
            var teachPrompt = PromptBuilder.BuildPromptForMode(OperationMode.Teach);
            var doPrompt = PromptBuilder.BuildPromptForMode(OperationMode.Do);

            // Assert
            Assert.That(doPrompt, Is.Not.EqualTo(teachPrompt));
        }

        #endregion

        #region GetCurrentMode Tests

        [Test]
        public void GetCurrentMode_TeachSettings_ReturnsTeach()
        {
            // Arrange
            var settings = new CapcomSettings { Mode = OperationMode.Teach };
            var builder = new PromptBuilder(settings);

            // Act
            var mode = builder.GetCurrentMode();

            // Assert
            Assert.That(mode, Is.EqualTo(OperationMode.Teach));
        }

        [Test]
        public void GetCurrentMode_DoSettings_ReturnsDo()
        {
            // Arrange
            var settings = new CapcomSettings { Mode = OperationMode.Do };
            var builder = new PromptBuilder(settings);

            // Act
            var mode = builder.GetCurrentMode();

            // Assert
            Assert.That(mode, Is.EqualTo(OperationMode.Do));
        }

        [Test]
        public void GetCurrentMode_NullSettings_ReturnsTeach()
        {
            // Arrange
            var builder = new PromptBuilder((CapcomSettings)null);

            // Act
            var mode = builder.GetCurrentMode();

            // Assert - should default to Teach when settings unavailable
            Assert.That(mode, Is.EqualTo(OperationMode.Teach));
        }

        [Test]
        public void GetCurrentMode_SettingsThrows_ReturnsTeach()
        {
            // Arrange
            var builder = new PromptBuilder(() => throw new Exception("Test"));

            // Act
            var mode = builder.GetCurrentMode();

            // Assert - should default to Teach on error
            Assert.That(mode, Is.EqualTo(OperationMode.Teach));
        }

        #endregion

        #region Fallback Prompt Tests

        [Test]
        public void FallbackPrompt_ContainsEssentialElements()
        {
            // Assert - fallback must still identify as CAPCOM
            Assert.That(PromptBuilder.FallbackPrompt, Does.Contain("CAPCOM"));

            // Assert - fallback must still contain no-autopilot constraint
            Assert.That(PromptBuilder.FallbackPrompt, Does.Contain("Do not pilot"));

            // Assert - fallback should mention kOS
            Assert.That(PromptBuilder.FallbackPrompt, Does.Contain("kOS"));

            // Assert - fallback should be reasonably short (it's a minimal prompt)
            Assert.That(PromptBuilder.FallbackPrompt.Length, Is.LessThan(500));
        }

        [Test]
        public void FallbackPrompt_IsNotEmpty()
        {
            Assert.That(PromptBuilder.FallbackPrompt, Is.Not.Null.And.Not.Empty);
        }

        #endregion

        #region Settings Provider Function Tests

        [Test]
        public void BuildSystemPrompt_UsesCurrentSettingsFromProvider()
        {
            // Arrange - mutable settings
            var settings = new CapcomSettings { Mode = OperationMode.Teach };
            var builder = new PromptBuilder(() => settings);

            // Act - first build in Teach mode
            var teachPrompt = builder.BuildSystemPrompt();

            // Change settings
            settings.Mode = OperationMode.Do;

            // Act - second build should use new mode
            var doPrompt = builder.BuildSystemPrompt();

            // Assert
            Assert.That(teachPrompt, Does.Contain("MODE: TEACH"));
            Assert.That(doPrompt, Does.Contain("MODE: DO"));
        }

        #endregion
    }
}
