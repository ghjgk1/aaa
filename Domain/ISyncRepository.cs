using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.DirectoryServices;

namespace Domain
{
    public interface ISyncRepository
    {
        Task<IEnumerable<User>> GetUsersFromSourceAsync();
        Task<User?> FindUserInTargetAsync(string identifier);
        Task UpdateUserInTargetAsync(User user);
    }
}
