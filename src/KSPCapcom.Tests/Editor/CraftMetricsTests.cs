using System.Collections.Generic;
using NUnit.Framework;
using KSPCapcom.Editor;

namespace KSPCapcom.Tests.Editor
{
    /// <summary>
    /// Tests for ReadinessMetrics.ToJsonBlock() method.
    /// These tests verify the structured JSON output for LLM context injection.
    /// </summary>
    [TestFixture]
    public class ReadinessMetricsJsonTests
    {
        #region ToJsonBlock Tests

        [Test]
        public void ToJsonBlock_WithValidTWR_ContainsTWRData()
        {
            // Arrange
            var twr = new TWRAnalysis(1.82f, 1.45f, 3, true);
            var deltaV = new DeltaVEstimate(4200f);
            var control = new ControlAuthorityCheck(ControlAuthorityStatus.Good, 1, 2, 0);
            var staging = new StagingValidation(new List<string>());
            var metrics = new ReadinessMetrics(twr, deltaV, control, staging);

            // Act
            var json = metrics.ToJsonBlock("editor", 3);

            // Assert
            Assert.That(json, Does.Contain("\"context\":\"editor\""));
            Assert.That(json, Does.Contain("\"twr\":"));
            Assert.That(json, Does.Contain("\"asl\":1.45"));
            Assert.That(json, Does.Contain("\"vacuum\":1.82"));
            Assert.That(json, Does.Contain("\"engineCount\":3"));
            Assert.That(json, Does.Contain("\"canLaunchFromKerbin\":true"));
        }

        [Test]
        public void ToJsonBlock_WithUnavailableTWR_ContainsTWRNull()
        {
            // Arrange
            var metrics = ReadinessMetrics.NotAvailable;

            // Act
            var json = metrics.ToJsonBlock("editor", 0);

            // Assert
            Assert.That(json, Does.Contain("\"twr\":null"));
        }

        [Test]
        public void ToJsonBlock_WithValidDeltaV_ContainsDeltaVData()
        {
            // Arrange
            var twr = TWRAnalysis.NotAvailable;
            var deltaV = new DeltaVEstimate(4200f);
            var control = ControlAuthorityCheck.NotAvailable;
            var staging = StagingValidation.Empty;
            var metrics = new ReadinessMetrics(twr, deltaV, control, staging);

            // Act
            var json = metrics.ToJsonBlock("editor", 3);

            // Assert
            Assert.That(json, Does.Contain("\"deltaV\":"));
            Assert.That(json, Does.Contain("\"total\":4200"));
            Assert.That(json, Does.Contain("\"stageCount\":3"));
            Assert.That(json, Does.Contain("\"perStage\":null"));
        }

        [Test]
        public void ToJsonBlock_WithUnavailableDeltaV_ContainsDeltaVNull()
        {
            // Arrange
            var metrics = ReadinessMetrics.NotAvailable;

            // Act
            var json = metrics.ToJsonBlock("editor", 0);

            // Assert
            Assert.That(json, Does.Contain("\"deltaV\":null"));
        }

        [Test]
        public void ToJsonBlock_WithControlAuthorityGood_ContainsGoodStatus()
        {
            // Arrange
            var control = new ControlAuthorityCheck(ControlAuthorityStatus.Good, 1, 2, 1);
            var metrics = new ReadinessMetrics(
                TWRAnalysis.NotAvailable,
                DeltaVEstimate.NotAvailable,
                control,
                StagingValidation.Empty);

            // Act
            var json = metrics.ToJsonBlock("editor", 0);

            // Assert
            Assert.That(json, Does.Contain("\"controlAuthority\":\"good\""));
        }

        [Test]
        public void ToJsonBlock_WithControlAuthorityMarginal_ContainsMarginalStatus()
        {
            // Arrange
            var control = new ControlAuthorityCheck(ControlAuthorityStatus.Marginal, 1, 0, 0);
            var metrics = new ReadinessMetrics(
                TWRAnalysis.NotAvailable,
                DeltaVEstimate.NotAvailable,
                control,
                StagingValidation.Empty);

            // Act
            var json = metrics.ToJsonBlock("editor", 0);

            // Assert
            Assert.That(json, Does.Contain("\"controlAuthority\":\"marginal\""));
        }

        [Test]
        public void ToJsonBlock_WithControlAuthorityNone_ContainsNoneStatus()
        {
            // Arrange
            var control = new ControlAuthorityCheck(ControlAuthorityStatus.None, 0, 0, 0);
            var metrics = new ReadinessMetrics(
                TWRAnalysis.NotAvailable,
                DeltaVEstimate.NotAvailable,
                control,
                StagingValidation.Empty);

            // Act
            var json = metrics.ToJsonBlock("editor", 0);

            // Assert
            Assert.That(json, Does.Contain("\"controlAuthority\":\"none\""));
        }

