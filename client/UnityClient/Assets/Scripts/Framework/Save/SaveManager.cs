using System;
using System.IO;
using System.Text;
using GameFramework.Core.GameSystem;

namespace GameFramework.Save
{
    public sealed class SaveManager : GameModule
    {
        private IStorageProvider? _provider;

        public override string ModuleName => "SaveManager";

        public IStorageProvider Provider
        {
            get => _provider ??= new LocalFileStorageProvider(
                Path.Combine(AppContext.BaseDirectory, "SaveData"));
            set => _provider = value ?? throw new ArgumentNullException(nameof(value));
        }

        public bool Exists(string path)
        {
            return Provider.Exists(path);
        }

        public void SaveBytes(string path, byte[] bytes)
        {
            Provider.WriteBytes(path, bytes);
        }

        public byte[] LoadBytes(string path)
        {
            return Provider.ReadBytes(path);
        }

        public bool TryLoadBytes(string path, out byte[] bytes)
        {
            if (!Exists(path))
            {
                bytes = Array.Empty<byte>();
                return false;
            }

            bytes = LoadBytes(path);
            return true;
        }

        public void SaveText(string path, string content)
        {
            SaveText(path, content, Encoding.UTF8);
        }

        public void SaveText(string path, string content, Encoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            Provider.WriteBytes(path, encoding.GetBytes(content ?? string.Empty));
        }

        public string LoadText(string path)
        {
            return LoadText(path, Encoding.UTF8);
        }

        public string LoadText(string path, Encoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            return encoding.GetString(Provider.ReadBytes(path));
        }

        public bool TryLoadText(string path, out string content)
        {
            return TryLoadText(path, Encoding.UTF8, out content);
        }

        public bool TryLoadText(string path, Encoding encoding, out string content)
        {
            if (!Exists(path))
            {
                content = string.Empty;
                return false;
            }

            content = LoadText(path, encoding);
            return true;
        }

        public bool Delete(string path)
        {
            return Provider.Delete(path);
        }

        public string GetFullPath(string path)
        {
            return Provider.GetFullPath(path);
        }
    }
}
