namespace WuwaModModifier.Common
{
    public interface IFileSystemService
    {
        bool DirectoryExists(string path);
        string[] GetDirectories(string path);
        void DeleteDirectory(string path, bool recursive);
        void CopyDirectory(string source, string destination);
    }
}


