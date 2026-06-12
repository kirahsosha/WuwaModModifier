using System.Threading.Tasks;

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

        // Async overloads
        Task<string> ReadAllTextAsync(string path);
        Task WriteAllTextAsync(string path, string content);
        Task CopyDirectoryAsync(string source, string destination);
        Task DeleteDirectoryAsync(string path, bool recursive);
    }
}


