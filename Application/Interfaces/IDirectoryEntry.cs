using System.DirectoryServices;

namespace Application.Interfaces
{
    public interface IDirectoryEntry : IDisposable
    {
        IPropertyCollection Properties { get; }
        void CommitChanges();
        DirectoryEntry GetNativeDirectoryEntry();
    }
}
