using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Renci.SshNet;
using Windows.Storage.Pickers;
using Windows.Storage;
using SeeMyServer.Models;
using Newtonsoft.Json;
using Windows.Storage.Provider;
using System.Diagnostics;
using System.Numerics;

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
                    //return "SSH 客户端初始化失败。";
                    return "0";
                }
            }
            catch (Exception ex)
            {
                //return "SSH 操作失败：" + ex.Message;
                return "0";
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
                        //return "错误：" + SSHCommand.Error;
                        return "0";
                    }
                    else
                    {
                        return SSHCommand.Result;
                    }
                }
                //return "SSH 命令执行失败。";
                return "0";
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



        public static async Task<string> GetLinuxCPUUsageAsync(CMSModel cmsModel)
        {
            string cpuUsageCMD = "top -bn1 | grep '^%Cpu' | sed 's/^.*://; s/,.*//; s/ *//g'";
            string cpuUsageRes = await SendSSHCommandAsync(cpuUsageCMD, cmsModel);
            int cpuUsageResValue = int.Parse(cpuUsageRes.Split('.')[0]);
            return cpuUsageResValue.ToString() + "%";
        }
        public static async Task<string> GetLinuxMemoryUsageAsync(CMSModel cmsModel)
        {
            string memUsageCMD = "free -m | awk 'NR==2{printf \"%.1f\", $3/$2*100}'";
            string memUsageRes = await SendSSHCommandAsync(memUsageCMD, cmsModel);
            int memUsageResValue = int.Parse(memUsageRes.Split('.')[0]);
            return memUsageResValue.ToString() + "%";
        }
        public static async Task<string> GetLinuxNetSentAsync(CMSModel cmsModel)
        {
            // 获取的是发送数据总量
            string netSentCMD = "ifconfig eth0 | grep 'RX bytes\\|TX bytes' | awk '{print $6}' | sed 's/.*bytes://'";

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

            // 解析结果为 BigInteger
            BigInteger netSentValue0s = BigInteger.Parse(result0s);
            BigInteger netSentValue1s = BigInteger.Parse(result1s);
            BigInteger netSentValue = (netSentValue1s - netSentValue0s) * 1000 / elapsedTime;
            string netSentRes;
            if (netSentValue >= (1024 * 1024 * 1024))
            {
                netSentRes = (netSentValue / 1024 / 1024 / 1024).ToString() + " GB";
            }
            else if (netSentValue >= (1024 * 1024))
            {
                netSentRes = (netSentValue / 1024 / 1024).ToString() + " MB";
            }
            else if (netSentValue >= 1024)
            {
                netSentRes = (netSentValue / 1024).ToString() + " KB";
            }
            else
            {
                netSentRes = netSentValue + " B";
            }
            return netSentRes + "/s ↑";
        }
        public static async Task<string> GetLinuxNetReceivedAsync(CMSModel cmsModel)
        {
            string netReceivedCMD = "ifconfig eth0 | grep 'RX bytes\\|TX bytes' | awk '{print $2}' | sed 's/.*bytes://'";

            // 创建 Stopwatch 实例
            Stopwatch stopwatch = new Stopwatch();

            string result0s = await SendSSHCommandAsync(netReceivedCMD, cmsModel);
            // 开始计时
            stopwatch.Start();
            string result1s = await SendSSHCommandAsync(netReceivedCMD, cmsModel);
            // 停止计时
            stopwatch.Stop();
            // 获取经过的时间
            BigInteger elapsedTime = new BigInteger(stopwatch.ElapsedMilliseconds);

            // 解析结果为 BigInteger
            BigInteger netReceivedValue0s = BigInteger.Parse(result0s);
            BigInteger netReceivedValue1s = BigInteger.Parse(result1s);
            BigInteger netReceivedValue = (netReceivedValue1s - netReceivedValue0s) * 1000 / elapsedTime;
            string netReceivedRes;
            if (netReceivedValue >= 1024 * 1024 * 1024)
            {
                netReceivedRes = (netReceivedValue / 1024 / 1024 / 1024).ToString() + " GB";
            }
            else if (netReceivedValue >= 1024 * 1024)
            {
                netReceivedRes = (netReceivedValue / 1024 / 1024).ToString() + " MB";
            }
            else if (netReceivedValue >= 1024)
            {
                netReceivedRes = (netReceivedValue / 1024).ToString() + " KB";
            }
            else
            {
                netReceivedRes = netReceivedValue + " B";
            }
            return netReceivedRes + "/s ↓";
        }





        public static async Task<string> GetOpenWRTCPUUsageAsync(CMSModel cmsModel)
        {
            string cpuUsageCMD = "top -bn1 | head -n 3 | grep -o 'CPU:.*' | awk '{print $2}'";
            string cpuUsageRes = await SendSSHCommandAsync(cpuUsageCMD, cmsModel);
            return cpuUsageRes.TrimEnd();
        }

        public static async Task<string> GetOpenWRTMemoryUsageAsync(CMSModel cmsModel)
        {
            string memUsageCMD = "top -bn1 | head -n 1 | awk '{used=$2; total=$2+$4; printf \"%.0f\", (used/total)*100}'";
            string memUsageRes = await SendSSHCommandAsync(memUsageCMD, cmsModel);
            return memUsageRes + "%";
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
            if (netSentValue >= (1024 * 1024 * 1024))
            {
                netSentRes = (netSentValue / 1024 / 1024 / 1024).ToString() + " GB";
            }
            else if (netSentValue >= (1024 * 1024))
            {
                netSentRes = (netSentValue / 1024 / 1024).ToString() + " MB";
            }
            else if (netSentValue >= 1024)
            {
                netSentRes = (netSentValue / 1024).ToString() + " KB";
            }
            else
            {
                netSentRes = netSentValue + " B";
            }
            return netSentRes + "/s ↑";
        }

        public static async Task<string> GetWindowsNetReceivedAsync(CMSModel cmsModel)
        {
            string netReceivedCMD = "powershell -Command \"(Get-Counter '\\Network Interface(*)\\Bytes Received/sec').CounterSamples.CookedValue | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum\"";
            string netReceivedRes = await SendSSHCommandAsync(netReceivedCMD, cmsModel);
            // 获取命令输出的值并转换为整数
            int netReceivedValue = int.Parse(netReceivedRes.Split('.')[0]);
            if (netReceivedValue >= 1024 * 1024 * 1024)
            {
                netReceivedRes = (netReceivedValue / 1024 / 1024 / 1024).ToString() + " GB";
            }
            else if (netReceivedValue >= 1024 * 1024)
            {
                netReceivedRes = (netReceivedValue / 1024 / 1024).ToString() + " MB";
            }
            else if (netReceivedValue >= 1024)
            {
                netReceivedRes = (netReceivedValue / 1024).ToString() + " KB";
            }
            else
            {
                netReceivedRes = netReceivedValue + " B";
            }
            return netReceivedRes + "/s ↓";
        }
    }
}
