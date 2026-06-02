namespace WuwaModModifier.Common
{
    public interface IFileSystemService
    {
        bool DirectoryExists(string path);
        string[] GetDirectories(string path);
        bool FileExists(string path);
        string[] GetFiles(string path, string searchPattern);
        string ReadAllText(string path);
        void WriteAllText(string path, string content);
        void CreateDirectory(string path);
        void DeleteDirectory(string path, bool recursive);
        void CopyDirectory(string source, string destination);
    }
}


