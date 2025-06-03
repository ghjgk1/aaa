using Novell.Directory.Ldap;
using NUnit.Framework;
using NSubstitute;
using Moq;
using Microsoft.Extensions.Logging;
using Domain;
using System.DirectoryServices;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;
using System.Runtime.Serialization;
using System.Collections;
using System.Collections.Specialized;
using Moq.Protected;
using static Infrastructure.Directory.LdapSyncRepository;
using Infrastructure.Directory.Models;
using System.Net;
using Application.Interfaces;
using System.DirectoryServices.AccountManagement;


namespace Infrastructure.Directory.Tests
{
    [TestFixture]
    public class LdapSyncRepositoryTests
    {
        private Mock<IDirectoryService> _directoryServiceMock;
        private Mock<ILogger<LdapSyncRepository>> _loggerMock;
        private Dictionary<string, string> _fieldMappings;
        private LdapSyncRepository _repository;

        // Тестовая реализация IPropertyCollection
        private class TestPropertyCollection : IPropertyCollection
        {
            private readonly Dictionary<string, object> _properties = new Dictionary<string, object>();

            public object? this[string propertyName]
            {
                get => _properties.TryGetValue(propertyName, out var value) ? value : null;
                set => _properties[propertyName] = value!;
            }

            public bool Contains(string propertyName) => _properties.ContainsKey(propertyName);

            public object? ValueOrDefault(string propertyName) =>
                _properties.TryGetValue(propertyName, out var value) ? value : null;

            public void Add(string propertyName, object value) => _properties[propertyName] = value;
        }

        [SetUp]
        public void Setup()
        {
            _directoryServiceMock = new Mock<IDirectoryService>();
            _loggerMock = new Mock<ILogger<LdapSyncRepository>>();
            _fieldMappings = new Dictionary<string, string>
            {
                { "samAccountName", "SamAccountName" },
                { "mail", "Email" },
                { "givenName", "FirstName" },
                { "info", "HireDate" }
            };

            _repository = new LdapSyncRepository(
                "LDAP://test",
                "username",
                "password",
                _fieldMappings,
                _loggerMock.Object,
                _directoryServiceMock.Object);
        }

        [Test]
        public void FindUserInTargetAsync_WhenSearcherThrowsException_ThrowsAndLogsError()
        {
            // Arrange
            _directoryServiceMock.Setup(x => x.CreateSearcher(It.IsAny<IDirectoryEntry>()))
                .Throws(new Exception("Test exception"));

            // Act & Assert
            Assert.ThrowsAsync<Exception>(() => _repository.FindUserInTargetAsync("test"));

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error finding user in AD")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ));
        }

        [Test]
        public void UpdateUserInTargetAsync_WhenCommitChangesThrowsException_ThrowsAndLogsError()
        {
            // Arrange
            var properties = new TestPropertyCollection();
            properties.Add("samAccountName", "test");

            var entryMock = new Mock<IDirectoryEntry>();
            entryMock.Setup(x => x.Properties).Returns(properties);
            entryMock.Setup(x => x.CommitChanges()).Throws(new Exception("Commit failed"));

            var searchResultMock = new Mock<IDirectorySearchResult>();
            searchResultMock.Setup(x => x.GetDirectoryEntry()).Returns(entryMock.Object);

            var searcherMock = new Mock<IDirectorySearcher>();
            searcherMock.Setup(x => x.FindOne()).Returns(searchResultMock.Object);

            _directoryServiceMock.Setup(x => x.CreateSearcher(It.IsAny<IDirectoryEntry>()))
                .Returns(searcherMock.Object);

            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(() =>
                _repository.UpdateUserInTargetAsync(new User { SamAccountName = "test" }));

            Assert.AreEqual("Commit failed", exception.Message);

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error updating user in AD")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ));
        }

        [Test]
        public async Task FindUserInTargetAsync_UserExists_ReturnsMappedUser()
        {
            // Arrange
            var testSamAccountName = "test.user";
            var testEmail = "test.user@example.com";
            var testFirstName = "Test User";
            var testHireDate = "2000-01-01";

            var properties = new TestPropertyCollection();
            properties.Add("samAccountName", testSamAccountName);
            properties.Add("mail", testEmail);
            properties.Add("givenName", testFirstName);
            properties.Add("info", testHireDate);

            var searchResultMock = new Mock<IDirectorySearchResult>();
            searchResultMock.Setup(x => x.Properties).Returns(properties);

            var searcherMock = new Mock<IDirectorySearcher>();
            searcherMock.Setup(x => x.FindOne()).Returns(searchResultMock.Object);

            var entryMock = new Mock<IDirectoryEntry>();
            _directoryServiceMock.Setup(x => x.CreateSearcher(It.IsAny<IDirectoryEntry>()))
                .Returns(searcherMock.Object);

            // Act
            var result = await _repository.FindUserInTargetAsync(testSamAccountName);

            // Assert
            Assert.NotNull(result);
            Assert.AreEqual(testSamAccountName, result.SamAccountName);
            Assert.AreEqual(testEmail, result.Email);
            Assert.AreEqual(testFirstName, result.FirstName);
            Assert.AreEqual(new DateTime(2000, 1, 1), result.HireDate);
        }

