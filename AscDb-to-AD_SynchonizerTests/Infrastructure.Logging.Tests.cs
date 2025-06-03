using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Sentry.Extensions.Logging;
using System;
using System.Reflection;

namespace Infrastructure.Logging.Tests
{
    [TestFixture]
    public class SentryLoggerExtensionsTests
    {
        [Test]
        public void AddSentryLogger_ShouldNotThrow_WhenConfigIsNull()
        {
            // Arrange
            var serviceCollection = new ServiceCollection();
            var loggingBuilder = new Mock<ILoggingBuilder>();
            loggingBuilder.SetupGet(x => x.Services).Returns(serviceCollection);

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var result = SentryLoggerExtensions.AddSentryLogger(loggingBuilder.Object, null);
                Assert.AreEqual(loggingBuilder.Object, result);
            });
        }

        [Test]
        public void AddSentryLogger_ShouldRegisterServices()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();

            var loggingBuilder = new Mock<ILoggingBuilder>();
            loggingBuilder.SetupGet(x => x.Services).Returns(services);

            // Act
            loggingBuilder.Object.AddSentryLogger(options =>
            {
                options.Dsn = ""; 
            });

            // Assert
            var provider = services.BuildServiceProvider();
            var loggerProvider = provider.GetService<ILoggerProvider>();
            Assert.IsNotNull(loggerProvider, "LoggerProvider не был зарегистрирован");
        }

        [Test]
        public void AddSentryLogger_ShouldRegisterSentryOptions_WhenConfigProvided()
        {
            // Arrange
            var services = new ServiceCollection();
            var loggingBuilder = new Mock<ILoggingBuilder>();
            loggingBuilder.SetupGet(x => x.Services).Returns(services);

            string testDsn = "test_dsn_123";

            // Act
            loggingBuilder.Object.AddSentryLogger(options =>
            {
                options.Dsn = testDsn;
            });

            // Assert
            var serviceProvider = services.BuildServiceProvider();
            var sentryOptions = serviceProvider.GetService<IOptions<SentryLoggingOptions>>()?.Value;

            Assert.IsNotNull(sentryOptions, "SentryLoggingOptions не зарегистрированы");
            Assert.AreEqual(testDsn, sentryOptions.Dsn, "Dsn не был установлен");
        }
    }
}
