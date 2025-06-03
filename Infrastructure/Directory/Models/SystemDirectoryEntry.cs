using Application.Interfaces;
using System.DirectoryServices;

namespace Infrastructure.Directory.Models
{
    public class SystemDirectoryEntry : IDirectoryEntry
    {
        private readonly DirectoryEntry _directoryEntry;

        public SystemDirectoryEntry(string path, string username, string password)
        {
            _directoryEntry = new DirectoryEntry(path, username, password);
        }

        public IPropertyCollection Properties =>
            new DirectoryPropertyCollection(_directoryEntry.Properties);

        public DirectoryEntry GetNativeDirectoryEntry() => _directoryEntry;

        public void CommitChanges()
        {
            _directoryEntry.CommitChanges();
        }

        public void Dispose()
        {
            _directoryEntry.Dispose();
        }
    }
}
