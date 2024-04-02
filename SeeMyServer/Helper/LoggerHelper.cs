﻿using System;
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
                    lock (lockObject)
                    {
                        if (!writingInProgress && logQueue.Count > 0)
                        {
                            writingInProgress = true;
                            try
                            {
                                string nextLogEntry;
                                while (logQueue.TryDequeue(out nextLogEntry))
                                {
                                    File.AppendAllText(logFilePath, nextLogEntry + Environment.NewLine);

                                    // 检查日志尺寸
                                    if (new FileInfo(logFilePath).Length > maxLogSize)
                                    {
                                        RotateLogFile();
                                    }
                                }
                            }
                            finally
                            {
                                writingInProgress = false;
                            }
                        }
                    }
                }
                Thread.Sleep(100); // 每100毫秒检查一次队列
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
