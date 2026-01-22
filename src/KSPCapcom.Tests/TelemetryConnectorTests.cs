using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using KSPCapcom.LLM;

namespace KSPCapcom.Tests
{
    [TestFixture]
    public class TelemetryConnectorTests
    {
        private List<string> _loggedMessages;
        private Action<string> _originalLogAction;

        [SetUp]
        public void SetUp()
        {
            _loggedMessages = new List<string>();
            _originalLogAction = TelemetryConnector.LogAction;
            TelemetryConnector.LogAction = msg => _loggedMessages.Add(msg);
        }

        [TearDown]
        public void TearDown()
        {
            TelemetryConnector.LogAction = _originalLogAction;
        }

        #region GenerateRequestId Tests

        [Test]
        public void GenerateRequestId_Returns8Characters()
        {
            // Act
            var rid = TelemetryConnector.GenerateRequestId();

            // Assert
            Assert.That(rid.Length, Is.EqualTo(8));
        }

        [Test]
        public void GenerateRequestId_ReturnsHexCharactersOnly()
        {
            // Act
            var rid = TelemetryConnector.GenerateRequestId();

            // Assert
            Assert.That(rid, Does.Match("^[0-9a-f]{8}$"));
        }

        [Test]
        public void GenerateRequestId_ReturnsUniqueValues()
        {
            // Act
            var rid1 = TelemetryConnector.GenerateRequestId();
            var rid2 = TelemetryConnector.GenerateRequestId();
            var rid3 = TelemetryConnector.GenerateRequestId();

            // Assert
            Assert.That(rid1, Is.Not.EqualTo(rid2));
            Assert.That(rid2, Is.Not.EqualTo(rid3));
            Assert.That(rid1, Is.Not.EqualTo(rid3));
        }

        #endregion

        #region ClassifyOutcome Tests

        [Test]
        public void ClassifyOutcome_Success_ReturnsSuccessWithDash()
        {
            // Arrange
            var response = LLMResponse.Ok("Hello");

            // Act
            var (outcome, code) = TelemetryConnector.ClassifyOutcome(response);

            // Assert
            Assert.That(outcome, Is.EqualTo("success"));
            Assert.That(code, Is.EqualTo("-"));
        }

        [Test]
        public void ClassifyOutcome_Cancelled_ReturnsCancelledWithDash()
        {
            // Arrange
            var response = LLMResponse.Cancelled();

            // Act
            var (outcome, code) = TelemetryConnector.ClassifyOutcome(response);

            // Assert
            Assert.That(outcome, Is.EqualTo("cancelled"));
            Assert.That(code, Is.EqualTo("-"));
        }

        [Test]
        public void ClassifyOutcome_Timeout_ReturnsTimeoutWithCode()
        {
            // Arrange
            var response = LLMResponse.Fail(LLMError.Timeout());

            // Act
            var (outcome, code) = TelemetryConnector.ClassifyOutcome(response);

            // Assert
            Assert.That(outcome, Is.EqualTo("timeout"));
            Assert.That(code, Is.EqualTo("Timeout"));
        }

        [Test]
        public void ClassifyOutcome_Authentication_ReturnsFailedWithCode()
        {
            // Arrange
            var response = LLMResponse.Fail(LLMError.Authentication());

            // Act
            var (outcome, code) = TelemetryConnector.ClassifyOutcome(response);

            // Assert
            Assert.That(outcome, Is.EqualTo("failed"));
            Assert.That(code, Is.EqualTo("Authentication"));
        }

        [Test]
        public void ClassifyOutcome_RateLimit_ReturnsFailedWithCode()
        {
            // Arrange
            var response = LLMResponse.Fail(LLMError.RateLimit());

            // Act
            var (outcome, code) = TelemetryConnector.ClassifyOutcome(response);

            // Assert
            Assert.That(outcome, Is.EqualTo("failed"));
            Assert.That(code, Is.EqualTo("RateLimit"));
        }

        [Test]
        public void ClassifyOutcome_Network_ReturnsFailedWithCode()
        {
            // Arrange
            var response = LLMResponse.Fail(LLMError.Network());

            // Act
            var (outcome, code) = TelemetryConnector.ClassifyOutcome(response);

            // Assert
            Assert.That(outcome, Is.EqualTo("failed"));
            Assert.That(code, Is.EqualTo("Network"));
        }

