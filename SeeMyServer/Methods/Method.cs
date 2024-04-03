using Microsoft.UI.Xaml.Shapes;
using Newtonsoft.Json;
using Renci.SshNet;
using Renci.SshNet.Security;
using SeeMyServer.Helper;
using SeeMyServer.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Power;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using static PInvoke.User32;

namespace SeeMyServer.Methods
{
    public class Method
    {
        // 设置日志，最大1MB
        private static Logger logger = new Logger(1);
        public static string SendSSHCommand(string sshCommand, string sshHost, string sshPort, string sshUser, string sshPasswd, string sshKey, string privateKeyIsOpen)
        {
            try
            {
                int port;
                if (!int.TryParse(sshPort, out port))
                {
                    logger.LogError($"{sshHost}:{sshPort} 使用了无效的 SSH 端口号：{sshPort}");
                    return "";
                }

                bool usePrivateKey = string.Equals(privateKeyIsOpen, "True", StringComparison.OrdinalIgnoreCase);
                using (SshClient sshClient = InitializeSshClient(sshHost, port, sshUser, sshPasswd, sshKey, usePrivateKey))
                {
                    if (sshClient == null)
                    {
                        logger.LogError($"{sshHost}:{sshPort} SSH 客户端初始化失败。");
                        return "";
                    }

                    return ExecuteSshCommand(sshClient, sshCommand);
                }
            }
            catch (Exception ex)
            {
                logger.LogError("SSH 操作失败：" + ex.Message);
                return "";
            }
        }
        private static SshClient InitializeSshClient(string sshHost, int sshPort, string sshUser, string sshPasswd, string sshKey, bool usePrivateKey)
        {
            try
            {
                if (usePrivateKey)
                {
                    PrivateKeyFile privateKeyFile = new PrivateKeyFile(sshKey);
                    ConnectionInfo connectionInfo = new ConnectionInfo(sshHost, sshPort, sshUser, new PrivateKeyAuthenticationMethod(sshUser, new PrivateKeyFile[] { privateKeyFile }));
                    connectionInfo.Encoding = Encoding.UTF8;
                    // 设置连接超时时间
                    connectionInfo.Timeout = TimeSpan.FromSeconds(5);
                    // 设置连接重试次数
                    connectionInfo.RetryAttempts = 3;
                    return new SshClient(connectionInfo);
                }
                else
                {
                    return new SshClient(sshHost, sshPort, sshUser, sshPasswd);
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"{sshHost} SSH 连接失败：" + ex.Message);
                return null;
            }
        }

        private static string ExecuteSshCommand(SshClient sshClient, string sshCommand)
        {
            try
            {
                sshClient.Connect();
                if (sshClient.IsConnected)
                {
                    SshCommand SSHCommand = sshClient.RunCommand(sshCommand);
                    if (!string.IsNullOrEmpty(SSHCommand.Error))
                    {
                        logger.LogError($"{sshCommand} SSH 命令执行错误：{SSHCommand.Error}");
                        return "";
                    }
                    else
                    {
                        return SSHCommand.Result;
                    }
                }
                logger.LogError($"{sshCommand} SSH 命令执行失败。");
                return "";
            }
            finally
            {
                sshClient.Disconnect();
            }
        }

        private static async Task<string> SendSSHCommandAsync(string command, CMSModel cmsModel)
        {
            string passwd = "";
            if (cmsModel.SSHKeyIsOpen != "True")
            {
                // 检查是否已经存在密钥和初始化向量，如果不存在则生成新的
                string key = Method.LoadKeyFromLocalSettings() ?? Method.GenerateRandomKey();
                string iv = Method.LoadIVFromLocalSettings() ?? Method.GenerateRandomIV();

                // 将密钥和初始化向量保存到 localSettings 中
                Method.SaveKeyToLocalSettings(key);
                Method.SaveIVToLocalSettings(iv);

                // 使用的对称加密算法
                SymmetricAlgorithm symmetricAlgorithm = new AesManaged();

                // 设置加密密钥和初始化向量
                symmetricAlgorithm.Key = Convert.FromBase64String(key);
                symmetricAlgorithm.IV = Convert.FromBase64String(iv);
                passwd = Method.DecryptString(cmsModel.SSHPasswd, symmetricAlgorithm);
            }
            return await Task.Run(() =>
            {
                return SendSSHCommand(command, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, passwd, cmsModel.SSHKey, cmsModel.SSHKeyIsOpen);
            });
        }


















