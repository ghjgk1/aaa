using System.DirectoryServices;
using Domain;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using Application.Interfaces;
using Infrastructure.Directory.Models;
using System.DirectoryServices.AccountManagement;

[assembly: InternalsVisibleTo("AscDb_to_AD_SynchonizerTests")]
namespace Infrastructure.Directory
{
    public class LdapSyncRepository : ISyncRepository
    {
        private readonly ILogger<LdapSyncRepository> _logger;
        private readonly Dictionary<string, string> _fieldMappings;
        private readonly IDirectoryService _directoryService;
        private readonly string _ldapPath;
        private readonly string _username;
        private readonly string _password;

        public LdapSyncRepository(
            string ldapPath,
            string username,
            string password,
            Dictionary<string, string> fieldMappings,
            ILogger<LdapSyncRepository> logger)
            : this(ldapPath, username, password, fieldMappings, logger, new SystemDirectoryService())
        {
        }

        internal LdapSyncRepository(
            string ldapPath,
            string username,
            string password,
            Dictionary<string, string> fieldMappings,
            ILogger<LdapSyncRepository> logger,
            IDirectoryService directoryService)
        {
            _ldapPath = ldapPath;
            _username = username;
            _password = password;
            _directoryService = directoryService;
            _fieldMappings = fieldMappings;
            _logger = logger;
        }

        public async Task<User?> FindUserInTargetAsync(string identifier)
        {
            try
            {
                using var entry = _directoryService.GetDirectoryEntry(_ldapPath, _username, _password);
                using var searcher = _directoryService.CreateSearcher(entry);

                searcher.Filter = $"(sAMAccountName={identifier})";
                searcher.PropertiesToLoad = _fieldMappings.Keys.ToArray();

                var result = searcher.FindOne();
                if (result == null) return null;

                return MapToUser(result.Properties);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding user in AD");
                throw;
            }
        }

        public async Task UpdateUserInTargetAsync(User user)
        {
            try
            {
                var identifier = GetIdentifier(user);
                using var entry = _directoryService.GetDirectoryEntry(_ldapPath, _username, _password);
                using var searcher = _directoryService.CreateSearcher(entry);

                searcher.Filter = $"(sAMAccountName={identifier})";

                var result = searcher.FindOne();
                if (result == null)
                {
                    _logger.LogWarning("User {Identifier} not found in AD", identifier);
                    return;
                }

                using var userEntry = result.GetDirectoryEntry();

                foreach (var mapping in _fieldMappings)
                {
                    if (IsProtectedAttribute(mapping.Key)) continue;

                    try
                    {
                        var property = typeof(User).GetProperty(mapping.Value);
                        if (property == null) continue;

                        var newValue = property.GetValue(user);
                        if (newValue == null) continue;

                        var formattedValue = FormatAttributeValue(mapping.Key, newValue?.ToString());
                        if (!IsValidAttributeValue(mapping.Key, formattedValue)) continue;

                        var currentValue = userEntry.Properties[mapping.Key]?.ToString();
                        if (currentValue == formattedValue) continue;

                        userEntry.Properties[mapping.Key] = formattedValue;
                        _logger.LogDebug("Preparing to update {Attribute} to {Value}", mapping.Key, formattedValue);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing attribute {Attribute}", mapping.Key);
                    }
                }

                userEntry.CommitChanges();
                _logger.LogInformation("User {Identifier} updated successfully", identifier);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user in AD");
                throw;
            }
        }

        internal User MapToUser(IPropertyCollection properties)
        {
            var user = new User();
            foreach (var mapping in _fieldMappings)
            {
                var property = typeof(User).GetProperty(mapping.Value);
                if (property != null && properties.Contains(mapping.Key))
                {
                    var value = properties[mapping.Key]?.ToString();
                    if (property.PropertyType == typeof(DateTime?))
                    {
                        if (value?.StartsWith("BirthDate:") == true)
                        {
                            value = value.Substring("BirthDate:".Length);
                        }

                        if (DateTime.TryParse(value, out var dateValue))
                        {
                            property.SetValue(user, dateValue);
                        }
                        else
                        {
                            property.SetValue(user, null);
                            _logger.LogWarning($"Failed to parse DateTime from value: {value}");
                        }
                    }
                    else
                    {
                        property.SetValue(user, Convert.ChangeType(value, property.PropertyType));
                    }
                }
            }
            return user;
        }

        internal string FormatAttributeValue(string attribute, string value) => attribute switch
        {
            "info" => DateTime.TryParse(value, out var date) ? $"BirthDate:{date:yyyy-MM-dd}" : throw new FormatException($"Invalid date format: {value}"),
            _ => value
        };

        internal bool IsProtectedAttribute(string attribute)
        {
            var protectedAttributes = new[] { "userPrincipalName", "sAMAccountName" };
            return protectedAttributes.Contains(attribute);
        }

        internal bool IsValidAttributeValue(string attribute, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            return attribute switch
            {
                "mail" => value.Contains("@") && value.Length <= 256,
                "telephoneNumber" => value.Length <= 32,
                "mobile" => value.Length <= 32,
                "employeeID" => !string.IsNullOrWhiteSpace(value) && value.Length <= 64,
                _ => true
            };
        }

        public Task<IEnumerable<User>> GetUsersFromSourceAsync()
        {
            throw new NotImplementedException();
        }

        private string GetIdentifier(User user)
        {
            return user.SamAccountName ?? throw new ArgumentNullException("SamAccountName is required");
        }
    }
}