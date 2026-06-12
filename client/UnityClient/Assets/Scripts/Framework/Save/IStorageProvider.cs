namespace GameFramework.Save
{
    public interface IStorageProvider
    {
        bool Exists(string path);
        byte[] ReadBytes(string path);
        void WriteBytes(string path, byte[] bytes);
        bool Delete(string path);
        string GetFullPath(string path);
    }
}
