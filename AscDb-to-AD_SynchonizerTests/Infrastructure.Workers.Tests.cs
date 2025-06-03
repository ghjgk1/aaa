using NUnit.Framework;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Infrastructure.Workers;
using System.Threading;
using System.Threading.Tasks;
using System;
using Application;
using Domain;

namespace Infrastructure.Workers.Tests
{
    [TestFixture]
    public class SyncWorkerTests : IDisposable
    {
        private Mock<ILogger<SyncWorker>> _loggerMock;
        private Mock<SyncService> _syncServiceMock;
        private Mock<IHostApplicationLifetime> _appLifetimeMock;
        private SyncWorker _worker;
        private CancellationTokenSource _cts;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<SyncWorker>>();
            _syncServiceMock = new Mock<SyncService>(
                Mock.Of<ISyncRepository>(),
                Mock.Of<ISyncRepository>(),
                Mock.Of<ILogger<SyncService>>(),
                new Dictionary<string, string>(),
                "SamAccountName");
            _appLifetimeMock = new Mock<IHostApplicationLifetime>();
            _cts = new CancellationTokenSource();

            _worker = new SyncWorker(
                _loggerMock.Object,
                _syncServiceMock.Object,
                Array.Empty<string>(),
                _appLifetimeMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            Dispose();
        }

        public void Dispose()
        {
            _worker?.Dispose();
            _cts?.Dispose();
            GC.SuppressFinalize(this);
        }

        [Test]
        public async Task ExecuteAsync_ShouldRunOnce_WhenOnceFlagSpecified()
        {
            // Arrange
            var worker = new SyncWorker(
                _loggerMock.Object,
                _syncServiceMock.Object,
                new[] { "--once" },
                _appLifetimeMock.Object);

            // Act
            await worker.StartAsync(_cts.Token);
            await Task.Delay(100, _cts.Token);

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Worker started")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
        
        [Test]
        public async Task ExecuteAsync_ShouldHandleSyncErrors_Gracefully()
        {
            // Arrange
            _syncServiceMock.Setup(x => x.SyncUsersAsync(It.IsAny<bool>()))
                .ThrowsAsync(new Exception("Test error")); 

            // Act
            await _worker.StartAsync(_cts.Token);
            await Task.Delay(100); 

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Sync error")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once);
        }

        [Test]
        public async Task ExecuteAsync_ShouldStop_WhenCancellationRequested()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var callCount = 0;

            _syncServiceMock.Setup(x => x.SyncUsersAsync(It.IsAny<bool>()))
                .Callback(() =>
                {
                    callCount++;
                    cts.Cancel(); 
                })
                .Returns(Task.CompletedTask);

            var worker = new SyncWorker(
                _loggerMock.Object,
                _syncServiceMock.Object,
                new[] { "--once" }, 
                _appLifetimeMock.Object);

            // Act
            await worker.StartAsync(cts.Token);

            // Assert
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ExecuteAsync_ShouldExitLoop_WhenCancellationRequestedDuringDelay()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var callCount = 0;

            _syncServiceMock.Setup(x => x.SyncUsersAsync(It.IsAny<bool>()))
                .Callback(() => callCount++)
                .Returns(Task.CompletedTask);

            var worker = new SyncWorker(
                _loggerMock.Object,
                _syncServiceMock.Object,
                Array.Empty<string>(),
                _appLifetimeMock.Object);

            // Симулируем запрос отмены во время Delay
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                cts.Cancel();
            });

            // Act
            await worker.StartAsync(cts.Token);
            await Task.Delay(200); // Даем время для обработки

            // Assert
            Assert.That(callCount, Is.EqualTo(1));
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Starting sync cycle")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Test]
        public async Task ExecuteAsync_ShouldRunMultipleCycles_WhenNoCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            var callCount = 0;
            const int expectedCycles = 2; // Уменьшаем до 2 циклов для стабильности

            // Уменьшаем время задержки в моке SyncService для ускорения теста
            _syncServiceMock.Setup(x => x.SyncUsersAsync(It.IsAny<bool>()))
                .Callback(async () =>
                {
                    callCount++;
                    if (callCount >= expectedCycles)
                    {
                        await Task.Delay(100); // Даём время на корректное завершение
                        cts.Cancel();
                    }
                })
                .Returns(Task.CompletedTask);

            var worker = new SyncWorker(
                _loggerMock.Object,
                _syncServiceMock.Object,
                Array.Empty<string>(),
                _appLifetimeMock.Object)
            {
                // Подменяем время задержки между циклами для теста
                SyncInterval = TimeSpan.FromMilliseconds(50) // Уменьшаем с 5 минут до 50 мс
            };

            // Act
            await worker.StartAsync(cts.Token);
            await Task.Delay(500); // Увеличиваем общее время ожидания

            // Assert
            Assert.That(callCount, Is.GreaterThanOrEqualTo(expectedCycles),
                    $"Expected at least {expectedCycles} cycles, but got {callCount}");

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Starting sync cycle")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeast(expectedCycles));
        }
    }
}