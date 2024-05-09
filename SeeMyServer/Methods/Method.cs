using Microsoft.UI.Xaml.Shapes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
using Windows.Networking;
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
        public static string[] SendSSHCommands(string[] sshCommands, string sshHost, string sshPort, string sshUser, string sshPasswd, string sshKey, string privateKeyIsOpen)
        {
            try
            {
                int port;
                if (!int.TryParse(sshPort, out port))
                {
                    logger.LogError($"{sshHost}:{sshPort} 使用了无效的 SSH 端口号：{sshPort}");
                    return new string[0]; // 返回空数组表示参数错误
                }

                bool usePrivateKey = string.Equals(privateKeyIsOpen, "True", StringComparison.OrdinalIgnoreCase);
                using (SshClient sshClient = InitializeSshClient(sshHost, port, sshUser, sshPasswd, sshKey, usePrivateKey))
                {
                    if (sshClient == null)
                    {
                        logger.LogError($"{sshHost}:{sshPort} SSH 客户端初始化失败。");
                        return new string[0]; // 返回空数组表示SSH客户端初始化失败
                    }

                    return ExecuteSshCommands(sshClient, sshCommands);
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"{sshHost}:{sshPort} SSH 操作失败：" + ex.Message);
                return new string[0]; // 返回空数组表示SSH操作失败
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

        private static string[] ExecuteSshCommands(SshClient sshClient, string[] sshCommands)
        {
            List<string> results = new List<string>();

            try
            {
                sshClient.Connect();
                if (sshClient.IsConnected)
                {
                    foreach (var sshCommand in sshCommands)
                    {
                        SshCommand SSHCommand = sshClient.RunCommand(sshCommand);
                        if (!string.IsNullOrEmpty(SSHCommand.Error))
                        {
                            results.Add($"Error executing command \"{sshCommand}\": {SSHCommand.Error}");
                        }
                        else
                        {
                            results.Add(SSHCommand.Result);
                        }
                    }
                }
                else
                {
                    results.Add("SSH connection failed.");
                }
            }
            finally
            {
                sshClient.Disconnect();
            }

            return results.ToArray();
        }

        private static async Task<string[]> SendSSHCommandAsync(string[] commands, CMSModel cmsModel)
        {
            string passwd = "";
            if (cmsModel.SSHKeyIsOpen != "True")
            {
                if (cmsModel.SSHPasswd != "" && cmsModel.SSHPasswd != null)
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
            }
            return await Task.Run(() =>
            {
                return SendSSHCommands(commands, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, passwd, cmsModel.SSHKey, cmsModel.SSHKeyIsOpen);
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
                //string jsonData = JsonConvert.SerializeObject(cmsModel);
                var jsonData = JsonConvert.SerializeObject(new
                {
                    cmsModel.Name,
                    cmsModel.HostIP,
                    cmsModel.HostPort,
                    cmsModel.SSHUser,
                    cmsModel.SSHKey,
                    cmsModel.OSType,
                    cmsModel.SSHKeyIsOpen,
                    cmsModel.CPUUsage,
                    cmsModel.MEMUsage,
                    cmsModel.NETSent,
                    cmsModel.NETReceived,
                });

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

        // 获取Linux系统信息
        public static async Task<Tuple<
            List<List<string>>,
            List<string>,
            List<NetworkInterfaceInfo>,
            List<List<MountInfo>>,
            List<string>,
            List<string>
            >> GetLinuxCPUUsageAsync(CMSModel cmsModel)
        {
            // 创建 Stopwatch 实例
            Stopwatch stopwatch = new Stopwatch();

            // OpenWRT不能用hostname，可以用"uci get system.@system[0].hostname"
            // 用户应自己设置命令别名以兼容
            // P - 防止换行
            // 2>&1 - 将标准错误输出重定向到标准输出，这样可以在管道中处理错误消息。
            // 命令列表
            string[] CPUUsageCMD = new string[]
            {
            "cat /proc/stat | grep cpu 2>&1",
            "cat /proc/meminfo | grep -E 'Mem|Swap' 2>&1",
            "cat /proc/net/dev 2>&1",
            "df -hP 2>&1",
            "uptime | awk '{print $3 \" \" $4}' 2>&1",
            "hostname 2>&1",
            "top -bn1 2>&1",
            "cat /proc/cpuinfo | grep processor | wc -l",
            "cat /etc/*-release 2>&1 | grep PRETTY_NAME",
            "cat /proc/diskstats 2>&1"
            };
            string[] result = await SendSSHCommandAsync(CPUUsageCMD, cmsModel);

            // 开始计时
            stopwatch.Start();

            if (result != null)
            {

                // 为了加快第一次更新的速度，代价是第一次的结果误差极大
                if (cmsModel.CPUUsage != "0%")
                {
                    await Task.Delay(1000);
                }

                string[] result2 = await SendSSHCommandAsync(CPUUsageCMD, cmsModel);
                // 停止计时
                stopwatch.Stop();

                // 用于保存结果的List
                List<List<string>> cpuUsageList = new List<List<string>>();
                List<string> parsedResults = new List<string>();
                List<NetworkInterfaceInfo> networkInterfaceInfos = new List<NetworkInterfaceInfo>();
                List<List<MountInfo>> mountInfos = new List<List<MountInfo>>();
                List<string> loadResults = new List<string>();
                List<string> aboutInfo = new List<string>();

                if (result.Length >= 10 && result2.Length >= 10)
                {
                    string CPUUsagesRev = result[0];
                    string MEMUsagesRev = result[1];
                    string NETUsagesRev = result[2];
                    string DFUsagesRev = result[3];
                    string UptimeRev = result[4];
                    string HostnameRev = result[5];
                    string TopRev = result[6];
                    string CoreNumRev = result[7];
                    string OSReleaseRev = result[8];
                    string DiskStatsRev = result[9];

                    string CPUUsagesRev2 = result2[0];
                    string MEMUsagesRev2 = result2[1];
                    string NETUsagesRev2 = result2[2];
                    string DFUsagesRev2 = result2[3];
                    string UptimeRev2 = result2[4];
                    string HostnameRev2 = result2[5];
                    string TopRev2 = result2[6];
                    string CoreNumRev2 = result2[7];
                    string OSReleaseRev2 = result2[8];
                    string DiskStatsRev2 = result2[9];

                    // 第一部分是CPU占用，结果格式如下：
                    // cpu  697687 0 1332141 93898629 1722210 0 840664 0 0 0
                    // cpu0 171727 0 309858 23571901 565476 0 3820 0 0 0
                    // cpu1 163341 0 297540 23583515 578130 0 277 0 0 0
                    // cpu2 155832 0 299665 23203048 129886 0 834464 0 0 0
                    // cpu3 206787 0 425078 23540165 448718 0 2103 0 0 0
                    //
                    // CPU 用户态 用户态低优先级 系统态 空闲 I/O等待 无意义 硬件中断 软件中断 steal_time guest_nice进程

                    {
                        // 解析结果
                        // 以换行符为准，按行分割结果
                        string[] lines = CPUUsagesRev.Split('\n');
                        List<List<string>> cpuUsageList0s = new List<List<string>>();
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

                                cpuUsage.Add(fields[1]);    //0 用户态
                                cpuUsage.Add(fields[2]);    //1 用户态低优先级
                                cpuUsage.Add(fields[3]);    //2 系统态
                                cpuUsage.Add(fields[4]);    //3 空闲
                                cpuUsage.Add(fields[5]);    //4 I/O等待
                                cpuUsage.Add(fields[6]);    //5 无意义
                                cpuUsage.Add(fields[7]);    //6 硬件中断
                                cpuUsage.Add(fields[8]);    //7 软件中断
                                cpuUsage.Add(fields[9]);    //8 steal_time
                                cpuUsage.Add(fields[10]);   //9 guest_nice进程
                                cpuUsage.Add($"{totalCpuTime}"); //10 总时间

                                cpuUsageList0s.Add(cpuUsage);
                            }
                        }

                        // 以换行符为准，按行分割结果
                        string[] lines2 = CPUUsagesRev2.Split('\n');
                        List<List<int>> cpuUsageListAbs = new List<List<int>>();
                        // 遍历每行
                        int index = 0;
                        foreach (string line in lines2)
                        {
                            // 检查是否以 cpu 开头
                            if (line.StartsWith("cpu"))
                            {
                                // 以空格分割，并去除空白项
                                string[] fields = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                                // 保存当前 CPU 的使用情况
                                List<int> cpuUsage = new List<int>();

                                // 计算CPU总事件（单行之和）
                                long totalCpuTime = 0;
                                for (int i = 1; i < fields.Length; i++)
                                {
                                    if (long.TryParse(fields[i], out long cpuTime))
                                    {
                                        totalCpuTime += cpuTime;
                                    }
                                }

                                // 计算差值
                                cpuUsage.Add(Math.Abs(int.Parse(cpuUsageList0s[index][0]) - int.Parse(fields[1])));    //0 用户态
                                cpuUsage.Add(Math.Abs(int.Parse(cpuUsageList0s[index][1]) - int.Parse(fields[2])));    //1 用户态低优先级
                                cpuUsage.Add(Math.Abs(int.Parse(cpuUsageList0s[index][2]) - int.Parse(fields[3])));    //2 系统态
                                cpuUsage.Add(Math.Abs(int.Parse(cpuUsageList0s[index][3]) - int.Parse(fields[4])));    //3 空闲
                                cpuUsage.Add(Math.Abs(int.Parse(cpuUsageList0s[index][4]) - int.Parse(fields[5])));    //4 I/O等待
                                cpuUsage.Add(Math.Abs(int.Parse(cpuUsageList0s[index][5]) - int.Parse(fields[6])));    //5 无意义
                                cpuUsage.Add(Math.Abs(int.Parse(cpuUsageList0s[index][6]) - int.Parse(fields[7])));    //6 硬件中断
                                cpuUsage.Add(Math.Abs(int.Parse(cpuUsageList0s[index][7]) - int.Parse(fields[8])));    //7 软件中断
                                cpuUsage.Add(Math.Abs(int.Parse(cpuUsageList0s[index][8]) - int.Parse(fields[9])));    //8 steal_time
                                cpuUsage.Add(Math.Abs(int.Parse(cpuUsageList0s[index][9]) - int.Parse(fields[10])));   //9 guest_nice进程
                                cpuUsage.Add(Math.Abs(int.Parse(cpuUsageList0s[index][10]) - (int)totalCpuTime));      //10 总时间

                                cpuUsageListAbs.Add(cpuUsage);

                                index++;
                            }
                        }

                        // 计算占用率
                        foreach (List<int> cpuUsageAbs in cpuUsageListAbs)
                        {
                            // 保存当前 CPU 的使用情况
                            List<string> cpuUsage = new List<string>();
                            // 占用率计算
                            cpuUsage.Add($"{100 - ((double)cpuUsageAbs[3] / (double)cpuUsageAbs[10] * 100):F0}");    //0 CPU占用率
                            cpuUsage.Add($"{((double)cpuUsageAbs[0]) / (double)cpuUsageAbs[10] * 100:F2}");    //0 CPUUser占用率
                            cpuUsage.Add($"{((double)cpuUsageAbs[2]) / (double)cpuUsageAbs[10] * 100:F2}");    //0 CPUSys占用率
                            cpuUsage.Add($"{((double)cpuUsageAbs[3]) / (double)cpuUsageAbs[10] * 100:F2}");    //0 CPUIdle占用率
                            cpuUsage.Add($"{((double)cpuUsageAbs[4]) / (double)cpuUsageAbs[10] * 100:F2}");    //0 CPUIO占用率

                            cpuUsageList.Add(cpuUsage);
                        }
                    }

                    // 第二部分是内存和swap占用，结果格式如下：
                    // MemTotal:        3902716 kB
                    // MemFree:          151924 kB
                    // MemAvailable:    2799072 kB
                    // SwapCached:        66680 kB
                    // SwapTotal:       4439980 kB
                    // SwapFree:        3741944 kB

                    {

                        // 定义正则表达式模式
                        Regex pattern = new Regex(@"(\w+):\s+(\d+)\s+(\w+)");

                        // 使用正则表达式进行匹配
                        MatchCollection matches = pattern.Matches(MEMUsagesRev);

                        // 遍历匹配结果
                        foreach (Match match in matches)
                        {
                            // 检查匹配是否成功
                            if (match.Success)
                            {
                                string matchResult = $"{match.Groups[2].Value}";
                                parsedResults.Add(matchResult);
                            }
                            else
                            {
                                logger.LogError("MEMUsagesRev pattern match failed.");
                            }
                        }
                    }

                    // 网卡信息
                    {
                        networkInterfaceInfos = NetworkInterfaceInfoParse(NETUsagesRev, NETUsagesRev2, stopwatch.ElapsedMilliseconds);
                    }

                    // 挂载信息
                    {

                        mountInfos = MountInfoParse(DFUsagesRev, DiskStatsRev, DiskStatsRev2, stopwatch.ElapsedMilliseconds);
                    }

                    // 启动时长、主机名、CPU核心数量、系统版本、top
                    {
                        // 单独处理OpenWRT的情况
                        if (HostnameRev.Split('\n')[0] == "ash: hostname: not found")
                        {
                            logger.LogError("HostName acquisition failed, try to use 'uci get system.@system[0].hostname'.");
                            string[] CMD = new string[]
                            {
                            "uci get system.@system[0].hostname"
                            };
                            HostnameRev = (await SendSSHCommandAsync(CMD, cmsModel)).FirstOrDefault();
                        }

                        // 启动时长
                        aboutInfo.Add(UptimeRev.Split(',')[0]);
                        // 主机名
                        aboutInfo.Add(HostnameRev.Split('\n')[0]);
                        aboutInfo.Add(CoreNumRev);   // 核心数量
                        if (OSReleaseRev != "" && OSReleaseRev != null)
                        {
                            aboutInfo.Add(OSReleaseRev.Split('\"')[1]); // 系统版本
                        }
                        else
                        {
                            aboutInfo.Add(""); // 系统版本
                            logger.LogError("OSRelease acquisition failed.");
                        }

                        aboutInfo.Add(TopRev); // top
                    }

                    // 负载信息
                    // 不同主机的top格式可能不同，大多数Linux发行版可能相同，OpenWRT一般不同，这里注意特殊处理。
                    {
                        double average1 = .0;
                        double average5 = .0;
                        double average15 = .0;
                        double average1Percentage = .0;
                        double average5Percentage = .0;
                        double average15Percentage = .0;
                        string CPUCoreRes = CoreNumRev;

                        // OpenWRT单独适配
                        if (TopRev.StartsWith("Mem"))
                        {
                            logger.LogInfo("Load average (OpenWRT).");
                            // 使用正则取出负载信息
                            Regex loadRegex = new Regex(@"Load average: (\d+\.\d+) (\d+\.\d+) (\d+\.\d+) (\d+)/(\d+) (\d+)");

                            Match loadMatch = loadRegex.Match(TopRev);
                            try
                            {
                                // 获取1分钟内负载
                                average1 = double.Parse(loadMatch.Groups[1].Value);
                                // 获取5分钟内负载
                                average5 = double.Parse(loadMatch.Groups[2].Value);
                                // 获取15分钟内负载
                                average15 = double.Parse(loadMatch.Groups[3].Value);

                                // 计算负载
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
                        }
                        // 负载信息
                        else
                        {
                            // 使用正则取出负载信息
                            Regex loadRegex = new Regex(@"load average: (\d+\.\d+), (\d+\.\d+), (\d+\.\d+)");

                            Match loadMatch = loadRegex.Match(TopRev);

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
                        }
                        // 将结果添加到 List<string> 中
                        loadResults.Add($"{average1:F2}");
                        loadResults.Add($"{average5:F2}");
                        loadResults.Add($"{average15:F2}");
                        loadResults.Add($"{average1Percentage:F2}");
                        loadResults.Add($"{average5Percentage:F2}");
                        loadResults.Add($"{average15Percentage:F2}");
                    }
                }
                else
                {
                    logger.LogError("The number of elements in the SSH result array is incorrect.");
                    logger.LogError(string.Join("\n\n", result));
                }

                return Tuple.Create(cpuUsageList, parsedResults, networkInterfaceInfos, mountInfos, aboutInfo, loadResults);
            }
            else
            {
                // 停止计时
                stopwatch.Stop();
                return null;
            }
        }

        public static string NetUnitConversion(decimal netValue)
        {
            decimal result;
            string unit;

            switch (netValue)
            {
                case decimal n when n >= 1000000000000:
                    result = netValue / 1024 / 1024 / 1024 / 1024;
                    unit = "TB";
                    break;
                case decimal n when n >= 1000000000:
                    result = netValue / 1024 / 1024 / 1024;
                    unit = "GB";
                    break;
                case decimal n when n >= 1000000:
                    result = netValue / 1024 / 1024;
                    unit = "MB";
                    break;
                case decimal n when n >= 1000:
                    result = netValue / 1024;
                    unit = "KB";
                    break;
                default:
                    result = netValue;
                    unit = "B";
                    break;
            }

            return result.ToString("F2") + " " + unit;
        }
        public static decimal ReverseNetUnitConversion(string convertedValue)
        {
            string[] parts = convertedValue.Split(' ');
            decimal value = decimal.Parse(parts[0]);
            string unit = parts[1];

            switch (unit)
            {
                case "TB":
                    return value * 1024m * 1024m * 1024m * 1024m;
                case "GB":
                    return value * 1024m * 1024m * 1024m;
                case "MB":
                    return value * 1024m * 1024m;
                case "KB":
                    return value * 1024m;
                default:
                    return value;
            }
        }
        // 处理 df
        public static List<List<MountInfo>> MountInfoParse(string input, string diskStatus1, string diskStatus2, decimal elapsedTime)
        {
            var mountInfos = new List<MountInfo>();
            var result = new List<List<MountInfo>>();

            // 按行分割输入
            var lines = input.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            List<MountInfo> status = ParseDiskSpeed(diskStatus1, diskStatus2, elapsedTime);

            // 跳过标题行
            foreach (var line in lines.Skip(1))
            {
                // 检查是否以 "/" 开头
                // 检查是否包含 ":" （兼容WSL）
                if (line.StartsWith("/") || line.Substring(1).StartsWith(":"))
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

                        // Find corresponding status for the current FileSystem
                        var correspondingStatus = status.FirstOrDefault(s => s.FileSystem == mountInfo.FileSystem);
                        if (correspondingStatus != null)
                        {
                            mountInfo.SectorsRead = correspondingStatus.SectorsRead;
                            mountInfo.SectorsWritten = correspondingStatus.SectorsWritten;
                            mountInfo.SectorsReadBytes = $"{NetUnitConversion(correspondingStatus.SectorsRead)}";
                            mountInfo.SectorsWrittenBytes = $"{NetUnitConversion(correspondingStatus.SectorsWritten)}";
                            mountInfo.SectorsReadPerSecondOrigin = correspondingStatus.SectorsReadPerSecondOrigin;
                            mountInfo.SectorsWrittenPerSecondOrigin = correspondingStatus.SectorsWrittenPerSecondOrigin;
                            mountInfo.SectorsReadPerSecond = correspondingStatus.SectorsReadPerSecond;
                            mountInfo.SectorsWrittenPerSecond = correspondingStatus.SectorsWrittenPerSecond;
                        }
                        else
                        {
                            // Log an error if corresponding status not found
                            //logger.LogError($"Corresponding status not found for FileSystem: {mountInfo.FileSystem}");
                        }

                        mountInfos.Add(mountInfo);
                    }
                    else
                    {
                        //logger.LogError($"Invalid line format: {line}");
                    }
                }
            }
            result.Add(mountInfos);
            result.Add(status);
            return result;
        }

        private static List<MountInfo> ParseSingleDiskStatus(string input)
        {
            var lines = input.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var diskStatusInfos = new List<MountInfo>();

            foreach (var line in lines)
            {
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                var info = new MountInfo
                {
                    FileSystem = $"/dev/{parts[2]}",
                    SectorsRead = long.Parse(parts[5]),
                    SectorsWritten = long.Parse(parts[9]),
                };

                diskStatusInfos.Add(info);
            }

            //throw new Exception($"{interfaceInfos[0].Interface}");
            return diskStatusInfos;
        }
        private static List<MountInfo> ParseDiskSpeed(string input1, string input2, decimal elapsedTime)
        {
            var diskStatus1 = ParseSingleDiskStatus(input1);
            var diskStatus2 = ParseSingleDiskStatus(input2);
            var diskStatusInfos = new List<MountInfo>();

            foreach (var dstatus1 in diskStatus1)
            {
                var dstatus2 = diskStatus2.FirstOrDefault(x => x.FileSystem == dstatus1.FileSystem);
                if (dstatus2 != null)
                {
                    decimal sectorsReadSpeed = (dstatus2.SectorsRead - dstatus1.SectorsRead) * 1000 / elapsedTime;
                    decimal sectorsWrittenSpeed = (dstatus2.SectorsWritten - dstatus1.SectorsWritten) * 1000 / elapsedTime;

                    diskStatusInfos.Add(new MountInfo
                    {
                        FileSystem = dstatus2.FileSystem,
                        SectorsRead = dstatus2.SectorsRead,
                        SectorsWritten = dstatus2.SectorsWritten,
                        SectorsReadPerSecondOrigin = sectorsReadSpeed,
                        SectorsWrittenPerSecondOrigin = sectorsWrittenSpeed,
                        SectorsReadPerSecond = $"{NetUnitConversion(sectorsReadSpeed)}/s R",
                        SectorsWrittenPerSecond = $"{NetUnitConversion(sectorsWrittenSpeed)}/s W",
                    });
                }
            }

            return diskStatusInfos;
        }



        // 处理 ifconfig
        public static List<NetworkInterfaceInfo> NetworkInterfaceInfoParse(string input, string input2, decimal elapsedTime)
        {
            var interfaces1 = ParseSingleNetDev(input);
            var interfaces2 = ParseSingleNetDev(input2);
            var interfaceInfos = new List<NetworkInterfaceInfo>();

            foreach (var iface1 in interfaces1)
            {
                var iface2 = interfaces2.FirstOrDefault(x => x.Interface == iface1.Interface);
                if (iface2 != null)
                {
                    //throw new Exception($"{elapsedTime}");
                    decimal receiveSpeed = (iface2.ReceiveBytesOrigin - iface1.ReceiveBytesOrigin) * 1000 / elapsedTime;
                    decimal transmitSpeed = (iface2.TransmitBytesOrigin - iface1.TransmitBytesOrigin) * 1000 / elapsedTime;

                    interfaceInfos.Add(new NetworkInterfaceInfo
                    {
                        Interface = iface2.Interface,
                        ReceiveBytes = iface2.ReceiveBytes,
                        ReceivePackets = iface2.ReceivePackets,
                        TransmitBytes = iface2.TransmitBytes,
                        TransmitPackets = iface2.TransmitPackets,
                        ReceiveSpeedByte = receiveSpeed,
                        TransmitSpeedByte = transmitSpeed,
                        ReceiveSpeed = $"{NetUnitConversion(receiveSpeed)}/s ↓",
                        TransmitSpeed = $"{NetUnitConversion(transmitSpeed)}/s ↑"
                    });
                }
            }

            return interfaceInfos;
        }
        private static List<NetworkInterfaceInfo> ParseSingleNetDev(string input)
        {
            var lines = input.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var interfaceInfos = new List<NetworkInterfaceInfo>();

            foreach (var line in lines.Skip(2))
            {
                var parts = line.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    continue;

                var interfaceName = parts[0].Trim();
                var values = parts[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(long.Parse)
                    .ToArray();

                var info = new NetworkInterfaceInfo
                {
                    Interface = interfaceName,
                    ReceiveBytes = $"{NetUnitConversion(values[0])}",
                    ReceiveBytesOrigin = values[0],
                    ReceivePackets = $"{values[1]}",
                    TransmitBytes = $"{NetUnitConversion(values[8])}",
                    TransmitBytesOrigin = values[8],
                    TransmitPackets = $"{values[9]}"
                };

                interfaceInfos.Add(info);
            }

            //throw new Exception($"{interfaceInfos[0].Interface}");
            return interfaceInfos;
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
                process.StartInfo.Arguments = $"-NoExit ssh -i {cmsModel.SSHKey} {cmsModel.SSHUser}@{cmsModel.HostIP} -p {cmsModel.HostPort}";
            }
            else
            {
                process.StartInfo.Arguments = $"-NoExit ssh {cmsModel.SSHUser}@{cmsModel.HostIP} -p {cmsModel.HostPort}";
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
