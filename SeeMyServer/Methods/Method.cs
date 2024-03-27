using Microsoft.UI.Xaml.Shapes;
using Newtonsoft.Json;
using Renci.SshNet;
using Renci.SshNet.Security;
using SeeMyServer.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
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
        // SSH执行
        public static string SendSSHCommand(string sshCommand, string sshHost, string sshPort, string sshUser, string sshPasswd, string sshKey, string privateKeyIsOpen)
        {
            sshHost = DomainToIp(sshHost, "IPv4").ToString();
            try
            {
                bool usePrivateKey = string.Equals(privateKeyIsOpen, "True", StringComparison.OrdinalIgnoreCase);
                SshClient sshClient = InitializeSshClient(sshHost, int.Parse(sshPort), sshUser, sshPasswd, sshKey, usePrivateKey);

                if (sshClient != null)
                {
                    return ExecuteSshCommand(sshClient, sshCommand);
                }
                else
                {
                    return "SSH 客户端初始化失败。";
                }
            }
            catch (Exception ex)
            {
                return "SSH 操作失败：" + ex.Message;
            }
        }
        // SSH初始化
        private static SshClient InitializeSshClient(string sshHost, int sshPort, string sshUser, string sshPasswd, string sshKey, bool usePrivateKey)
        {
            try
            {
                if (usePrivateKey)
                {
                    PrivateKeyFile privateKeyFile = new PrivateKeyFile(sshKey);
                    ConnectionInfo connectionInfo = new ConnectionInfo(sshHost, sshPort, sshUser, new PrivateKeyAuthenticationMethod(sshUser, new PrivateKeyFile[] { privateKeyFile }));
                    return new SshClient(connectionInfo);
                }
                else
                {
                    return new SshClient(sshHost, sshPort, sshUser, sshPasswd);
                }
            }
            catch
            {
                return null;
            }
        }
        // SSH返回
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
                        return "错误：" + SSHCommand.Error;
                    }
                    else
                    {
                        return SSHCommand.Result;
                    }
                }
                return "SSH 命令执行失败。";
            }
            finally
            {
                sshClient.Disconnect();
                sshClient.Dispose();
            }
        }
        // 获取域名对应的IP
        public static IPAddress DomainToIp(string domain, string ipVersion)
        {
            IPAddress ipAddress;
            if (IPAddress.TryParse(domain, out ipAddress))
            {
                // 是IP
                if ((ipVersion == "IPv4" && ipAddress.AddressFamily == AddressFamily.InterNetwork) ||
                    (ipVersion == "IPv6" && ipAddress.AddressFamily == AddressFamily.InterNetworkV6))
                {
                    return ipAddress;
                }
                else
                {
                    throw new ArgumentException("IP version mismatch");
                }
            }
            else
            {
                // 是域名或其他输入
                IPAddress[] addressList = Dns.GetHostEntry(domain).AddressList;

                if (ipVersion == "IPv4")
                {
                    foreach (IPAddress address in addressList)
                    {
                        if (address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            return address;
                        }
                    }
                }
                else if (ipVersion == "IPv6")
                {
                    foreach (IPAddress address in addressList)
                    {
                        if (address.AddressFamily == AddressFamily.InterNetworkV6)
                        {
                            return address;
                        }
                    }
                }

                throw new ArgumentException("No matching IP address found");
            }
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
                    return "保存成功";
                }
                else if (status == FileUpdateStatus.CompleteAndRenamed)
                {
                    // 重命名并保存成功
                    return "重命名并保存成功";
                }
                else
                {
                    // 文件无法保存！
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
        private static async Task<string> SendSSHCommandAsync(string command, CMSModel cmsModel)
        {
            return await Task.Run(() =>
            {
                return SendSSHCommand(command, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
            });
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
                return new string[] { $"{(int)averageUsage}%", $"{int.Parse(memUsageResValue.ToString().Split('.')[0])}%", $"{string.Join(", ", cpuUsageList)}", $"{UsageRes}", $"{memUsageResTotalValue}" };
            }
            else
            {
                return new string[] { "0%", "0%", "Err,Err", "Err" };
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










        public static async Task<string> GetWindowsCPUUsageAsync(CMSModel cmsModel)
        {
            string cpuUsageCMD = "powershell -Command \"(Get-Counter '\\Processor Information(_Total)\\% Processor Utility').CounterSamples.CookedValue\"";
            string cpuUsageRes = await SendSSHCommandAsync(cpuUsageCMD, cmsModel);
            int cpuUsageResValue = int.Parse(cpuUsageRes.Split('.')[0]);
            return Math.Min(Math.Max(cpuUsageResValue, 0), 100).ToString() + "%";
        }

        public static async Task<string> GetWindowsMemoryUsageAsync(CMSModel cmsModel)
        {
            string memUsageCMD = "powershell -Command \"((($totalMemory = (Get-WmiObject -Class Win32_OperatingSystem).TotalVisibleMemorySize) - (Get-WmiObject -Class Win32_OperatingSystem).FreePhysicalMemory) / $totalMemory * 100)\"";
            string memUsageRes = await SendSSHCommandAsync(memUsageCMD, cmsModel);
            int memUsageResValue = int.Parse(memUsageRes.Split('.')[0]);
            return Math.Min(Math.Max(memUsageResValue, 0), 100).ToString() + "%";
        }
        public static async Task<string> GetWindowsNetSentAsync(CMSModel cmsModel)
        {
            string netSentCMD = "powershell -Command \"(Get-Counter '\\Network Interface(*)\\Bytes Sent/sec').CounterSamples.CookedValue | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum\"";
            string netSentRes = await SendSSHCommandAsync(netSentCMD, cmsModel);
            // 获取命令输出的值并转换为整数
            int netSentValue = int.Parse(netSentRes.Split('.')[0]);
            netSentRes = NetUnitConversion(netSentValue);
            return netSentRes + "/s ↑";
        }

        public static async Task<string> GetWindowsNetReceivedAsync(CMSModel cmsModel)
        {
            string netReceivedCMD = "powershell -Command \"(Get-Counter '\\Network Interface(*)\\Bytes Received/sec').CounterSamples.CookedValue | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum\"";
            string netReceivedRes = await SendSSHCommandAsync(netReceivedCMD, cmsModel);
            // 获取命令输出的值并转换为整数
            int netReceivedValue = int.Parse(netReceivedRes.Split('.')[0]);
            netReceivedRes = NetUnitConversion(netReceivedValue);
            return netReceivedRes + "/s ↓";
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

        // 处理 df -h
        public static List<MountInfo> MountInfoParse(string input)
        {
            var mountInfos = new List<MountInfo>();

            // 按行分割输入
            var lines = input.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            //throw new Exception($"{lines[1]}");

            // 跳过标题行
            foreach (var line in lines.Skip(1))
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
                //if (match.Groups[1].Value == "lo")
                //    throw new Exception($"{match}");
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
    }
}
