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
                    logger.LogError($"{sshHost} 使用了无效的 SSH 端口号：{sshPort}");
                    return "无效的 SSH 端口号。";
                }

                bool usePrivateKey = string.Equals(privateKeyIsOpen, "True", StringComparison.OrdinalIgnoreCase);
                using (SshClient sshClient = InitializeSshClient(sshHost, port, sshUser, sshPasswd, sshKey, usePrivateKey))
                {
                    if (sshClient == null)
                    {
                        logger.LogError($"{sshHost} SSH 客户端初始化失败。");
                        return "SSH 客户端初始化失败。";
                    }

                    return ExecuteSshCommand(sshClient, sshCommand);
                }
            }
            catch (Exception ex)
            {
                logger.LogError("SSH 操作失败：" + ex.Message);
                return "SSH 操作失败：" + ex.Message;
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
                        return "SSH 命令执行错误：" + SSHCommand.Error;
                    }
                    else
                    {
                        return SSHCommand.Result;
                    }
                }
                logger.LogError($"{sshCommand} SSH 命令执行失败。");
                return "SSH 命令执行失败。";
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
            string UsageCMD = "top 1 -bn1";
            string UsageRes = await SendSSHCommandAsync(UsageCMD, cmsModel);
            //throw new Exception($"{UsageRes}");

            // 定义正则表达式模式 匹配CPU占用
            Regex cpuPattern = new Regex(@"%Cpu\d+\s+:\s*(\d*\.\d+)\s+us,\s*(\d*\.\d+)\s+sy,\s*(\d*\.\d+)\s+ni,\s*(\d*\.\d+)\s+id,\s*(\d*\.\d+)\s+wa,\s*(\d*\.\d+)\s+hi,\s*(\d*\.\d+)\s+si,\s*(\d*\.\d+)\s+st");

            // 定义正则表达式模式 匹配MEM占用
            string memPattern = @"GiB\s+Mem\s+:\s+([\d\.]+)\s+total,\s+([\d\.]+)\s+free,\s+([\d\.]+)\s+used,\s+([\d\.]+)\s+buff/cache";

            // 创建列表以存储结果
            List<string> cpuUsageList = new List<string>();

            // 匹配输入字符串中的模式
            Match memMatch = Regex.Match(UsageRes, memPattern);

            if (cpuPattern.IsMatch(UsageRes) && memMatch.Success)
            {
                float memUsageResTotalValue = float.Parse(memMatch.Groups[1].Value);
                float memUsageResUsedValue = float.Parse(memMatch.Groups[3].Value);
                float memUsageResValue = (memUsageResUsedValue / memUsageResTotalValue) * 100;

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
                return new string[] { $"{(int)averageUsage}%", $"{memUsageResValue.ToString().Split('.')[0]}%", $"{string.Join(", ", cpuUsageList)}", $"{UsageRes}", $"{memUsageResTotalValue}" };
            }
            else
            {
                return new string[] { "0%", "0%", "0,0", "Err", "0.0" };
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
            BigInteger elapsedTime = new BigInteger(stopwatch.ElapsedMilliseconds);

            Regex XPattern = new Regex(@"eth0\s+Link.*?RX\s+bytes:(\d+)\s+\(.*?\)\s+TX\s+bytes:(\d+)\s+\(.*?\)", RegexOptions.Singleline);

            Match XMatch0s = XPattern.Match(result0s);
            Match XMatch1s = XPattern.Match(result1s);

            if (XMatch0s.Success && XMatch1s.Success)
            {
                // 解析结果为 BigInteger
                BigInteger netReceivedValue0s = BigInteger.Parse(XMatch0s.Groups[1].Value);
                BigInteger netReceivedValue1s = BigInteger.Parse(XMatch1s.Groups[1].Value);
                BigInteger netSentValue0s = BigInteger.Parse(XMatch0s.Groups[2].Value);
                BigInteger netSentValue1s = BigInteger.Parse(XMatch1s.Groups[2].Value);

                BigInteger netReceivedValue = (netReceivedValue1s - netReceivedValue0s) * 1000 / elapsedTime;
                BigInteger netSentValue = (netSentValue1s - netSentValue0s) * 1000 / elapsedTime;

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
            string CMD = "df -hP";
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























        public static async Task<string[]> GetOpenWRTCPUUsageAsync(CMSModel cmsModel)
        {
            string UsageCMD = "top -bn1";
            string UsageRes = await SendSSHCommandAsync(UsageCMD, cmsModel);

            Regex cpuRegex = new Regex(@"CPU:\s+(\d+)% usr\s+(\d+)% sys\s+(\d+)% nic\s+(\d+)% idle\s+(\d+)% io\s+(\d+)% irq\s+(\d+)% sirq");
            Regex memRegex = new Regex(@"Mem:\s+(\d+)K used,\s+(\d+)K free");

            Match cpuMatch = cpuRegex.Match(UsageRes);
            Match memMatch = memRegex.Match(UsageRes);

            if (cpuMatch.Success)
            {
                // 获取使用和空闲内存的数值
                double usedMemory = double.Parse(memMatch.Groups[1].Value);
                double freeMemory = double.Parse(memMatch.Groups[2].Value);

                // 计算内存占用百分比
                int memoryPercentage = (int)((usedMemory / (usedMemory + freeMemory)) * 100);


                return new string[] { $"{cpuMatch.Groups[1].Value}%", $"{memoryPercentage}%" };
            }
            else
            {
                return new string[] { "0%", "0%" };
            }
        }
        public static async Task<string> GetOpenWRTHostName(CMSModel cmsModel)
        {
            string CMD = "uci get system.@system[0].hostname";
            CMD = await SendSSHCommandAsync(CMD, cmsModel);

            return CMD.Split('\n')[0];
        }











        // 如果目标主机用的cmd，那么上方的pwsh命令会失败。
        // 但如果目标主机是pwsh，那么加powershell -Command的命令在部分情况下也会失败
        // 所以我放弃了对cmd的兼容，要求用户必须使用pwsh作为ssh shell。
        public static async Task<string[]> GetWindowsUsageAsync(CMSModel cmsModel)
        {
            string UsageCMD = "(Get-Counter '\\Processor Information(*)\\% Processor Utility').CounterSamples.CookedValue;"
                + " \'-\';"
                + " ((($totalMemory = (Get-WmiObject -Class Win32_OperatingSystem).TotalVisibleMemorySize) - (Get-WmiObject -Class Win32_OperatingSystem).FreePhysicalMemory) / $totalMemory * 100);"
                + " \'-\';"
                + "(Get-WmiObject Win32_ComputerSystem).TotalPhysicalMemory;";
            string UsageRes = await SendSSHCommandAsync(UsageCMD, cmsModel);

            // 以分号分割字符串
            string[] UsageResA = UsageRes.Split('-');
            // 去除每个子字符串的开头和结尾的换行符
            for (int i = 0; i < UsageResA.Length; i++)
            {
                UsageResA[i] = UsageResA[i].Trim('\r', '\n');
            }
            // 第一部分是各CPU核心占用和总占用，分割出来后再处理到一个List
            string[] cpuUsageRes = UsageResA[0].Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            // 创建列表
            List<string> resultList = new List<string>();
            // 将每行添加到列表中
            foreach (string cpuUsage in cpuUsageRes)
            {
                resultList.Add(cpuUsage);
            }

            // 去掉最后两项（最后两项表示总占用）
            List<string> modifiedList = new List<string>(cpuUsageRes);
            try
            {
                modifiedList.RemoveAt(modifiedList.Count - 2);
            }
            catch (Exception ex) { logger.LogError($"CPU各核心占用获取失败：{ex.Message}"); }

            // 第二部分是内存占用
            string memUsageRes = "";
            try
            {
                memUsageRes = UsageResA[1];
            }
            catch (Exception ex) { logger.LogError($"内存占用获取失败：{ex.Message}"); }

            // 第三部分是总内存量
            string memUsageTotalRes = "";
            BigInteger memUsageTotalValue = 0;
            try
            {
                memUsageTotalRes = UsageResA[2];
                memUsageTotalValue = BigInteger.Parse(memUsageTotalRes);
                memUsageTotalRes = NetUnitConversion(memUsageTotalValue);
            }
            catch (Exception ex) { logger.LogError($"总内存量获取失败：{ex.Message}"); }

            return new string[] { $"{cpuUsageRes[cpuUsageRes.Length - 1].Split('.')[0]}%", $"{memUsageRes.Split('.')[0]}%", $"{string.Join(", ", modifiedList)}", $"{UsageRes}", $"{memUsageTotalRes}" };
        }
        public static async Task<string> GetWindowsUpTime(CMSModel cmsModel)
        {
            string CMD = "[string]::Format('{0} Days {1} Hours {2} Minutes', (New-TimeSpan -Start (Get-CimInstance Win32_OperatingSystem).LastBootUpTime).Days, (New-TimeSpan -Start (Get-CimInstance Win32_OperatingSystem).LastBootUpTime).Hours, (New-TimeSpan -Start (Get-CimInstance Win32_OperatingSystem).LastBootUpTime).Minutes);";
            CMD = await SendSSHCommandAsync(CMD, cmsModel);

            return CMD;
        }
        // 保留这个方法给一级界面用（pwsh太慢了）
        public static async Task<string> GetWindowsCPUUsageAsync(CMSModel cmsModel)
        {
            string cpuUsageCMD = "(Get-Counter '\\Processor Information(_Total)\\% Processor Utility').CounterSamples.CookedValue";
            string cpuUsageRes = await SendSSHCommandAsync(cpuUsageCMD, cmsModel);
            int cpuUsageResValue = 0;
            try
            {
                cpuUsageResValue = int.Parse(cpuUsageRes.Split('.')[0]);
            }
            catch (Exception ex)
            {
                logger.LogError($"CPU占用结果 {cpuUsageResValue} 转换失败：{ex.Message}");
            }
            return Math.Min(Math.Max(cpuUsageResValue, 0), 100).ToString() + "%";
        }

        public static async Task<string> GetWindowsMemoryUsageAsync(CMSModel cmsModel)
        {
            string memUsageCMD = "((($totalMemory = (Get-WmiObject -Class Win32_OperatingSystem).TotalVisibleMemorySize) - (Get-WmiObject -Class Win32_OperatingSystem).FreePhysicalMemory) / $totalMemory * 100)";
            string memUsageRes = await SendSSHCommandAsync(memUsageCMD, cmsModel);
            int memUsageResValue = 0;
            try
            {
                memUsageResValue = int.Parse(memUsageRes.Split('.')[0]);
            }
            catch (Exception ex)
            {
                logger.LogError($"内存占用结果 {memUsageResValue} 转换失败：{ex.Message}");
            }
            return Math.Min(Math.Max(memUsageResValue, 0), 100).ToString() + "%";
        }
        public static async Task<string> GetWindowsNetSentAsync(CMSModel cmsModel)
        {
            string netSentCMD = "(Get-Counter '\\Network Interface(*)\\Bytes Sent/sec').CounterSamples.CookedValue | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum";
            string netSentRes = await SendSSHCommandAsync(netSentCMD, cmsModel);
            // 获取命令输出的值并转换为整数
            int netSentValue = 0;
            try
            {
                netSentValue = int.Parse(netSentRes.Split('.')[0]);
            }
            catch (Exception ex)
            {
                logger.LogError($"netSent结果 {netSentValue} 转换失败：{ex.Message}");
            }
            netSentRes = NetUnitConversion(netSentValue);
            return netSentRes + "/s ↑";
        }

        public static async Task<string> GetWindowsNetReceivedAsync(CMSModel cmsModel)
        {
            string netReceivedCMD = "(Get-Counter '\\Network Interface(*)\\Bytes Received/sec').CounterSamples.CookedValue | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum";
            string netReceivedRes = await SendSSHCommandAsync(netReceivedCMD, cmsModel);
            // 获取命令输出的值并转换为整数
            int netReceivedValue = 0;
            try
            {
                netReceivedValue = int.Parse(netReceivedRes.Split('.')[0]);
            }
            catch (Exception ex)
            {
                logger.LogError($"netReceived结果 {netReceivedValue} 转换失败：{ex.Message}");
            }
            netReceivedRes = NetUnitConversion(netReceivedValue);
            return netReceivedRes + "/s ↓";
        }

        public static async Task<List<MountInfo>> GetWindowsMountInfo(CMSModel cmsModel)
        {
            string CMD = "Get-Volume | Format-Table -AutoSize -Wrap -Property DriveLetter,FriendlyName,FileSystemType,DriveType,SizeRemaining,Size";
            CMD = await SendSSHCommandAsync(CMD, cmsModel);

            List<MountInfo> mountInfos = WindowsMountInfoParse(CMD);

            return mountInfos;
        }
        public static async Task<List<NetworkInterfaceInfo>> GetWindowsNetworkInterfaceInfo(CMSModel cmsModel)
        {
            string CMD = "Get-NetAdapter | Format-Table -AutoSize -Wrap";
            CMD = await SendSSHCommandAsync(CMD, cmsModel);

            List<NetworkInterfaceInfo> networkInterfaceInfos = WindowsNetworkInterfaceInfoParse(CMD);

            return networkInterfaceInfos;
        }


















        private static string NetUnitConversion(BigInteger netValue)
        {
            if (netValue >= (1024 * 1024 * 1024))
            {
                return (netValue / 1024 / 1024 / 1024).ToString() + " GB";
            }
            else if (netValue >= (1024 * 1024))
            {
                return (netValue / 1024 / 1024).ToString() + " MB";
            }
            else if (netValue >= 1024)
            {
                return (netValue / 1024).ToString() + " KB";
            }
            else
            {
                return netValue + " B";
            }
        }

        public static List<MountInfo> WindowsMountInfoParse(string input)
        {
            List<MountInfo> mountInfos = new List<MountInfo>();

            string[] lines = input.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            // Regex pattern to match the structure of the input lines
            Regex pattern = new Regex(@"^\s*(\w)\s+([\w\s]+)?\s+(\w+)\s+(\w+)\s+(\d+)\s+(\d+)$");


            foreach (string line in lines)
            {
                Match match = pattern.Match(line);
                if (match.Success)
                {
                    MountInfo info = new MountInfo();
                    info.DriveLetter = match.Groups[1].Value.Trim();
                    info.FriendlyName = match.Groups[2].Value.Trim();
                    info.FileSystem = $"{info.FriendlyName} ({info.DriveLetter})";
                    info.FileSystemType = match.Groups[3].Value.Trim();
                    info.DriveType = match.Groups[4].Value.Trim();
                    info.SizeRemaining = NetUnitConversion(BigInteger.Parse(match.Groups[5].Value.Trim()));
                    info.Size = NetUnitConversion(BigInteger.Parse(match.Groups[6].Value.Trim()));
                    //throw new Exception($"{info.DriveLetter}");
                    try
                    {
                        float UsedValue = float.Parse(info.Size.Split(' ')[0]) - float.Parse(info.SizeRemaining.Split(' ')[0]);
                        //info.Used = UsedValue.ToString();
                        info.Used = $"{String.Format("{0:0.00}", UsedValue)} {info.Size.Substring(info.Size.Length - 2)}";
                        info.UsePercentage = (UsedValue * 100 / float.Parse(info.Size.Split(' ')[0])).ToString().Split('.')[0] + "%";
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Windows挂载占用结果转换失败：{ex.Message}");
                        info.UsePercentage = "0%";
                    }

                    mountInfos.Add(info);
                }
            }

            return mountInfos;
        }
        // 处理 df -h
        public static List<MountInfo> MountInfoParse(string input)
        {
            var mountInfos = new List<MountInfo>();

            // 按行分割输入
            var lines = input.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            // 跳过标题行
            foreach (var line in lines.Skip(1))
            {
                var columns = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                // 共6列
                if (columns.Length == 6 && columns[0] != "tmpfs" && columns[0] != "devtmpfs")
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
            return mountInfos;
        }

        public static List<NetworkInterfaceInfo> WindowsNetworkInterfaceInfoParse(string input)
        {
            List<NetworkInterfaceInfo> networkInterfaces = new List<NetworkInterfaceInfo>();

            string[] lines = input.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            // Regex pattern to match the structure of the input lines
            Regex pattern = new Regex(@"^(?<Name>[\w\s]+?)\s+(?<InterfaceDescription>.+?)\s+(?<ifIndex>\d+)\s+(?<Status>.+?)\s+(?<MacAddress>[0-9A-Fa-f-]+)\s+(?<LinkSpeed>.+)$");

            foreach (string line in lines)
            {
                Match match = pattern.Match(line);
                if (match.Success)
                {
                    NetworkInterfaceInfo info = new NetworkInterfaceInfo();
                    info.Name = match.Groups["Name"].Value.Trim();
                    info.InterfaceDescription = match.Groups["InterfaceDescription"].Value.Trim();
                    info.ifIndex = int.Parse(match.Groups["ifIndex"].Value);
                    info.Status = match.Groups["Status"].Value.Trim();
                    info.MacAddress = match.Groups["MacAddress"].Value.Trim();
                    info.LinkSpeed = match.Groups["LinkSpeed"].Value.Trim();

                    networkInterfaces.Add(info);
                }
            }

            return networkInterfaces;
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
                interfaceInfo.RXBytes = NetUnitConversion(BigInteger.Parse(ExtractValue(infoText, @"RX bytes:(\S+)")));
                interfaceInfo.TXBytes = NetUnitConversion(BigInteger.Parse(ExtractValue(infoText, @"TX bytes:(\S+)")));

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