        [Test]
        public void ToJsonBlock_WithStagingWarnings_ContainsWarningsArray()
        {
            // Arrange
            var warnings = new List<string> { "Decoupler before engine", "Missing parachute" };
            var staging = new StagingValidation(warnings);
            var metrics = new ReadinessMetrics(
                TWRAnalysis.NotAvailable,
                DeltaVEstimate.NotAvailable,
                ControlAuthorityCheck.NotAvailable,
                staging);

            // Act
            var json = metrics.ToJsonBlock("editor", 0);

            // Assert
            Assert.That(json, Does.Contain("\"stagingWarnings\":["));
            Assert.That(json, Does.Contain("\"Decoupler before engine\""));
            Assert.That(json, Does.Contain("\"Missing parachute\""));
        }

        [Test]
        public void ToJsonBlock_WithNoWarnings_ContainsEmptyArray()
        {
            // Arrange
            var metrics = ReadinessMetrics.NotAvailable;

            // Act
            var json = metrics.ToJsonBlock("editor", 0);

            // Assert
            Assert.That(json, Does.Contain("\"stagingWarnings\":[]"));
        }

        [Test]
        public void ToJsonBlock_WithFlightContext_ContainsFlightContext()
        {
            // Arrange
            var metrics = ReadinessMetrics.NotAvailable;

            // Act
            var json = metrics.ToJsonBlock("flight", 0);

            // Assert
            Assert.That(json, Does.Contain("\"context\":\"flight\""));
        }

        [Test]
        public void ToJsonBlock_EscapesSpecialCharactersInWarnings()
        {
            // Arrange
            var warnings = new List<string> { "Warning with \"quotes\"", "Line\nbreak" };
            var staging = new StagingValidation(warnings);
            var metrics = new ReadinessMetrics(
                TWRAnalysis.NotAvailable,
                DeltaVEstimate.NotAvailable,
                ControlAuthorityCheck.NotAvailable,
                staging);

            // Act
            var json = metrics.ToJsonBlock("editor", 0);

            // Assert - should have escaped quotes and newlines
            Assert.That(json, Does.Contain("\\\"quotes\\\""));
            Assert.That(json, Does.Contain("\\n"));
        }

        [Test]
        public void ToJsonBlock_CanLaunchFromKerbinFalse_ContainsFalse()
        {
            // Arrange - TWR below 1.0 at sea level
            var twr = new TWRAnalysis(0.95f, 0.85f, 1, false);
            var metrics = new ReadinessMetrics(
                twr,
                DeltaVEstimate.NotAvailable,
                ControlAuthorityCheck.NotAvailable,
                StagingValidation.Empty);

            // Act
            var json = metrics.ToJsonBlock("editor", 0);

            // Assert
            Assert.That(json, Does.Contain("\"canLaunchFromKerbin\":false"));
        }

        #endregion
    }

    // Note: EngineSummaryAggregate and EngineSummary tests cannot be included here
    // because they depend on EditorCraftSnapshot which has KSP/Unity dependencies.
    // These classes will be tested through integration tests with KSP_PRESENT defined.

    [TestFixture]
    public class TWRAnalysisTests
    {
        [Test]
        public void NotAvailable_IsAvailableIsFalse()
        {
            // Assert
            Assert.That(TWRAnalysis.NotAvailable.IsAvailable, Is.False);
        }

        [Test]
        public void Constructor_SetsIsAvailableTrue()
        {
            // Arrange
            var twr = new TWRAnalysis(1.5f, 1.2f, 2, true);

            // Assert
            Assert.That(twr.IsAvailable, Is.True);
            Assert.That(twr.VacuumTWR, Is.EqualTo(1.5f));
            Assert.That(twr.AtmosphericTWR, Is.EqualTo(1.2f));
            Assert.That(twr.EngineCount, Is.EqualTo(2));
            Assert.That(twr.CanLaunchFromKerbin, Is.True);
        }

        [Test]
        public void ToPromptLine_NotAvailable_ReturnsNA()
        {
            // Act
            var line = TWRAnalysis.NotAvailable.ToPromptLine();

            // Assert
            Assert.That(line, Is.EqualTo("TWR: N/A"));
        }

        [Test]
        public void ToPromptLine_Available_ContainsTWRValues()
        {
            // Arrange
            var twr = new TWRAnalysis(1.8f, 1.45f, 3, true);

            // Act
            var line = twr.ToPromptLine();

            // Assert
            Assert.That(line, Does.Contain("1.45"));
            Assert.That(line, Does.Contain("1.80"));
            Assert.That(line, Does.Contain("3 engines"));
        }

