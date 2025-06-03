using Application.Interfaces;
using System.DirectoryServices;

namespace Infrastructure.Directory.Models
{
    public class SystemDirectorySearcher : IDirectorySearcher
    {
        private readonly DirectorySearcher _searcher;

        public SystemDirectorySearcher(SystemDirectoryEntry directoryEntry)
        {
            _searcher = new DirectorySearcher(directoryEntry.GetNativeDirectoryEntry());
        }

        public string Filter
        {
            get => _searcher.Filter;
            set => _searcher.Filter = value;
        }

        public string[] PropertiesToLoad
        {
            get => _searcher.PropertiesToLoad.Cast<string>().ToArray();
            set => _searcher.PropertiesToLoad.AddRange(value);
        }

        public IDirectorySearchResult? FindOne()
        {
            var result = _searcher.FindOne();
            return result != null ? new SystemDirectorySearchResult(result) : null;
        }

        public void Dispose()
        {
            _searcher.Dispose();
        }
    }
}
