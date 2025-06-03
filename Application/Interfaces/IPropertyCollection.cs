namespace Application.Interfaces
{
    public interface IPropertyCollection
    {
        object? this[string propertyName] { get; set; }
        bool Contains(string propertyName);
    }
}
