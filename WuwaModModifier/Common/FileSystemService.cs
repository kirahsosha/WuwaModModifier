using System.IO;

namespace WuwaModModifier.Common
{
    /// <summary>
    /// 真实文件系统操作实现
    /// </summary>
    public class FileSystemService : IFileSystemService
    {
        public bool DirectoryExists(string path) => Directory.Exists(path);

        public string[] GetDirectories(string path) => Directory.GetDirectories(path);

        public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

        public void CopyDirectory(string source, string destination)
        {
            if (string.IsNullOrEmpty(source) || !Directory.Exists(source))
                return;

            if (!Directory.Exists(destination))
            {
                Directory.CreateDirectory(destination);
            }

            foreach (string file in Directory.GetFiles(source))
            {
                string dest = Path.Combine(destination, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }

            foreach (string folder in Directory.GetDirectories(source))
            {
                string dest = Path.Combine(destination, Path.GetFileName(folder));
                CopyDirectory(folder, dest);
            }
        }
    }
}


