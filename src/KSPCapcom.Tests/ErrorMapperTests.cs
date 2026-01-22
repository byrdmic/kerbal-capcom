using NUnit.Framework;
using KSPCapcom.LLM;

namespace KSPCapcom.Tests
{
    [TestFixture]
    public class ErrorMapperTests
    {
        #region ClassifyNetworkError Tests

        [Test]
        public void ClassifyNetworkError_DnsResolveError_ReturnsDnsResolutionFailed()
        {
            // Arrange
            var errorString = "Could not resolve host: invalid.local.host";

            // Act
            var result = ErrorMapper.ClassifyNetworkError(errorString);

            // Assert
            Assert.That(result, Is.EqualTo(LLMErrorType.DnsResolutionFailed));
        }

        [Test]
        public void ClassifyNetworkError_DnsKeyword_ReturnsDnsResolutionFailed()
        {
            // Arrange
            var errorString = "DNS lookup failed";

            // Act
            var result = ErrorMapper.ClassifyNetworkError(errorString);

            // Assert
            Assert.That(result, Is.EqualTo(LLMErrorType.DnsResolutionFailed));
        }

        [Test]
        public void ClassifyNetworkError_ConnectionRefused_ReturnsConnectionRefused()
        {
            // Arrange
            var errorString = "Connection refused";

            // Act
            var result = ErrorMapper.ClassifyNetworkError(errorString);

            // Assert
            Assert.That(result, Is.EqualTo(LLMErrorType.ConnectionRefused));
        }

        [Test]
        public void ClassifyNetworkError_HostUnreachable_ReturnsConnectionRefused()
        {
            // Arrange
            var errorString = "Host unreachable";

            // Act
            var result = ErrorMapper.ClassifyNetworkError(errorString);

            // Assert
            Assert.That(result, Is.EqualTo(LLMErrorType.ConnectionRefused));
        }

        [Test]
        public void ClassifyNetworkError_GenericNetworkError_ReturnsNetwork()
        {
            // Arrange
            var errorString = "Network is down";

            // Act
            var result = ErrorMapper.ClassifyNetworkError(errorString);

            // Assert
            Assert.That(result, Is.EqualTo(LLMErrorType.Network));
        }

        [Test]
        public void ClassifyNetworkError_NullString_ReturnsNetwork()
        {
            // Arrange
            string errorString = null;

            // Act
            var result = ErrorMapper.ClassifyNetworkError(errorString);

            // Assert
            Assert.That(result, Is.EqualTo(LLMErrorType.Network));
        }

        [Test]
        public void ClassifyNetworkError_EmptyString_ReturnsNetwork()
        {
            // Arrange
            var errorString = "";

            // Act
            var result = ErrorMapper.ClassifyNetworkError(errorString);

            // Assert
            Assert.That(result, Is.EqualTo(LLMErrorType.Network));
        }

        [Test]
        public void ClassifyNetworkError_CaseInsensitive_WorksWithUpperCase()
        {
            // Arrange
            var errorString = "COULD NOT RESOLVE DNS";

            // Act
            var result = ErrorMapper.ClassifyNetworkError(errorString);

            // Assert
            Assert.That(result, Is.EqualTo(LLMErrorType.DnsResolutionFailed));
        }

        #endregion

        #region GetUserFriendlyMessage Tests

        [Test]
        public void GetUserFriendlyMessage_NullError_ReturnsGenericMessage()
        {
            // Act
            var result = ErrorMapper.GetUserFriendlyMessage(null);

            // Assert
            Assert.That(result, Is.EqualTo("Unknown error occurred"));
        }

        [Test]
        public void GetUserFriendlyMessage_Authentication_ReturnsActionableMessage()
        {
            // Arrange
            var error = LLMError.Authentication();

            // Act
            var result = ErrorMapper.GetUserFriendlyMessage(error);

            // Assert
            Assert.That(result, Does.Contain("API key"));
            Assert.That(result, Does.Contain("Settings"));
        }

        [Test]
        public void GetUserFriendlyMessage_Authorization_ReturnsActionableMessage()
        {
            // Arrange
            var error = LLMError.Authorization();

            // Act
            var result = ErrorMapper.GetUserFriendlyMessage(error);

            // Assert
            Assert.That(result, Does.Contain("denied"));
            Assert.That(result, Does.Contain("permissions"));
        }

        [Test]
        public void GetUserFriendlyMessage_Network_ReturnsActionableMessage()
        {
            // Arrange
            var error = LLMError.Network();

            // Act
            var result = ErrorMapper.GetUserFriendlyMessage(error);

            // Assert
            Assert.That(result, Does.Contain("Network"));
            Assert.That(result, Does.Contain("internet"));
        }