        // 导出配置
        public static async Task<string> ExportConfig(CMSModel cmsModel)
        {
            // 创建一个FileSavePicker
            FileSavePicker savePicker = new FileSavePicker();
            // 获取当前窗口句柄 (HWND) 
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.m_window);
            // 使用窗口句柄 (HWND) 初始化FileSavePicker
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

            // 为FilePicker设置选项
            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            // 用户可以将文件另存为的文件类型下拉列表
            savePicker.FileTypeChoices.Add("Plain Text", new List<string>() { ".cmsconfig" });
            // 如果用户没有选择文件类型，则默认为
            savePicker.DefaultFileExtension = ".cmsconfig";

            // 默认文件名
            savePicker.SuggestedFileName = cmsModel.Name + "_BackUp_" + DateTime.Now.ToString();

            // 打开Picker供用户选择文件
            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                try
                {
                    // 阻止更新文件的远程版本，直到我们完成更改并调用 CompleteUpdatesAsync。
                    CachedFileManager.DeferUpdates(file);
                }
                catch
                {
                    // 当您保存至OneDrive等同步盘目录时，在Windows11上可能引起DeferUpdates错误，备份文件不一定写入正确。
                    logger.LogWarning("保存行为完成，但当您保存至OneDrive等同步盘目录时，在Windows11上可能引起DeferUpdates错误，备份文件不一定写入正确。");
                    return "保存行为完成，但当您保存至OneDrive等同步盘目录时，在Windows11上可能引起DeferUpdates错误，备份文件不一定写入正确。";
                }

                // 将数据序列化为 JSON 格式
                string jsonData = JsonConvert.SerializeObject(cmsModel);

                // 写入文件
                await FileIO.WriteTextAsync(file, jsonData);