        [Test]
        public void ClassifyOutcome_ServerError_ReturnsFailedWithCode()
        {
            // Arrange
            var response = LLMResponse.Fail(LLMError.ServerError());

            // Act
            var (outcome, code) = TelemetryConnector.ClassifyOutcome(response);

            // Assert
            Assert.That(outcome, Is.EqualTo("failed"));
            Assert.That(code, Is.EqualTo("ServerError"));
        }

        #endregion

        #region SendChatAsync Tests

        [Test]
        public async Task SendChatAsync_LogsStartBeforeInnerCall()
        {
            // Arrange
            var innerCallTime = DateTime.MinValue;
            var mockConnector = new MockConnector("TestBackend", () =>
            {
                innerCallTime = DateTime.Now;
                return LLMResponse.Ok("response");
            });
            var telemetryConnector = new TelemetryConnector(mockConnector);
            var options = new LLMRequestOptions { Model = "test-model" };

            // Act
            await telemetryConnector.SendChatAsync(
                new List<LLMMessage>(),
                options,
                CancellationToken.None);

            // Assert
            Assert.That(_loggedMessages.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(_loggedMessages[0], Does.Contain("TELEM|START"));
            Assert.That(_loggedMessages[0], Does.Contain("backend=TestBackend"));
            Assert.That(_loggedMessages[0], Does.Contain("model=test-model"));
        }

        [Test]
        public async Task SendChatAsync_LogsEndAfterResponse()
        {
            // Arrange
            var mockConnector = new MockConnector("TestBackend", () => LLMResponse.Ok("response"));
            var telemetryConnector = new TelemetryConnector(mockConnector);

            // Act
            await telemetryConnector.SendChatAsync(
                new List<LLMMessage>(),
                new LLMRequestOptions { Model = "test-model" },
                CancellationToken.None);

            // Assert
            Assert.That(_loggedMessages.Count, Is.EqualTo(2));
            Assert.That(_loggedMessages[1], Does.Contain("TELEM|END"));
            Assert.That(_loggedMessages[1], Does.Contain("outcome=success"));
            Assert.That(_loggedMessages[1], Does.Contain("code=-"));
        }

        [Test]
        public async Task SendChatAsync_CorrelatesToSameRequestId()
        {
            // Arrange
            var mockConnector = new MockConnector("TestBackend", () => LLMResponse.Ok("response"));
            var telemetryConnector = new TelemetryConnector(mockConnector);

            // Act
            await telemetryConnector.SendChatAsync(
                new List<LLMMessage>(),
                new LLMRequestOptions(),
                CancellationToken.None);

            // Assert
            var startMatch = Regex.Match(_loggedMessages[0], @"rid=([0-9a-f]{8})");
            var endMatch = Regex.Match(_loggedMessages[1], @"rid=([0-9a-f]{8})");

            Assert.That(startMatch.Success, Is.True, "START should contain rid");
            Assert.That(endMatch.Success, Is.True, "END should contain rid");
            Assert.That(startMatch.Groups[1].Value, Is.EqualTo(endMatch.Groups[1].Value),
                "START and END should have same rid");
        }

        [Test]
        public async Task SendChatAsync_CapturesDuration()
        {
            // Arrange
            var mockConnector = new MockConnector("TestBackend", () =>
            {
                Thread.Sleep(50); // Simulate some latency
                return LLMResponse.Ok("response");
            });
            var telemetryConnector = new TelemetryConnector(mockConnector);

            // Act
            await telemetryConnector.SendChatAsync(
                new List<LLMMessage>(),
                new LLMRequestOptions(),
                CancellationToken.None);

            // Assert
            var endMatch = Regex.Match(_loggedMessages[1], @"ms=(\d+)");
            Assert.That(endMatch.Success, Is.True, "END should contain ms");

            var ms = int.Parse(endMatch.Groups[1].Value);
            Assert.That(ms, Is.GreaterThanOrEqualTo(50), "Duration should be at least 50ms");
        }

        [Test]
        public async Task SendChatAsync_UsesDefaultModelWhenNotSpecified()
        {
            // Arrange
            var mockConnector = new MockConnector("TestBackend", () => LLMResponse.Ok("response"));
            var telemetryConnector = new TelemetryConnector(mockConnector);

            // Act
            await telemetryConnector.SendChatAsync(
                new List<LLMMessage>(),
                new LLMRequestOptions(), // No model specified
                CancellationToken.None);

            // Assert
            Assert.That(_loggedMessages[0], Does.Contain("model=default"));
        }

        [Test]
        public async Task SendChatAsync_LogsFailedOutcome()
        {
            // Arrange
            var mockConnector = new MockConnector("TestBackend",
                () => LLMResponse.Fail(LLMError.Authentication()));
            var telemetryConnector = new TelemetryConnector(mockConnector);

            // Act
            await telemetryConnector.SendChatAsync(
                new List<LLMMessage>(),
                new LLMRequestOptions(),
                CancellationToken.None);

            // Assert
            Assert.That(_loggedMessages[1], Does.Contain("outcome=failed"));
            Assert.That(_loggedMessages[1], Does.Contain("code=Authentication"));
        }

        [Test]
        public async Task SendChatAsync_LogsCancelledOutcome()
        {
            // Arrange
            var mockConnector = new MockConnector("TestBackend",
                () => LLMResponse.Cancelled());
            var telemetryConnector = new TelemetryConnector(mockConnector);

            // Act
            await telemetryConnector.SendChatAsync(
                new List<LLMMessage>(),
                new LLMRequestOptions(),
                CancellationToken.None);

            // Assert
            Assert.That(_loggedMessages[1], Does.Contain("outcome=cancelled"));
            Assert.That(_loggedMessages[1], Does.Contain("code=-"));
        }

        [Test]
        public async Task SendChatAsync_LogsTimeoutOutcome()
        {
            // Arrange
            var mockConnector = new MockConnector("TestBackend",
                () => LLMResponse.Fail(LLMError.Timeout()));
            var telemetryConnector = new TelemetryConnector(mockConnector);

            // Act
            await telemetryConnector.SendChatAsync(
                new List<LLMMessage>(),
                new LLMRequestOptions(),
                CancellationToken.None);

            // Assert
            Assert.That(_loggedMessages[1], Does.Contain("outcome=timeout"));
            Assert.That(_loggedMessages[1], Does.Contain("code=Timeout"));
        }

        #endregion

        #region Property Passthrough Tests

        [Test]
        public void Name_ReturnsInnerConnectorName()
        {
            // Arrange
            var mockConnector = new MockConnector("InnerName", () => LLMResponse.Ok(""));
            var telemetryConnector = new TelemetryConnector(mockConnector);

            // Assert
            Assert.That(telemetryConnector.Name, Is.EqualTo("InnerName"));
        }

        [Test]
        public void IsConfigured_ReturnsInnerConnectorValue()
        {
            // Arrange
            var configuredConnector = new MockConnector("Test", () => LLMResponse.Ok(""), isConfigured: true);
            var notConfiguredConnector = new MockConnector("Test", () => LLMResponse.Ok(""), isConfigured: false);

            var telemetryConfigured = new TelemetryConnector(configuredConnector);
            var telemetryNotConfigured = new TelemetryConnector(notConfiguredConnector);

            // Assert
            Assert.That(telemetryConfigured.IsConfigured, Is.True);
            Assert.That(telemetryNotConfigured.IsConfigured, Is.False);
        }

        [Test]
        public void Constructor_ThrowsOnNullInner()
        {
            // Assert
            Assert.Throws<ArgumentNullException>(() => new TelemetryConnector(null));
        }

        #endregion

        /// <summary>
        /// Mock connector for testing purposes.
        /// </summary>
        private class MockConnector : ILLMConnector
        {
            private readonly Func<LLMResponse> _responseFactory;

            public string Name { get; }
            public bool IsConfigured { get; }

            public MockConnector(string name, Func<LLMResponse> responseFactory, bool isConfigured = true)
            {
                Name = name;
                _responseFactory = responseFactory;
                IsConfigured = isConfigured;
            }

            public Task<LLMResponse> SendChatAsync(
                IReadOnlyList<LLMMessage> messages,
                LLMRequestOptions options,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(_responseFactory());
            }
        }
    }
}
