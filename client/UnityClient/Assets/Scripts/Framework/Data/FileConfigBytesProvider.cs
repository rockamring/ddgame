using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GameFramework.Data
{
    public sealed class FileConfigBytesProvider : IConfigBytesProvider
    {
        private readonly string _rootDirectory;

        public FileConfigBytesProvider(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("Config root directory cannot be empty.", nameof(rootDirectory));

            _rootDirectory = rootDirectory;
        }

        public bool SupportsSynchronousLoad => true;

        public byte[] Load(string fileName)
        {
            return File.ReadAllBytes(GetFullPath(fileName));
        }

        public async Task<byte[]> LoadAsync(
            string fileName,
            CancellationToken cancellationToken = default)
        {
            var path = GetFullPath(fileName);
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            return await File.ReadAllBytesAsync(path, cancellationToken);
#else
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(() => File.ReadAllBytes(path), cancellationToken);
#endif
        }

        public string GetDisplayPath(string fileName)
        {
            return GetFullPath(fileName);
        }

        private string GetFullPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Config file name cannot be empty.", nameof(fileName));

            return Path.Combine(_rootDirectory, fileName);
        }
    }
}
