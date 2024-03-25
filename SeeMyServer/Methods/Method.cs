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
    }
}