        [Test]
        public void GetUserFriendlyMessage_DnsResolutionFailed_ReturnsActionableMessage()
        {
            // Arrange
            var error = LLMError.DnsResolutionFailed();

            // Act
            var result = ErrorMapper.GetUserFriendlyMessage(error);

            // Assert
            Assert.That(result, Does.Contain("hostname"));
            Assert.That(result, Does.Contain("endpoint"));
        }

        [Test]
        public void GetUserFriendlyMessage_ConnectionRefused_ReturnsActionableMessage()
        {
            // Arrange
            var error = LLMError.ConnectionRefused();

            // Act
            var result = ErrorMapper.GetUserFriendlyMessage(error);

            // Assert
            Assert.That(result, Does.Contain("connect"));
            Assert.That(result, Does.Contain("endpoint") | Does.Contain("server"));
        }

        [Test]
        public void GetUserFriendlyMessage_ServerError_ReturnsActionableMessage()
        {
            // Arrange
            var error = LLMError.ServerError();

            // Act
            var result = ErrorMapper.GetUserFriendlyMessage(error);

            // Assert
            Assert.That(result, Does.Contain("server error"));
            Assert.That(result, Does.Contain("retry"));
        }

        [Test]
        public void GetUserFriendlyMessage_Timeout_ReturnsActionableMessage()
        {
            // Arrange
            var error = LLMError.Timeout();

            // Act
            var result = ErrorMapper.GetUserFriendlyMessage(error);

            // Assert
            Assert.That(result, Does.Contain("timed out"));
        }

        [Test]
        public void GetUserFriendlyMessage_RateLimit_IncludesRetryDelay()
        {
            // Arrange
            var error = LLMError.RateLimit(10000);

            // Act
            var result = ErrorMapper.GetUserFriendlyMessage(error);

            // Assert
            Assert.That(result, Does.Contain("Rate limited"));
            Assert.That(result, Does.Contain("10"));
        }

        [Test]
        public void GetUserFriendlyMessage_AllErrorTypes_ReturnNonEmptyStrings()
        {
            // Test that all error types produce non-empty messages
            var errorTypes = new[]
            {
                LLMError.Authentication(),
                LLMError.Authorization(),
                LLMError.Network(),
                LLMError.DnsResolutionFailed(),
                LLMError.ConnectionRefused(),
                LLMError.Timeout(),
                LLMError.ServerError(),
                LLMError.RateLimit(),
                LLMError.NotConfigured(),
                LLMError.InvalidRequest(),
                LLMError.ModelNotFound(),
                LLMError.ContextLengthExceeded(),
                LLMError.ContentFiltered(),
                LLMError.Cancelled(),
                LLMError.Unknown("Test error")
            };

            foreach (var error in errorTypes)
            {
                // Act
                var result = ErrorMapper.GetUserFriendlyMessage(error);

                // Assert
                Assert.That(result, Is.Not.Null.And.Not.Empty,
                    $"Error type {error.Type} produced null or empty message");
            }
        }

        #endregion

        #region IsRetryable Tests

        [Test]
        public void IsRetryable_DnsResolutionFailed_IsRetryable()
        {
            // Arrange
            var error = LLMError.DnsResolutionFailed();

            // Assert
            Assert.That(error.IsRetryable, Is.True);
        }

        [Test]
        public void IsRetryable_ConnectionRefused_IsRetryable()
        {
            // Arrange
            var error = LLMError.ConnectionRefused();

            // Assert
            Assert.That(error.IsRetryable, Is.True);
        }

        [Test]
        public void IsRetryable_Network_IsRetryable()
        {
            // Arrange
            var error = LLMError.Network();

            // Assert
            Assert.That(error.IsRetryable, Is.True);
        }

        [Test]
        public void IsRetryable_ServerError_IsRetryable()
        {
            // Arrange
            var error = LLMError.ServerError();

            // Assert
            Assert.That(error.IsRetryable, Is.True);
        }

        [Test]
        public void IsRetryable_Authentication_NotRetryable()
        {
            // Arrange
            var error = LLMError.Authentication();

            // Assert
            Assert.That(error.IsRetryable, Is.False);
        }

        [Test]
        public void IsRetryable_Authorization_NotRetryable()
        {
            // Arrange
            var error = LLMError.Authorization();

            // Assert
            Assert.That(error.IsRetryable, Is.False);
        }

        #endregion

        #region SuggestedRetryDelayMs Tests

        [Test]
        public void SuggestedRetryDelayMs_DnsResolutionFailed_Is5000()
        {
            // Arrange
            var error = LLMError.DnsResolutionFailed();

            // Assert
            Assert.That(error.SuggestedRetryDelayMs, Is.EqualTo(5000));
        }

        [Test]
        public void SuggestedRetryDelayMs_ConnectionRefused_Is3000()
        {
            // Arrange
            var error = LLMError.ConnectionRefused();

            // Assert
            Assert.That(error.SuggestedRetryDelayMs, Is.EqualTo(3000));
        }

        #endregion
    }
}
