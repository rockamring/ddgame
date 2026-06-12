using System.Threading;
using System.Threading.Tasks;

namespace GameFramework.Data
{
    public interface IConfigBytesProvider
    {
        bool SupportsSynchronousLoad { get; }

        byte[] Load(string fileName);

        Task<byte[]> LoadAsync(string fileName, CancellationToken cancellationToken = default);

        string GetDisplayPath(string fileName);
    }
}
