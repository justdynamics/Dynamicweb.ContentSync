using Dynamicweb.ContentSync.Models;

namespace Dynamicweb.ContentSync.Infrastructure;

public interface IContentStore
{
    void WriteTree(SerializedArea area, string rootDirectory);
    SerializedArea ReadTree(string rootDirectory);
}
