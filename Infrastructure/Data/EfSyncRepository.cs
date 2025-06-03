using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data
{
    public class EfSyncRepository : ISyncRepository
    {
        private readonly AscDbContext _context;
        private readonly ILogger<EfSyncRepository> _logger;

        public EfSyncRepository(AscDbContext context, ILogger<EfSyncRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<User>> GetUsersFromSourceAsync()
        {
            try
            {
                return await _context.Users
                    .AsNoTracking()
                    .Select(u => new User
                    {
                        SamAccountName = u.SamAccountName,
                        EmployeeId = u.EmployeeId,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        MiddleName = u.MiddleName,
                        FullName = u.FullName,
                        JobTitle = u.JobTitle,
                        Department = u.Department,
                        InternalPhone = u.InternalPhone,
                        MobilePhone = u.MobilePhone,
                        AdditionalPhone = u.AdditionalPhone,
                        Email = u.Email,
                        HireDate = u.HireDate
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users from database");
                throw;
            }
        }

        public Task<User?> FindUserInTargetAsync(string identifier)
        {
            // This is implemented in the LDAP repository
            throw new NotImplementedException();
        }

        public Task UpdateUserInTargetAsync(User user)
        {
            // This is implemented in the LDAP repository
            throw new NotImplementedException();
        }
    }

}
