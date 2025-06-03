using NUnit.Framework;
using Moq;
using Microsoft.Extensions.Logging;
using Domain;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace Infrastructure.Data.Tests
{
    [TestFixture]
    public class EfSyncRepositoryTests : IDisposable
    {
        private EfSyncRepository _repository;
        private AscDbContext _dbContext;
        private Mock<ILogger<EfSyncRepository>> _loggerMock;
        private SqliteConnection _connection;

        [SetUp]
        public void Setup()
        {
            // Создаем и открываем соединение SQLite in-memory
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<AscDbContext>()
                .UseSqlite(_connection)
                .Options;

            _dbContext = new AscDbContext(options);
            _dbContext.Database.EnsureCreated();

            _loggerMock = new Mock<ILogger<EfSyncRepository>>();
            _repository = new EfSyncRepository(_dbContext, _loggerMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            Dispose();
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
            _connection?.Close();
            _connection?.Dispose();
        }

        [Test]
        public async Task GetUsersFromSourceAsync_ShouldReturnUsers_WhenDbOperationSucceeds()
        {
            // Arrange
            _dbContext.Users.Add(new User
            {
                SamAccountName = "john.doe",
                EmployeeId = "EMP1001",
                FirstName = "John",
                LastName = "Doe",
                MiddleName = "William",
                FullName = "John William Doe",
                JobTitle = "Senior Software Developer",
                Department = "IT Department",
                InternalPhone = "x1234",
                MobilePhone = "+1234567890",
                AdditionalPhone = "+7234567890",
                Email = "john.doe@company.com",
                HireDate = new DateTime(2020, 5, 15)
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _repository.GetUsersFromSourceAsync();

            // Assert
            Assert.AreEqual(1, result.Count());
            Assert.AreEqual("john.doe", result.First().SamAccountName);
            Assert.AreEqual("EMP1001", result.First().EmployeeId); 
            Assert.AreEqual("John", result.First().FirstName);
            Assert.AreEqual("Doe", result.First().LastName);
            Assert.AreEqual("William", result.First().MiddleName);
            Assert.AreEqual("John William Doe", result.First().FullName); 
            Assert.AreEqual("Senior Software Developer", result.First().JobTitle);
            Assert.AreEqual("IT Department", result.First().Department);
            Assert.AreEqual("x1234", result.First().InternalPhone);
            Assert.AreEqual("+1234567890", result.First().MobilePhone);
            Assert.AreEqual("+7234567890", result.First().AdditionalPhone);
            Assert.AreEqual("john.doe@company.com", result.First().Email); 
            Assert.AreEqual(new DateTime(2020, 5, 15), result.First().HireDate);
        }

        [Test]
        public void GetUsersFromSourceAsync_ShouldLogError_WhenDbOperationFails()
        {
            // Arrange
            var failingContext = new FailingDbContext();
            var testRepo = new EfSyncRepository(failingContext, _loggerMock.Object);

            // Act & Assert
            Assert.ThrowsAsync<Exception>(() => testRepo.GetUsersFromSourceAsync());
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        private class FailingDbContext : AscDbContext
        {
            public FailingDbContext()
                : base(new DbContextOptionsBuilder<AscDbContext>()
                    .UseSqlite("DataSource=:memory:")
                    .Options)
            {
            }

            public override DbSet<User> Users => throw new Exception("Database error");
        }
    }
}