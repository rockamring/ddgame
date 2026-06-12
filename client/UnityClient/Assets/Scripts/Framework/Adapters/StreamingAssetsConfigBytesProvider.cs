using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GameFramework.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace GameClient.Framework
{
    public sealed class StreamingAssetsConfigBytesProvider : IConfigBytesProvider
    {
        private readonly string _rootPath;

        public StreamingAssetsConfigBytesProvider(string relativeConfigPath = "Config")
        {
            if (string.IsNullOrWhiteSpace(relativeConfigPath))
                throw new ArgumentException("Config path cannot be empty.", nameof(relativeConfigPath));

            _rootPath = CombinePath(Application.streamingAssetsPath, relativeConfigPath);
        }

        public bool SupportsSynchronousLoad
        {
            get
            {
#if UNITY_ANDROID || UNITY_WEBGL
                return false;
#else
                return true;
#endif
            }
        }

        public byte[] Load(string fileName)
        {
            if (!SupportsSynchronousLoad)
            {
                throw new NotSupportedException(
                    "StreamingAssets config synchronous load is not supported on this platform. " +
                    "Use DataManager.PreloadAsync<T>() before accessing config rows.");
            }

            return File.ReadAllBytes(GetFileSystemPath(fileName));
        }

        public async Task<byte[]> LoadAsync(
            string fileName,
            CancellationToken cancellationToken = default)
        {
            var uri = GetUri(fileName);
            using var request = UnityWebRequest.Get(uri);
            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (request.result != UnityWebRequest.Result.Success)
                throw new FileNotFoundException($"Config file load failed: {uri}. {request.error}", uri);

            return request.downloadHandler.data;
        }

        public string GetDisplayPath(string fileName)
        {
            return GetUri(fileName);
        }

        private string GetFileSystemPath(string fileName)
        {
            return CombinePath(_rootPath, fileName);
        }

        private string GetUri(string fileName)
        {
            var path = GetFileSystemPath(fileName);
            if (path.Contains("://"))
                return path;

            return new Uri(path).AbsoluteUri;
        }

        private static string CombinePath(string left, string right)
        {
            return left.TrimEnd('/', '\\') + "/" + right.TrimStart('/', '\\');
        }
    }
}
