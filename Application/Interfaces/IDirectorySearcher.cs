
namespace Application.Interfaces
{
    public interface IDirectorySearcher : IDisposable
    {
        string Filter { get; set; }
        string[] PropertiesToLoad { get; set; }
        IDirectorySearchResult? FindOne();
    }
}