        [Test]
        public async Task FindUserInTargetAsync_UserNotExists_ReturnsNull()
        {
            // Arrange
            var searcherMock = new Mock<IDirectorySearcher>();
            searcherMock.Setup(x => x.FindOne()).Returns((IDirectorySearchResult)null);

            _directoryServiceMock.Setup(x => x.CreateSearcher(It.IsAny<IDirectoryEntry>()))
                .Returns(searcherMock.Object);

            // Act
            var result = await _repository.FindUserInTargetAsync("nonexistent.user");

            // Assert
            Assert.IsNull(result);

        }

        [Test]
        public async Task UpdateUserInTargetAsync_UserExists_UpdatesAllMappedProperties()
        {
            // Arrange
            var testUser = new User
            {
                SamAccountName = "test.user",
                Email = "new.email@example.com",
                FirstName = "New Name",
                HireDate = new DateTime(1990, 1, 1)
            };

            var properties = new TestPropertyCollection();
            properties.Add("samAccountName", "test.user");
            properties.Add("mail", "old.email@example.com");
            properties.Add("givenName", "Old Name");
            properties.Add("info", "BirthDate:2000-01-01");

            var entryMock = new Mock<IDirectoryEntry>();
            entryMock.Setup(x => x.Properties).Returns(properties);

            var searchResultMock = new Mock<IDirectorySearchResult>();
            searchResultMock.Setup(x => x.GetDirectoryEntry()).Returns(entryMock.Object);

            var searcherMock = new Mock<IDirectorySearcher>();
            searcherMock.Setup(x => x.FindOne()).Returns(searchResultMock.Object);

            _directoryServiceMock.Setup(x => x.CreateSearcher(It.IsAny<IDirectoryEntry>()))
                .Returns(searcherMock.Object);

            // Act
            await _repository.UpdateUserInTargetAsync(testUser);

            // Assert
            Assert.AreEqual("new.email@example.com", properties["mail"]);
            Assert.AreEqual("New Name", properties["givenName"]);
            Assert.AreEqual("BirthDate:1990-01-01", properties["info"]);
            entryMock.Verify(x => x.CommitChanges(), Times.Once);
        }

