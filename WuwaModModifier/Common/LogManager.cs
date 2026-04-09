using System.IO;
using System.Text;

namespace WuwaModModifier.Common
{
    /// <summary>
    /// 日志写入类
    /// </summary>
    public class LogManager
    {
        private static object lock_info = new object();
        private static object lock_log = new object();
        private static object lock_error = new object();
        private static object lock_logName = new object();

        /// <summary>
        /// 写入日志(每天一个文件)
        /// </summary>
        /// <param name="content">内容</param>
        /// <param name="folder">日志文件所在文件件,默认“Log”</param>
        public static void Log(string content, string folder = "Log")
        {
            lock (lock_log)
            {
                string fileDir = string.Format("{0}\\{1}", AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'), folder);
                CreateFolder(fileDir);
                string filePath = string.Format("{0}\\{1:yyyy-MM-dd}.log", fileDir, DateTime.Now);
                try
                {
                    using (StreamWriter sw = new StreamWriter(filePath, true))
                    {
                        sw.AutoFlush = true;
                        sw.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss fff") + "]" + content);
                        sw.Close();
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 写入信息
        /// </summary>
        /// <param name="content">内容</param>
        public static void Info(string content)
        {
            lock (lock_info)
            {
                string fileDir = string.Format("{0}\\MessageLog", AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'));
                CreateFolder(fileDir);
                string filePath = string.Format("{0}\\{1:yyyy-MM-dd}.log", fileDir, DateTime.Now);
                try
                {
                    using (StreamWriter sw = new StreamWriter(filePath, true))
                    {
                        sw.AutoFlush = true;
                        sw.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss fff") + "]" + content);
                        sw.Close();
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 写入Exception信息
        /// </summary>
        /// <param name="content">错误文本</param>
        /// <param name="ex">异常信息</param>
        public static void Error(string content, Exception ex)
        {
            lock (lock_error)
            {
                string fileDir = string.Format("{0}\\ErrorLog", AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'));
                CreateFolder(fileDir);
                string filePath = string.Format("{0}\\{1:yyyy-MM-dd}.log", fileDir, DateTime.Now);
                try
                {
                    using (StreamWriter sw = new StreamWriter(filePath, true))
                    {
                        sw.AutoFlush = true;
                        sw.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss fff") + "]" + content + "=>" + ex.ToString());
                        sw.Close();
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 写入日志(每天一个文件)
        /// 文件名带后缀
        /// </summary>
        /// <param name="content">内容</param>
        /// <param name="postfix">文件名后缀</param>
        /// <param name="folder">日志文件所在文件件,默认“Log”</param>
        public static void LogWithPostfix(string content, string postfix, string folder = "Log")
        {
            lock (lock_logName)
            {
                string fileDir = string.Format("{0}\\{1}", AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'), folder);
                // 创建目录
                CreateFolder(fileDir);
                string filePath = string.Format("{0}\\{1:yyyy-MM-dd}_{2}.log", fileDir, DateTime.Now, postfix);
                try
                {
                    using (StreamWriter sw = new StreamWriter(filePath, true))
                    {
                        sw.AutoFlush = true;
                        sw.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss fff") + "]" + content);
                        sw.Close();
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 创建文件夹
        /// </summary>
        /// <param name="dirPath">文件夹路径</param>
        public static void CreateFolder(string dirPath)
        {
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }
        }

        private static readonly object lock_file = new object();
        /// <summary>
        /// 保存数据到文件
        /// </summary>
        /// <param name="filePath">文件全路径</param>
        /// <param name="content">内容</param>
        /// <param name="encoding">编码方式,默认为utf-8</param>
        public static void SaveDataToFile(string filePath, string content, Encoding? encoding = null)
        {
            lock (lock_file)
            {
                if (encoding == null)
                {
                    encoding = Encoding.GetEncoding("utf-8");
                }
                try
                {
                    FileStream? fs = null;
                    if (File.Exists(filePath))
                    {
                        fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    }
                    else
                    {
                        fs = File.Create(filePath);
                    }
                    using (StreamWriter sw = new StreamWriter(fs, encoding))
                    {
                        sw.Write(content);
                        sw.Close();
                        fs.Close();
                    }
                }
                catch
                {
                }
            }
        }
    }
}