                // 让Windows知道我们已完成文件更改，以便其他应用程序可以更新文件的远程版本。
                // 完成更新可能需要Windows请求用户输入。
                FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                if (status == FileUpdateStatus.Complete)
                {
                    // 保存成功
                    logger.LogInfo("保存成功");
                    return "保存成功";
                }
                else if (status == FileUpdateStatus.CompleteAndRenamed)
                {
                    // 重命名并保存成功
                    logger.LogInfo("重命名并保存成功");
                    return "重命名并保存成功";
                }
                else
                {
                    // 文件无法保存！
                    logger.LogError("无法保存！");
                    return "无法保存！";
                }
            }
            return "错误！";
        }
        // 导入配置
        public static async Task<CMSModel> ImportConfig()
        {
            // 创建一个FileOpenPicker
            var openPicker = new FileOpenPicker();
            // 获取当前窗口句柄 (HWND) 
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.m_window);
            // 使用窗口句柄 (HWND) 初始化FileOpenPicker
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

            // 为FilePicker设置选项
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            // 建议打开位置 桌面
            openPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            // 文件类型过滤器
            openPicker.FileTypeFilter.Add(".cmsconfig");

            // 打开选择器供用户选择文件
            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                // 读取 JSON 文件内容
                string jsonData = await FileIO.ReadTextAsync(file);
                // 反序列化JSON数据为WoLModel对象
                CMSModel importedData = JsonConvert.DeserializeObject<CMSModel>(jsonData);
                if (importedData != null)
                {
                    // 成功导入配置数据。 
                    return importedData;
                }
                else
                {
                    // JSON数据无法反序列化为配置数据。 
                    return null;
                }
            }
            else
            {
                // 未选择JSON文件。
                return null;
            }
        }



        public static async Task<string[]> GetLinuxUsageAsync(CMSModel cmsModel)
        {
            // top查询
            string UsageCMD = "top 1 -bn1";
            string UsageRes = await SendSSHCommandAsync(UsageCMD, cmsModel);

            // CPU核心数
            string CPUCoreCMD = "cat /proc/cpuinfo | grep processor | wc -l";
            string CPUCoreRes = await SendSSHCommandAsync(CPUCoreCMD, cmsModel);

            // 使用正则取出CPU占用
            Regex cpuPattern = new Regex(@"%Cpu\d+\s+:\s*(\d*\.\d+)\s+us,\s*(\d*\.\d+)\s+sy,\s*(\d*\.\d+)\s+ni,\s*(\d*\.\d+)\s+id,\s*(\d*\.\d+)\s+wa,\s*(\d*\.\d+)\s+hi,\s*(\d*\.\d+)\s+si,\s*(\d*\.\d+)\s+st");
            // 使用正则取出MEM占用
            Regex memPattern = new Regex(@"GiB\s+Mem\s+:\s+([\d\.]+)\s+total,\s+([\d\.]+)\s+free,\s+([\d\.]+)\s+used,\s+([\d\.]+)\s+buff/cache");
            // 使用正则取出负载信息
            Regex loadRegex = new Regex(@"load average: (\d+\.\d+), (\d+\.\d+), (\d+\.\d+)");

            Match memMatch = memPattern.Match(UsageRes);
            Match loadMatch = loadRegex.Match(UsageRes);


            // 创建列表以存储结果
            List<string> cpuUsageList = new List<string>();
            if (cpuPattern.IsMatch(UsageRes) && memMatch.Success)
            {
                float memUsageResTotalValue = float.Parse(memMatch.Groups[1].Value);
                float memUsageResUsedValue = float.Parse(memMatch.Groups[3].Value);
                float memUsageResValue = (memUsageResUsedValue / memUsageResTotalValue) * 100;

                // 获取1分钟内负载
                double average1 = double.Parse(loadMatch.Groups[1].Value);
                // 获取5分钟内负载
                double average5 = double.Parse(loadMatch.Groups[2].Value);
                // 获取15分钟内负载
                double average15 = double.Parse(loadMatch.Groups[3].Value);
                // 计算负载
                double average1Percentage = 0.0;
                double average5Percentage = 0.0;
                double average15Percentage = 0.0;
                try
                {
                    // 此处的计算基于Load average定义，每个核心有一个任务在执行是最佳满载状态(100%)
                    // 1分钟内
                    average1Percentage = average1 * 100 / double.Parse(CPUCoreRes);
                    // 5分钟内
                    average5Percentage = average5 * 100 / double.Parse(CPUCoreRes);
                    // 15分钟内
                    average15Percentage = average15 * 100 / double.Parse(CPUCoreRes);
                }
                catch
                {
                    //double.Parse(CPUCoreRes)失败
                }

                // 在输入文本中查找匹配项并构建结果字符串
                foreach (Match match in cpuPattern.Matches(UsageRes))
                {
                    string coreInfo = $"{match.Groups[1].Value}";
                    cpuUsageList.Add(coreInfo);
                }

                // 计算核心平均占用率
                double averageUsage = 0;
                if (cpuUsageList.Count > 0)
                {
                    double totalUsage = 0;
                    foreach (var coreInfo in cpuUsageList)
                    {
                        totalUsage += double.Parse(coreInfo);
                    }
                    averageUsage = totalUsage / cpuUsageList.Count;
                }
                //throw new Exception($"Invalid argument: {string.Join(", ", cpuUsageList)}");
                return new string[] {
                    $"{(int)averageUsage}%",                            //0 平均占用
                    $"{memUsageResValue.ToString().Split('.')[0]}%",    //1
                    $"{string.Join(", ", cpuUsageList)}",               //2
                    $"{UsageRes}",                                      //3
                    $"{memUsageResTotalValue}",                         //4 物理内存量
                    $"{average1}",                                      //5 1分钟内负载
                    $"{average5}",                                      //6 5分钟内负载
                    $"{average15}",                                     //7 15分钟内负载
                    $"{average1Percentage}",                            //8 1分钟负载百分比
                    $"{average5Percentage}",                            //9 5分钟内负载百分比
                    $"{average15Percentage}",                           //10 15分钟内负载百分比
                };
            }
            else
            {
                return new string[] {
                    "0%",
                    "0%",
                    "0,0",
                    "Err",
                    "0.0",
                    "0",
                    "0",
                    "0",
                    "0",
                    "0",
                    "0"};
            }

        }
        public static async Task<string[]> GetLinuxNetAsync(CMSModel cmsModel)
        {
            string netSentCMD = "ifconfig";

            // 创建 Stopwatch 实例
            Stopwatch stopwatch = new Stopwatch();

            string result0s = await SendSSHCommandAsync(netSentCMD, cmsModel);
            // 开始计时
            stopwatch.Start();
            string result1s = await SendSSHCommandAsync(netSentCMD, cmsModel);
            // 停止计时
            stopwatch.Stop();
            // 获取经过的时间
            decimal elapsedTime = stopwatch.ElapsedMilliseconds;

            Regex XPattern = new Regex(@"eth0\s+Link.*?RX\s+bytes:(\d+)\s+\(.*?\)\s+TX\s+bytes:(\d+)\s+\(.*?\)", RegexOptions.Singleline);

            Match XMatch0s = XPattern.Match(result0s);
            Match XMatch1s = XPattern.Match(result1s);

            if (XMatch0s.Success && XMatch1s.Success)
            {
                // 解析结果
                decimal netReceivedValue0s = decimal.Parse(XMatch0s.Groups[1].Value);
                decimal netReceivedValue1s = decimal.Parse(XMatch1s.Groups[1].Value);
                decimal netSentValue0s = decimal.Parse(XMatch0s.Groups[2].Value);
                decimal netSentValue1s = decimal.Parse(XMatch1s.Groups[2].Value);

                decimal netReceivedValue = (netReceivedValue1s - netReceivedValue0s) * 1000 / elapsedTime;
                decimal netSentValue = (netSentValue1s - netSentValue0s) * 1000 / elapsedTime;

                string netReceivedRes = NetUnitConversion(netReceivedValue);
                string netSentRes = NetUnitConversion(netSentValue);
                return new string[] { $"{netReceivedRes + "/s ↓"}", $"{netSentRes + "/s ↑"}" };
            }
            else
            {
                return new string[] { "0B/s ↓", "0B/s ↑" };
            }
        }
        public static async Task<string> GetLinuxHostName(CMSModel cmsModel)
        {
            string CMD = "hostname";
            CMD = await SendSSHCommandAsync(CMD, cmsModel);

            if (CMD == "")
            {
                CMD = "uci get system.@system[0].hostname";
                CMD = await SendSSHCommandAsync(CMD, cmsModel);
            }

            return CMD.Split('\n')[0];
        }
        public static async Task<string> GetLinuxUpTime(CMSModel cmsModel)
        {
            string CMD = "uptime | awk '{print $3 \" \" $4}'";
            CMD = await SendSSHCommandAsync(CMD, cmsModel);

            return CMD.Split(',')[0];
        }

        // 获取Linux挂载情况
        public static async Task<List<MountInfo>> GetLinuxMountInfo(CMSModel cmsModel)
        {
            // P - 防止换行
            // 2>&1 - 将标准错误输出重定向到标准输出，这样可以在管道中处理错误消息。
            string CMD = "df -hP 2>&1";
            CMD = await SendSSHCommandAsync(CMD, cmsModel);

            List<MountInfo> mountInfos = MountInfoParse(CMD);

            return mountInfos;
        }

        // 获取Linux网络情况 
        public static async Task<List<NetworkInterfaceInfo>> GetLinuxNetworkInterfaceInfo(CMSModel cmsModel)
        {
            string CMD = "ifconfig";
            CMD = await SendSSHCommandAsync(CMD, cmsModel);

            List<NetworkInterfaceInfo> networkInterfaceInfos = NetworkInterfaceInfoParse(CMD);

            return networkInterfaceInfos;
        }






















        // OpenWRT系统下的负载查询（CPU占用、内存占用、负载）
        public static async Task<string[]> GetOpenWRTUsageAsync(CMSModel cmsModel)
        {
            // top查询
            string UsageCMD = "top -bn1";
            string UsageRes = await SendSSHCommandAsync(UsageCMD, cmsModel);
            // CPU核心数
            string CPUCoreCMD = "cat /proc/cpuinfo | grep processor | wc -l";
            string CPUCoreRes = await SendSSHCommandAsync(CPUCoreCMD, cmsModel);

            // 使用正则取出CPU占用信息
            Regex cpuRegex = new Regex(@"CPU:\s+(\d+)% usr\s+(\d+)% sys\s+(\d+)% nic\s+(\d+)% idle\s+(\d+)% io\s+(\d+)% irq\s+(\d+)% sirq");
            // 使用正则取出内存占用信息
            Regex memRegex = new Regex(@"Mem:\s+(\d+)K used,\s+(\d+)K free");
            // 使用正则取出负载信息
            Regex loadRegex = new Regex(@"Load average: (\d+\.\d+) (\d+\.\d+) (\d+\.\d+) (\d+)/(\d+) (\d+)");


            Match cpuMatch = cpuRegex.Match(UsageRes);
            Match memMatch = memRegex.Match(UsageRes);
            Match loadMatch = loadRegex.Match(UsageRes);

            // CPU 都无法匹配，则后续工作毫无意义。
            if (cpuMatch.Success)
            {
                // 获取使用内存容量
                double usedMemory = double.Parse(memMatch.Groups[1].Value);
                // 获取空闲内存容量
                double freeMemory = double.Parse(memMatch.Groups[2].Value);
                // 获取1分钟内负载
                double average1 = double.Parse(loadMatch.Groups[1].Value);
                // 获取5分钟内负载
                double average5 = double.Parse(loadMatch.Groups[2].Value);
                // 获取15分钟内负载
                double average15 = double.Parse(loadMatch.Groups[3].Value);

                // 计算内存占用百分比
                double memoryPercentage = (usedMemory / (usedMemory + freeMemory)) * 100;
                // 计算物理内存大小
                double totalMemory = usedMemory + freeMemory;
                // 单位换算（原本是KB单位）
                string totalMemoryStr = NetUnitConversion(decimal.Parse($"{totalMemory * 1024}"));


                // 计算负载
                double average1Percentage = 0.0;
                double average5Percentage = 0.0;
                double average15Percentage = 0.0;
                try
                {
                    // 此处的计算基于Load average定义，每个核心有一个任务在执行是最佳满载状态(100%)
                    // 1分钟内
                    average1Percentage = average1 * 100 / double.Parse(CPUCoreRes);
                    // 5分钟内
                    average5Percentage = average5 * 100 / double.Parse(CPUCoreRes);
                    // 15分钟内
                    average15Percentage = average15 * 100 / double.Parse(CPUCoreRes);
                }
                catch
                {
                    //double.Parse(CPUCoreRes)失败
                }

                // 返回结果要单纯的数字，百分号或其他单位应在View处理添加
                return new string[] {
                    $"{cpuMatch.Groups[1].Value}",          //0 CPU占用
                    $"{memoryPercentage}",                  //1 内存占用
                    $"{average1}",                          //2 1分钟内负载
                    $"{average5}",                          //3 5分钟内负载
                    $"{average15}",                         //4 15分钟内负载
                    $"{average1Percentage}",                //5 1分钟负载百分比
                    $"{average5Percentage}",                //6 5分钟内负载百分比
                    $"{average15Percentage}",               //7 15分钟内负载百分比
                    $"{totalMemoryStr}"                    //8 物理内存大小
                };
            }
            else
            {
                return new string[] {
                    "0",
                    "0",
                    "0",
                    "0",
                    "0",
                    "0",
                    "0",
                    "0",
                    "0"
                };
            }
        }
        public static async Task<string> GetOpenWRTCPUCoreUsageAsync(CMSModel cmsModel)
        {
            // 各CPU核心占用（mpstat通常包含在sysstat软件包中）
            string CPUUsageCMD = "mpstat -P ALL";
            string CPUUsageRes = await SendSSHCommandAsync(CPUUsageCMD, cmsModel);

            List<string> cpuUsageList = new List<string>();

            // 使用换行符分割输出
            string[] lines = CPUUsageRes.Split('\n');

            // 遍历每行，忽略首行标题行
            for (int i = 4; i < lines.Length - 1; i++)
            {
                // 使用空格分割每行，并取得最后一列（%idle）
                string[] columns = lines[i].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string cpuUsage = $"{columns[2]}";
                cpuUsageList.Add(cpuUsage);
            }

            //throw new Exception($"{lines[lines.Length - 2]}");
            return $"{string.Join(", ", cpuUsageList)}";
        }
        public static async Task<string> GetOpenWRTHostName(CMSModel cmsModel)
        {
            string CMD = "uci get system.@system[0].hostname";
            CMD = await SendSSHCommandAsync(CMD, cmsModel);

            return CMD.Split('\n')[0];
        }









        // 获取Linux系统CPU占用百分比
        public static async Task<List<List<string>>> GetLinuxCPUUsageAsync(CMSModel cmsModel)
        {
            // 结果格式如下：
            // cpu  697687 0 1332141 93898629 1722210 0 840664 0 0 0
            // cpu0 171727 0 309858 23571901 565476 0 3820 0 0 0
            // cpu1 163341 0 297540 23583515 578130 0 277 0 0 0
            // cpu2 155832 0 299665 23203048 129886 0 834464 0 0 0
            // cpu3 206787 0 425078 23540165 448718 0 2103 0 0 0
            //
            // CPU 用户态 用户态低优先级 系统态 空闲 I/O等待 无意义 硬件中断 软件中断 steal_time guest_nice进程
            string CPUUsageCMD = "cat /proc/stat | grep cpu";
            string CPUUsageRes = await SendSSHCommandAsync(CPUUsageCMD, cmsModel);

            // 解析结果
            // 用于保存结果的List
            List<List<string>> cpuUsageList = new List<List<string>>();

            // 以换行符为准，按行分割结果
            string[] lines = CPUUsageRes.Split('\n');

            // 遍历每行
            foreach (string line in lines)
            {
                // 检查是否以 cpu 开头
                if (line.StartsWith("cpu"))
                {
                    // 以空格分割，并去除空白项
                    string[] fields = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    // 保存当前 CPU 的使用情况
                    List<string> cpuUsage = new List<string>();

                    // 计算CPU总事件（单行之和）
                    long totalCpuTime = 0;
                    for (int i = 1; i < fields.Length; i++)
                    {
                        if (long.TryParse(fields[i], out long cpuTime))
                        {
                            totalCpuTime += cpuTime;
                        }
                    }

                    // 计算CPU占用率百分比
                    double user1 = (double)long.Parse(fields[1]) / totalCpuTime * 100; // 用户态
                    double user2 = (double)long.Parse(fields[2]) / totalCpuTime * 100; // 用户态低优先级

                    // 添加 CPU 用户态 用户态低优先级 系统态 空闲 I/O等待 无意义 硬件中断 软件中断 steal_time guest_nice进程的占用百分比
                    cpuUsage.Add(((double)(totalCpuTime - long.Parse(fields[4])) / totalCpuTime * 100).ToString("F2"));//0 总占比
                    cpuUsage.Add((user1 + user2).ToString("F2")); //1 用户态 + 用户态低优先级
                    cpuUsage.Add(((double)long.Parse(fields[3]) / totalCpuTime * 100).ToString("F2")); // 系统态
                    cpuUsage.Add(((double)long.Parse(fields[4]) / totalCpuTime * 100).ToString("F2")); // 空闲
                    cpuUsage.Add(((double)long.Parse(fields[5]) / totalCpuTime * 100).ToString("F2")); // I/O等待
                    cpuUsage.Add(((double)long.Parse(fields[6]) / totalCpuTime * 100).ToString("F2")); // 无意义
                    cpuUsage.Add(((double)long.Parse(fields[7]) / totalCpuTime * 100).ToString("F2")); // 硬件中断
                    cpuUsage.Add(((double)long.Parse(fields[8]) / totalCpuTime * 100).ToString("F2")); // 软件中断
                    cpuUsage.Add(((double)long.Parse(fields[9]) / totalCpuTime * 100).ToString("F2")); // steal_time
                    cpuUsage.Add(((double)long.Parse(fields[10]) / totalCpuTime * 100).ToString("F2")); // guest_nice进程

                    cpuUsageList.Add(cpuUsage);
                }
            }

            return cpuUsageList;
        }

        // 获取Linux系统内存占用百分比
        public static async Task<List<string>> GetLinuxMEMUsageAsync(CMSModel cmsModel)
        {
            // 结果格式如下
            // MemTotal:        3902716 kB
            // MemFree:          151924 kB
            // MemAvailable:    2799072 kB
            // SwapCached:        66680 kB
            // SwapTotal:       4439980 kB
            // SwapFree:        3741944 kB
            string MEMUsageCMD = "cat /proc/meminfo | grep -E 'Mem|Swap'";
            string MEMUsageRes = await SendSSHCommandAsync(MEMUsageCMD, cmsModel);

            List<string> parsedResults = new List<string>();

            // 定义正则表达式模式
            Regex pattern = new Regex(@"(\w+):\s+(\d+)\s+(\w+)");

            // 使用正则表达式进行匹配
            MatchCollection matches = pattern.Matches(MEMUsageRes);

            // 遍历匹配结果
            foreach (Match match in matches)
            {
                // 检查匹配是否成功
                if (match.Success)
                {
                    // 构造解析结果字符串并添加到列表中
                    //string result = $"{match.Groups[1].Value}: {match.Groups[2].Value} {match.Groups[3].Value}";
                    string result = $"{match.Groups[2].Value}";
                    parsedResults.Add(result);
                }
            }

            return parsedResults;
        }

        // 获取系统1、5、15分钟负载
        public static async Task<string[]> GetLinuxLoadAverageAsync(CMSModel cmsModel)
        {

            // top查询
            string UsageCMD = "top 1 -bn1";
            string UsageRes = await SendSSHCommandAsync(UsageCMD, cmsModel);

            // CPU核心数
            string CPUCoreCMD = "cat /proc/cpuinfo | grep processor | wc -l";
            string CPUCoreRes = await SendSSHCommandAsync(CPUCoreCMD, cmsModel);

            // 使用正则取出负载信息
            Regex loadRegex = new Regex(@"load average: (\d+\.\d+), (\d+\.\d+), (\d+\.\d+)");

            Match loadMatch = loadRegex.Match(UsageRes);

            double average1 = .0;
            double average5 = .0;
            double average15 = .0;
            double average1Percentage = .0;
            double average5Percentage = .0;
            double average15Percentage = .0;
            try
            {
                // 获取1分钟内负载
                average1 = double.Parse(loadMatch.Groups[1].Value);
                // 获取5分钟内负载
                average5 = double.Parse(loadMatch.Groups[2].Value);
                // 获取15分钟内负载
                average15 = double.Parse(loadMatch.Groups[3].Value);

                // 此处的计算基于Load average定义，每个核心有一个任务在执行是最佳满载状态(100%)
                // 1分钟内
                average1Percentage = average1 * 100 / double.Parse(CPUCoreRes);
                // 5分钟内
                average5Percentage = average5 * 100 / double.Parse(CPUCoreRes);
                // 15分钟内
                average15Percentage = average15 * 100 / double.Parse(CPUCoreRes);
            }
            catch
            {
                //double.Parse(CPUCoreRes)失败
            }
            return new string[] {
                    $"{average1}",                                      //0 1分钟内负载
                    $"{average5}",                                      //1 5分钟内负载
                    $"{average15}",                                     //2 15分钟内负载
                    $"{average1Percentage}",                            //3 1分钟负载百分比
                    $"{average5Percentage}",                            //4 5分钟内负载百分比
                    $"{average15Percentage}",                           //5 15分钟内负载百分比
                };
        }














        public static string NetUnitConversion(decimal netValue)
        {
            if (netValue >= (1000 * 1000 * 1000))
            {
                return (netValue / 1024 / 1024 / 1024).ToString("F2") + " GB";
            }
            else if (netValue >= (1000 * 1000))
            {
                return (netValue / 1024 / 1024).ToString("F2") + " MB";
            }
            else if (netValue >= 1000)
            {
                return (netValue / 1024).ToString("F2") + " KB";
            }
            else
            {
                return netValue + " B";
            }
        }
        // 处理 df
        public static List<MountInfo> MountInfoParse(string input)
        {
            var mountInfos = new List<MountInfo>();

            // 按行分割输入
            var lines = input.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            // 跳过标题行
            foreach (var line in lines.Skip(1))
            {
                // 检查是否以 / 开头
                if (line.StartsWith("/"))
                {
                    var columns = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    // 共6列
                    if (columns.Length == 6)
                    {
                        var mountInfo = new MountInfo
                        {
                            FileSystem = columns[0],
                            Size = columns[1],
                            Used = columns[2],
                            Avail = columns[3],
                            UsePercentage = columns[4],
                            MountedOn = columns[5]
                        };

                        mountInfos.Add(mountInfo);
                    }
                    else
                    {
                        Console.WriteLine("Invalid line format: " + line);
                    }
                }
            }
            return mountInfos;
        }


        // 处理 ifconfig
        public static List<NetworkInterfaceInfo> NetworkInterfaceInfoParse(string input)
        {
            //throw new Exception($"{input}");
            var interfaceInfos = new List<NetworkInterfaceInfo>();

            var pattern = @"(\w+)\s+Link encap:(.*?)\s+(?:HWaddr\s+(\S+))?(.*?)((?=\n\n)|(?=$))";

            var matches = Regex.Matches(input, pattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                var interfaceInfo = new NetworkInterfaceInfo();

                interfaceInfo.Name = match.Groups[1].Value;
                interfaceInfo.LinkEncap = match.Groups[2].Value;
                interfaceInfo.HWAddr = match.Groups[3].Value;

                var infoText = match.Groups[4].Value;

                interfaceInfo.InetAddr = ExtractValue(infoText, @"inet addr:(\S+)");
                interfaceInfo.Bcast = ExtractValue(infoText, @"Bcast:(\S+)");
                interfaceInfo.Mask = ExtractValue(infoText, @"Mask:(\S+)");
                interfaceInfo.Inet6Addr = ExtractValue(infoText, @"inet6 addr:(\S+)");
                interfaceInfo.Scope = ExtractValue(infoText, @"Scope:(\S+)");
                interfaceInfo.Status = infoText.Contains("UP") ? "UP" : "DOWN";
                interfaceInfo.MTU = ExtractValue(infoText, @"MTU:(\S+)");
                interfaceInfo.Metric = ExtractValue(infoText, @"Metric:(\S+)");
                interfaceInfo.RXPackets = ExtractValue(infoText, @"RX packets:(\S+)");
                interfaceInfo.RXErrors = ExtractValue(infoText, @"errors:(\S+)");
                interfaceInfo.RXDropped = ExtractValue(infoText, @"dropped:(\S+)");
                interfaceInfo.RXOverruns = ExtractValue(infoText, @"overruns:(\S+)");
                interfaceInfo.RXFrame = ExtractValue(infoText, @"frame:(\S+)");
                interfaceInfo.TXPackets = ExtractValue(infoText, @"TX packets:(\S+)");
                interfaceInfo.TXErrors = ExtractValue(infoText, @"errors:(\S+)");
                interfaceInfo.TXDropped = ExtractValue(infoText, @"dropped:(\S+)");
                interfaceInfo.TXOverruns = ExtractValue(infoText, @"overruns:(\S+)");
                interfaceInfo.TXCarrier = ExtractValue(infoText, @"carrier:(\S+)");
                interfaceInfo.Collisions = ExtractValue(infoText, @"collisions:(\S+)");
                interfaceInfo.TXQueueLen = ExtractValue(infoText, @"txqueuelen:(\S+)");
                interfaceInfo.RXBytes = NetUnitConversion(decimal.Parse(ExtractValue(infoText, @"RX bytes:(\S+)")));
                interfaceInfo.TXBytes = NetUnitConversion(decimal.Parse(ExtractValue(infoText, @"TX bytes:(\S+)")));

                interfaceInfos.Add(interfaceInfo);
            }

            return interfaceInfos;
        }
        private static string ExtractValue(string input, string pattern)
        {
            var match = Regex.Match(input, pattern);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return null;
        }


























        public static string EncryptString(string plainText, SymmetricAlgorithm symmetricAlgorithm)
        {
            // 创建加密器
            ICryptoTransform encryptor = symmetricAlgorithm.CreateEncryptor(symmetricAlgorithm.Key, symmetricAlgorithm.IV);

            // 创建内存流，用于写入加密后的数据
            using (MemoryStream memoryStream = new MemoryStream())
            {
                // 创建加密流
                using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                {
                    // 将字符串转换为字节数组并写入加密流
                    byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                    cryptoStream.Write(plainBytes, 0, plainBytes.Length);
                    cryptoStream.FlushFinalBlock();
                }
                // 返回加密后的数据，以Base64编码的字符串形式
                return Convert.ToBase64String(memoryStream.ToArray());
            }
        }

        public static string DecryptString(string cipherText, SymmetricAlgorithm symmetricAlgorithm)
        {
            // 创建解密器
            ICryptoTransform decryptor = symmetricAlgorithm.CreateDecryptor(symmetricAlgorithm.Key, symmetricAlgorithm.IV);

            // 创建内存流，用于写入解密后的数据
            using (MemoryStream memoryStream = new MemoryStream(Convert.FromBase64String(cipherText)))
            {
                // 创建解密流
                using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                {
                    // 从解密流中读取解密后的字节数组
                    using (StreamReader streamReader = new StreamReader(cryptoStream))
                    {
                        return streamReader.ReadToEnd();
                    }
                }
            }
        }

        public static string LoadKeyFromLocalSettings()
        {
            // 从 localSettings 中加载密钥
            var localSettings = ApplicationData.Current.LocalSettings;
            return localSettings.Values["Key"] as string;
        }

        public static string LoadIVFromLocalSettings()
        {
            // 从 localSettings 中加载初始化向量
            var localSettings = ApplicationData.Current.LocalSettings;
            return localSettings.Values["IV"] as string;
        }

        public static void SaveKeyToLocalSettings(string key)
        {
            // 将密钥保存到 localSettings 中
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["Key"] = key;
        }

        public static void SaveIVToLocalSettings(string iv)
        {
            // 将初始化向量保存到 localSettings 中
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["IV"] = iv;
        }

        public static string GenerateRandomKey()
        {
            // 生成一个随机的密钥
            byte[] key = new byte[32];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(key);
            }
            return Convert.ToBase64String(key);
        }

        public static string GenerateRandomIV()
        {
            // 生成一个随机的初始化向量
            byte[] iv = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(iv);
            }
            return Convert.ToBase64String(iv);
        }




































        public static void SSHTerminal(CMSModel cmsModel)
        {
            //string KeyPath, string User, string Domain, string Port
            // 创建一个新的进程
            Process process = new Process();
            // 指定运行PowerShell
            process.StartInfo.FileName = "PowerShell.exe";
            // 命令
            if (cmsModel.SSHKeyIsOpen == "True")
            {
                process.StartInfo.Arguments = $"ssh -i {cmsModel.SSHKey} {cmsModel.SSHUser}@{cmsModel.HostIP} -p {cmsModel.HostPort}";
            }
            else
            {
                process.StartInfo.Arguments = $"ssh {cmsModel.SSHUser}@{cmsModel.HostIP} -p {cmsModel.HostPort}";
            }
            // 是否使用操作系统shell启动
            process.StartInfo.UseShellExecute = false;
            // 是否在新窗口中启动该进程的值 (不显示程序窗口)
            process.StartInfo.CreateNoWindow = false;
            // 进程开始
            process.Start();
            // 等待执行结束
            //process.WaitForExit();
            // 进程关闭
            process.Close();
        }
    }
}
