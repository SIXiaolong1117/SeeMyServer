using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SeeMyServer.Helper
{
    public class Logger
    {
        private string logFilePath;
        private int maxLogSize;
        private ConcurrentQueue<string> logQueue = new ConcurrentQueue<string>();
        private bool writingInProgress = false;
        private readonly object lockObject = new object();

        public Logger(int maxFileSizeMB)
        {
            string userFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string logFolder = Path.Combine(userFolderPath, ".cmslogs");
            logFilePath = Path.Combine(logFolder, "logfile.txt");

            // 将MB转换为字节
            maxLogSize = maxFileSizeMB * 1024 * 1024;

            // 确保目录存在
            Directory.CreateDirectory(logFolder);

            // 启动日志写入线程
            Thread logThread = new Thread(WriteLogThread);
            logThread.IsBackground = true;
            logThread.Start();
        }

        public void LogInfo(string message)
        {
            Log("[INFO] " + GetTimestamp() + " " + message);
        }

        public void LogWarning(string message)
        {
            Log("[WARNING] " + GetTimestamp() + " " + message);
        }

        public void LogError(string message)
        {
            Log("[ERROR] " + GetTimestamp() + " " + message);
        }

        private void Log(string message)
        {
            logQueue.Enqueue(message);
        }

        private void WriteLogThread()
        {
            while (true)
            {
                if (!writingInProgress && logQueue.Count > 0)
                {
                    Monitor.Enter(lockObject);
                    try
                    {
                        if (!writingInProgress && logQueue.Count > 0)
                        {
                            writingInProgress = true;
                            string[] logEntries = logQueue.ToArray();
                            // 立即清空队列以释放锁
                            logQueue.Clear(); 

                            try
                            {
                                using (FileStream fileStream = new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                                using (StreamWriter streamWriter = new StreamWriter(fileStream))
                                {
                                    foreach (string nextLogEntry in logEntries)
                                    {
                                        streamWriter.WriteLine(nextLogEntry);
                                    }
                                }

                                // 写入所有条目后检查日志大小
                                if (new FileInfo(logFilePath).Length > maxLogSize)
                                {
                                    RotateLogFile();
                                }
                            }
                            catch (IOException ex)
                            {
                                // 处理 IOException（文件正在使用），等待并重试
                                //throw new Exception($"IOException occurred: {ex.Message}");
                                // 等待1秒
                                Thread.Sleep(1000);
                                // 重试写入
                                continue; 
                            }
                            finally
                            {
                                writingInProgress = false;
                            }
                        }
                    }
                    finally
                    {
                        Monitor.Exit(lockObject);
                    }
                }
                // 每100毫秒检查一次队列
                Thread.Sleep(100); 
            }
        }


        private void RotateLogFile()
        {
            // 从日志文件中读取所有行
            string[] lines = File.ReadAllLines(logFilePath);

            // 删除前10行
            lines = lines[10..];

            // 将剩余行写回日志文件
            File.WriteAllLines(logFilePath, lines);

            // 在最后追加一条新的日志消息
            File.AppendAllText(logFilePath, "[INFO] " + GetTimestamp() + " Log rotated." + Environment.NewLine);
        }

        private string GetTimestamp()
        {
            return DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss]");
        }

        public void OpenLogFileDirectory()
        {
            string logFileDirectory = Path.GetDirectoryName(logFilePath);
            if (Directory.Exists(logFileDirectory))
            {
                System.Diagnostics.Process.Start("explorer.exe", logFileDirectory);
            }
            else
            {
                Console.WriteLine("Log file directory does not exist.");
            }
        }
    }
}
