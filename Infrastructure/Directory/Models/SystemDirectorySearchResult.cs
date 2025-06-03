using Application.Interfaces;
using System.DirectoryServices;

namespace Infrastructure.Directory.Models
{
    public class SystemDirectorySearchResult : IDirectorySearchResult
    {
        private readonly SearchResult _searchResult;

        public SystemDirectorySearchResult(SearchResult searchResult)
        {
            _searchResult = searchResult;
        }

        public IDirectoryEntry GetDirectoryEntry()
        {
            var nativeEntry = _searchResult.GetDirectoryEntry();
            return new SystemDirectoryEntry(
                nativeEntry.Path,
                nativeEntry.Username,
                password: null);
        }

        public IPropertyCollection Properties =>
            new DirectoryPropertyCollection(_searchResult.Properties);
    }
}
