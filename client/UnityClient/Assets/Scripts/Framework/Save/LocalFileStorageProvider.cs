using System;
using System.IO;

namespace GameFramework.Save
{
    public sealed class LocalFileStorageProvider : IStorageProvider
    {
        private readonly string _rootDirectory;

        public LocalFileStorageProvider(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("Root directory cannot be empty.", nameof(rootDirectory));

            _rootDirectory = Path.GetFullPath(rootDirectory);
            Directory.CreateDirectory(_rootDirectory);
        }

        public bool Exists(string path)
        {
            return File.Exists(GetFullPath(path));
        }

        public byte[] ReadBytes(string path)
        {
            return File.ReadAllBytes(GetFullPath(path));
        }

        public void WriteBytes(string path, byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            var fullPath = GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllBytes(fullPath, bytes);
        }

        public bool Delete(string path)
        {
            var fullPath = GetFullPath(path);
            if (!File.Exists(fullPath))
                return false;

            File.Delete(fullPath);
            return true;
        }

        public string GetFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Storage path cannot be empty.", nameof(path));

            var relativePath = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(_rootDirectory, relativePath));
            var root = _rootDirectory.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? _rootDirectory
                : _rootDirectory + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Storage path escapes root directory: {path}");

            return fullPath;
        }
    }
}