        [Test]
        public void ToPromptLine_LowTWR_ContainsWarning()
        {
            // Arrange - TWR below 1.0 at sea level
            var twr = new TWRAnalysis(0.9f, 0.7f, 1, false);

            // Act
            var line = twr.ToPromptLine();

            // Assert
            Assert.That(line, Does.Contain("WARN"));
            Assert.That(line, Does.Contain("TWR < 1.0"));
        }
    }

    [TestFixture]
    public class DeltaVEstimateTests
    {
        [Test]
        public void NotAvailable_IsAvailableIsFalse()
        {
            // Assert
            Assert.That(DeltaVEstimate.NotAvailable.IsAvailable, Is.False);
        }

        [Test]
        public void Constructor_SetsIsAvailableTrue()
        {
            // Arrange
            var deltaV = new DeltaVEstimate(4200f);

            // Assert
            Assert.That(deltaV.IsAvailable, Is.True);
            Assert.That(deltaV.TotalDeltaV, Is.EqualTo(4200f));
        }

        [Test]
        public void ToPromptLine_NotAvailable_ReturnsNA()
        {
            // Act
            var line = DeltaVEstimate.NotAvailable.ToPromptLine();

            // Assert
            Assert.That(line, Does.Contain("N/A"));
        }

        [Test]
        public void ToPromptLine_Available_ContainsDeltaVValue()
        {
            // Arrange
            var deltaV = new DeltaVEstimate(4200f);

            // Act
            var line = deltaV.ToPromptLine();

            // Assert
            Assert.That(line, Does.Contain("4200"));
            Assert.That(line, Does.Contain("m/s"));
        }
    }

    [TestFixture]
    public class ControlAuthorityCheckTests
    {
        [Test]
        public void NotAvailable_IsAvailableIsFalse()
        {
            // Assert
            Assert.That(ControlAuthorityCheck.NotAvailable.IsAvailable, Is.False);
        }

        [Test]
        public void ToPromptLine_Good_ReturnsOK()
        {
            // Arrange
            var control = new ControlAuthorityCheck(ControlAuthorityStatus.Good, 1, 2, 1);

            // Act
            var line = control.ToPromptLine();

            // Assert
            Assert.That(line, Does.Contain("OK"));
        }

        [Test]
        public void ToPromptLine_Marginal_ReturnsMarginal()
        {
            // Arrange
            var control = new ControlAuthorityCheck(ControlAuthorityStatus.Marginal, 1, 0, 0);

            // Act
            var line = control.ToPromptLine();

            // Assert
            Assert.That(line, Does.Contain("MARGINAL"));
        }

        [Test]
        public void ToPromptLine_None_ReturnsWarning()
        {
            // Arrange
            var control = new ControlAuthorityCheck(ControlAuthorityStatus.None, 0, 0, 0);

            // Act
            var line = control.ToPromptLine();

            // Assert
            Assert.That(line, Does.Contain("WARN"));
            Assert.That(line, Does.Contain("No command pod"));
        }
    }

    [TestFixture]
    public class StagingValidationTests
    {
        [Test]
        public void Empty_HasNoWarnings()
        {
            // Assert
            Assert.That(StagingValidation.Empty.HasWarnings, Is.False);
            Assert.That(StagingValidation.Empty.Warnings.Count, Is.EqualTo(0));
        }

        [Test]
        public void WithWarnings_HasWarningsIsTrue()
        {
            // Arrange
            var warnings = new List<string> { "Warning 1", "Warning 2" };
            var staging = new StagingValidation(warnings);

            // Assert
            Assert.That(staging.HasWarnings, Is.True);
            Assert.That(staging.Warnings.Count, Is.EqualTo(2));
        }

        [Test]
        public void ToPromptLine_NoWarnings_ReturnsOK()
        {
            // Act
            var line = StagingValidation.Empty.ToPromptLine();

            // Assert
            Assert.That(line, Does.Contain("OK"));
        }

        [Test]
        public void ToPromptLine_WithWarnings_ContainsWarnings()
        {
            // Arrange
            var warnings = new List<string> { "Decoupler issue", "Missing parachute" };
            var staging = new StagingValidation(warnings);

            // Act
            var line = staging.ToPromptLine();

            // Assert
            Assert.That(line, Does.Contain("WARNINGS"));
            Assert.That(line, Does.Contain("Decoupler issue"));
            Assert.That(line, Does.Contain("Missing parachute"));
        }
    }
}
