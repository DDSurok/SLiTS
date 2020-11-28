namespace SLiTS.Api
{
    public interface ISharedPropertyProvider
    {
        string this[string name] { get; }
    }
}