        [Test]
        public async Task UpdateUserInTargetAsync_UserNotExists_LogsWarning()
        {
            // Arrange
            var testUser = new User { SamAccountName = "nonexistent.user" };

            var searcherMock = new Mock<IDirectorySearcher>();
            searcherMock.Setup(x => x.FindOne()).Returns((IDirectorySearchResult)null);

            _directoryServiceMock.Setup(x => x.CreateSearcher(It.IsAny<IDirectoryEntry>()))
                .Returns(searcherMock.Object);

            // Act
            await _repository.UpdateUserInTargetAsync(testUser);

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"User {testUser.SamAccountName} not found in AD")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ));
        }

        [Test]
        public async Task UpdateUserInTargetAsync_ProtectedAttributes_NotUpdated()
        {
            // Arrange
            var testUser = new User
            {
                SamAccountName = "test.user",
                Email = "new.email@example.com"
            };

            var properties = new TestPropertyCollection();
            properties.Add("samAccountName", "test.user");
            properties.Add("mail", "old.email@example.com");

            var entryMock = new Mock<IDirectoryEntry>();
            entryMock.Setup(x => x.Properties).Returns(properties);

            var searchResultMock = new Mock<IDirectorySearchResult>();
            searchResultMock.Setup(x => x.GetDirectoryEntry()).Returns(entryMock.Object);

            var searcherMock = new Mock<IDirectorySearcher>();
            searcherMock.Setup(x => x.FindOne()).Returns(searchResultMock.Object);

            _directoryServiceMock.Setup(x => x.CreateSearcher(It.IsAny<IDirectoryEntry>()))
                .Returns(searcherMock.Object);

            // Act
            await _repository.UpdateUserInTargetAsync(testUser);

            // Assert
            Assert.AreEqual("test.user", properties["samAccountName"]); // Не должно измениться
            Assert.AreEqual("new.email@example.com", properties["mail"]); // Должно измениться
        }


        [Test]
        public void FormatAttributeValue_ShouldFormatDate_ForInfoAttribute()
        {
            // Arrange
            var dateStr = "2023-01-15";

            // Act
            var result = _repository.FormatAttributeValue("info", dateStr);

            // Assert
            Assert.That(result, Does.StartWith("BirthDate:"));
        }

        [Test]
        public void IsProtectedAttribute_ShouldReturnTrue_ForProtectedAttributes()
        {
            Assert.IsTrue(_repository.IsProtectedAttribute("userPrincipalName"));
            Assert.IsTrue(_repository.IsProtectedAttribute("sAMAccountName"));
        }

        [Test]
        public void IsValidAttributeValue_ShouldValidateAllAttributes()
        {
            Assert.IsTrue(_repository.IsValidAttributeValue("mail", "valid@example.com"));
            Assert.IsFalse(_repository.IsValidAttributeValue("mail", "invalid-email"));
        
            Assert.IsTrue(_repository.IsValidAttributeValue("telephoneNumber", "123456789"));
            Assert.IsFalse(_repository.IsValidAttributeValue("telephoneNumber", "123456789123456789123456789123456789"));
        
            Assert.IsTrue(_repository.IsValidAttributeValue("mobile", "123456789"));
            Assert.IsFalse(_repository.IsValidAttributeValue("mobile", "123456789123456789123456789123456789"));
        
            Assert.IsTrue(_repository.IsValidAttributeValue("employeeID", "12"));
            Assert.IsFalse(_repository.IsValidAttributeValue("employeeID", " ")); 
        }
    }

    [TestFixture]
    [Category("Integration")]
    public class LdapSyncRepositoryIntegrationTests
    {
        private LdapSyncRepository _repository;
        private Dictionary<string, string> _fieldMappings;
        private ILogger<LdapSyncRepository> _logger;
        private const string domain = "testlab.local";
        private const string adminUser = "TESTLAB\\administrator";
        private const string adminPassword = "Qwerty1!";
        private const string ouPath = $"LDAP://OU=Employees,DC=testlab,DC=local";
        private const string _testSamAccountName = "testuser";
        private const string password  = "TempP@ss123!";

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _logger = Mock.Of<ILogger<LdapSyncRepository>>();

            _fieldMappings = new Dictionary<string, string>
            {
                { "samAccountName", "SamAccountName" },
                { "mail", "Email" },
                { "givenName", "FirstName" },
                { "sn", "LastName" },
                { "info", "HireDate" }
            };

            _repository = new LdapSyncRepository(
                ouPath,
                adminUser,
                adminPassword,
                _fieldMappings,
                _logger);

            // Создаем тестового пользователя в AD
            CreateTestUserInAD();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            // Удаляем тестового пользователя после всех тестов
            DeleteTestUserFromAD();
        }

        [Test]
        public async Task FindUserInTargetAsync_ExistingUser_ReturnsUserWithCorrectProperties()
        {
            // Act
            var result = await _repository.FindUserInTargetAsync(_testSamAccountName);

            // Assert
            Assert.NotNull(result);
            Assert.AreEqual(_testSamAccountName, result.SamAccountName);
            Assert.AreEqual("testuser@example.com", result.Email);
            Assert.AreEqual("User", result.FirstName);
            Assert.AreEqual("Test", result.LastName);
            Assert.AreEqual(new DateTime(2007, 4, 18), result.HireDate);
        }

        [Test]
        public async Task UpdateUserInTargetAsync_UpdatesNonProtectedAttributes()
        {
            // Arrange
            var testUser = new User
            {
                SamAccountName = _testSamAccountName,
                Email = "updated.email@example.com",
                FirstName = "UpdatedName",
                LastName = "UpdatedLastName",
                HireDate = new DateTime(2020, 1, 1)
            };

            // Act
            await _repository.UpdateUserInTargetAsync(testUser);

            // Assert
            var updatedUser = await _repository.FindUserInTargetAsync(_testSamAccountName);
            Assert.AreEqual("updated.email@example.com", updatedUser.Email);
            Assert.AreEqual("UpdatedName", updatedUser.FirstName);
            Assert.AreEqual("UpdatedLastName", updatedUser.LastName);
            Assert.AreEqual(new DateTime(2020, 1, 1), updatedUser.HireDate);

            // Проверяем, что защищенные атрибуты не изменились
            Assert.AreEqual(_testSamAccountName, updatedUser.SamAccountName);
        }

        [Test]
        public async Task FindUserInTargetAsync_NonExistingUser_ReturnsNull()
        {
            // Act
            var result = await _repository.FindUserInTargetAsync("non.existing.user");

            // Assert
            Assert.IsNull(result);
        }

        //[Test]
        //public async Task Indexer_Get_PropertyCollectionWithMissingProperty_ReturnsNull()
        //{
        //    // Arrange
        //    var user = await _repository.FindUserInTargetAsync(_testSamAccountName);
        //    var properties = ((SystemDirectoryEntry)user.DirectoryEntry).GetNativeDirectoryEntry().Properties;

        //    // Act
        //    var result = new DirectoryPropertyCollection(properties)["nonExistentProperty"];

        //    // Assert
        //    Assert.IsNull(result); // Проверяем, что свойство не существует (null)
        //}

        //[Test]
        //public async Task Indexer_Get_ResultPropertyCollectionWithMissingProperty_ReturnsNull()
        //{
        //    // Arrange
        //    using var searcher = new DirectorySearcher(
        //        new DirectoryEntry(ouPath, adminUser, adminPassword))
        //    {
        //        Filter = $"(sAMAccountName={_testSamAccountName})",
        //        PropertiesToLoad = new[] { "nonExistentProperty" } // Загружаем несуществующее свойство
        //    };

        //    var result = searcher.FindOne();
        //    var properties = new DirectoryPropertyCollection(result.Properties);

        //    // Act
        //    var value = properties["nonExistentProperty"];

        //    // Assert
        //    Assert.IsNull(value); // Должен вернуть null
        //}

        [Test]
        public async Task Indexer_Set_ResultPropertyCollection_ThrowsException()
        {
            // Arrange
            using var searcher = new DirectorySearcher(
            new DirectoryEntry(ouPath, adminUser, adminPassword))
            {
                Filter = $"(sAMAccountName={_testSamAccountName})"
            };

            var result = searcher.FindOne();
            var properties = new DirectoryPropertyCollection(result.Properties);

            // Act & Assert
            Assert.Throws<NotSupportedException>(() =>
                properties["mail"] = "new@example.com");
        }

        //[Test]
        //public async Task Contains_PropertyCollectionWithProperty_ReturnsTrue()
        //{
        //    // Arrange
        //    var user = await _repository.FindUserInTargetAsync(_testSamAccountName);
        //    var properties = ((SystemDirectoryEntry)user.DirectoryEntry).GetNativeDirectoryEntry().Properties;

        //    // Act
        //    bool exists = new DirectoryPropertyCollection(properties).Contains("mail");

        //    // Assert
        //    Assert.IsTrue(exists); // "mail" должен существовать
        //}

        //[Test]
        //public async Task Contains_PropertyCollectionWithoutProperty_ReturnsFalse()
        //{
        //    // Arrange
        //    var user = await _repository.FindUserInTargetAsync(_testSamAccountName);
        //    var properties = ((SystemDirectoryEntry)user.DirectoryEntry).GetNativeDirectoryEntry().Properties;

        //    // Act
        //    bool exists = new DirectoryPropertyCollection(properties).Contains("nonExistentProperty");

        //    // Assert
        //    Assert.IsFalse(exists); // Свойства нет
        //}
        public static void CreateTestUserInAD()
        {
            try
            {
                using (DirectoryEntry ouEntry = new DirectoryEntry(ouPath, adminUser, adminPassword))
                {
                    using (DirectorySearcher searcher = new DirectorySearcher(ouEntry))
                    {
                        searcher.Filter = $"(sAMAccountName={_testSamAccountName})";
                        if (searcher.FindOne() != null) return;
                    }

                    using (DirectoryEntry newUser = ouEntry.Children.Add($"CN=Test User", "user"))
                    {
                        newUser.Properties["sAMAccountName"].Value = _testSamAccountName;
                        newUser.Properties["userPrincipalName"].Value = $"{_testSamAccountName}@{domain}";
                        newUser.Properties["sn"].Value = "Test";
                        newUser.Properties["givenName"].Value = "User";
                        newUser.Properties["mail"].Value = "testuser@example.com";
                        newUser.Properties["info"].Value = $"BirthDate:{new DateTime(2007,4,18)}";
                        newUser.CommitChanges();

                        // Устанавливаем пароль через LDAPS (если требуется)
                        newUser.Invoke("SetPassword", new object[] { password });
                        System.Threading.Thread.Sleep(500);

                        // Активируем
                        newUser.Properties["userAccountControl"].Value = 0x200; // 512
                        newUser.CommitChanges();

                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private void DeleteTestUserFromAD()
        {
            try
            {
                using (var entry = new DirectoryEntry(ouPath, adminUser, adminPassword))
                using (var searcher = new DirectorySearcher(entry))
                {
                    searcher.Filter = $"(samAccountName={_testSamAccountName})";
                    var result = searcher.FindOne();

                    if (result != null)
                    {
                        using (var userEntry = result.GetDirectoryEntry())
                        {
                            userEntry.DeleteTree();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to delete test user: {ex.Message}");
            }
        }
    }
}
