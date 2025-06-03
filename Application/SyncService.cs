using Domain;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AscDb_to_AD_SynchonizerTests")]
namespace Application
{
    public class SyncService 
    {
        private readonly ISyncRepository _dbRepository;
        private readonly ISyncRepository _ldapRepository;
        private readonly ILogger<SyncService> _logger;
        private readonly Dictionary<string, string> _fieldMappings;
        private readonly string _searchBy;

        public SyncService(
            ISyncRepository dbRepository,
            ISyncRepository ldapRepository,
            ILogger<SyncService> logger,
            Dictionary<string, string> fieldMappings,
            string searchBy)
        {
            _dbRepository = dbRepository;
            _ldapRepository = ldapRepository;
            _logger = logger;
            _fieldMappings = fieldMappings;
            _searchBy = searchBy;
        }

        public virtual async Task SyncUsersAsync(bool dryRun = true)
        {
            try
            {
                var sourceUsers = await _dbRepository.GetUsersFromSourceAsync();
                _logger.LogInformation("Retrieved {UserCount} users from source database", sourceUsers.Count());

                foreach (var sourceUser in sourceUsers)
                {
                    var identifier = GetIdentifier(sourceUser);
                    var targetUser = await _ldapRepository.FindUserInTargetAsync(identifier);

                    if (targetUser == null)
                    {
                        _logger.LogWarning("User {Identifier} not found in target system", identifier);
                        continue;
                    }

                    if (NeedUpdate(sourceUser, targetUser))
                    {
                        _logger.LogInformation("User {Identifier} needs update", identifier);
                        if (!dryRun)
                        {
                            await _ldapRepository.UpdateUserInTargetAsync(sourceUser);
                        }
                    }
                    else _logger.LogInformation("User {Identifier} is up-to-date in AD, no update required", identifier);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user synchronization");
                throw;
            }
        }

        internal string GetIdentifier(User user)
        {
            var property = typeof(User).GetProperty(_searchBy);
            return property?.GetValue(user)?.ToString() ?? string.Empty;
        }

        internal bool NeedUpdate(User source, User target)
        {
            foreach (var mapping in _fieldMappings)
            {
                var sourceProperty = typeof(User).GetProperty(mapping.Value);
                var targetProperty = typeof(User).GetProperty(mapping.Value);

                if (sourceProperty == null || targetProperty == null) continue;

                var sourceValue = sourceProperty.GetValue(source);
                var targetValue = targetProperty.GetValue(target);

                if (!Equals(sourceValue, targetValue))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
