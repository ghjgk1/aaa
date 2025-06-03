using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IDirectoryService
    {
        IDirectoryEntry GetDirectoryEntry(string path, string username, string password);
        IDirectorySearcher CreateSearcher(IDirectoryEntry directoryEntry);
    }
}
