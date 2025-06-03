using Application.Interfaces;
using System.DirectoryServices;

namespace Infrastructure.Directory.Models
{
    public class DirectoryPropertyCollection : IPropertyCollection
    {
        private readonly PropertyCollection _propertyCollection;
        private readonly ResultPropertyCollection _resultPropertyCollection;

        public DirectoryPropertyCollection(PropertyCollection properties)
        {
            _propertyCollection = properties;
            _resultPropertyCollection = null;
        }

        public DirectoryPropertyCollection(ResultPropertyCollection properties)
        {
            _resultPropertyCollection = properties;
            _propertyCollection = null;
        }

        public object? this[string propertyName]
        {
            get => _propertyCollection != null
                ? _propertyCollection[propertyName]?.Value
                : _resultPropertyCollection?[propertyName]?[0];
            set
            {
                if (_propertyCollection != null)
                {
                    _propertyCollection[propertyName].Value = value;
                }
                else
                {
                    throw new NotSupportedException("Setting values is not supported for ResultPropertyCollection");
                }
            }
        }

        public bool Contains(string propertyName)
        {
            return _propertyCollection != null
                ? _propertyCollection.Contains(propertyName)
                : _resultPropertyCollection?.Contains(propertyName) ?? false;
        }
    }
}
