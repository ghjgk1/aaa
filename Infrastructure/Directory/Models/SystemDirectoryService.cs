using Application.Interfaces;
using System.DirectoryServices;

namespace Infrastructure.Directory.Models
{
    public class SystemDirectoryService : IDirectoryService
    {
        public IDirectoryEntry GetDirectoryEntry(string path, string username, string password)
        {
            return new SystemDirectoryEntry(path, username, password);
        }

        public IDirectorySearcher CreateSearcher(IDirectoryEntry directoryEntry)
        {
            return new SystemDirectorySearcher((SystemDirectoryEntry)directoryEntry);
        }
    }
}
