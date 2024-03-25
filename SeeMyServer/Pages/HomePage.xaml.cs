using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SeeMyServer.Datas;
using SeeMyServer.Models;
using SeeMyServer.Pages.Dialogs;
using SeeMyServer.Methods;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Dispatching;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using System.Numerics;
using System.Diagnostics;

namespace SeeMyServer.Pages
{
    public sealed partial class HomePage : Page
    {
        ResourceLoader resourceLoader = new ResourceLoader();
        private DispatcherQueue _dispatcherQueue;
        private DispatcherTimer timer;

        public HomePage()
        {
            this.InitializeComponent();

            // 获取UI线程的DispatcherQueue
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            // 页面初始化后，加载数据
            LoadData();
            LoadString();

        }
        private void LoadString()
        {
            // 在子线程中执行任务
            Thread subThread = new Thread(new ThreadStart(() =>
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    ConfirmDelete.Content = resourceLoader.GetString("Confirm");

                    CancelDelete.Content = resourceLoader.GetString("Cancel");
                });
            }));
            subThread.Start();
        }

        private List<CMSModel> dataList;

        private void LoadData()
        {
            // 实例化SQLiteHelper
            SQLiteHelper dbHelper = new SQLiteHelper();

            // 查询数据
            dataList = dbHelper.QueryData();

            // 将数据列表绑定到ListView
            dataListView.ItemsSource = dataList;

            // 初始化占用
            foreach (CMSModel cmsModel in dataList)
            {
                cmsModel.CPUUsage = "0%";
                cmsModel.MEMUsage = "0%";
                cmsModel.NETSent = "0 B/s ↑";
                cmsModel.NETReceived = "0 B/s ↓";
            }

            // 创建并配置DispatcherTimer
            timer = new DispatcherTimer();
            timer.Tick += Timer_Tick;

            // 每隔段时间触发一次
            timer.Interval = TimeSpan.FromSeconds(5);

            // 先执行一次事件处理方法
            Timer_Tick(null, null);

            // 启动计时器
            timer.Start();
        }

        // Linux 信息更新
        private async Task UpdateLinuxCMSModelAsync(CMSModel cmsModel)
        {
            // 定义异步任务
            Task<string> cpuTask = GetLinuxCPUUsageAsync(cmsModel);
            Task<string> memTask = GetLinuxMemoryUsageAsync(cmsModel);
            Task<string> netSentTask = GetLinuxNetSentAsync(cmsModel);
            Task<string> netReceivedTask = GetLinuxNetReceivedAsync(cmsModel);

            // 同时执行异步任务
            await Task.WhenAll(cpuTask, memTask, netSentTask, netReceivedTask);

            // 处理获取到的数据
            cmsModel.CPUUsage = cpuTask.Result;
            cmsModel.MEMUsage = memTask.Result;
            cmsModel.NETSent = netSentTask.Result;
            cmsModel.NETReceived = netReceivedTask.Result;
        }
        private async Task<string> GetLinuxCPUUsageAsync(CMSModel cmsModel)
        {
            string cpuUsageCMD = "top -bn1 | grep '^%Cpu' | sed 's/^.*://; s/,.*//; s/ *//g'";
            string cpuUsageRes = await SendSSHCommandAsync(cpuUsageCMD, cmsModel);
            int cpuUsageResValue = int.Parse(cpuUsageRes.Split('.')[0]);
            return cpuUsageResValue.ToString() + "%";
        }
        private async Task<string> GetLinuxMemoryUsageAsync(CMSModel cmsModel)
        {
            string memUsageCMD = "free -m | awk 'NR==2{printf \"%.1f\", $3/$2*100}'";
            string memUsageRes = await SendSSHCommandAsync(memUsageCMD, cmsModel);
            int memUsageResValue = int.Parse(memUsageRes.Split('.')[0]);
            return memUsageResValue.ToString() + "%";
        }
        private async Task<string> GetLinuxNetSentAsync(CMSModel cmsModel)
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
        private async Task<string> GetLinuxNetReceivedAsync(CMSModel cmsModel)
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

        // OpenWRT 信息更新
        private async Task UpdateOpenWRTCMSModelAsync(CMSModel cmsModel)
        {
            // 定义异步任务
            Task<string> cpuTask = GetOpenWRTCPUUsageAsync(cmsModel);
            Task<string> memTask = GetOpenWRTMemoryUsageAsync(cmsModel);
            // OpenWRT也可以用ifconfig查询网速
            Task<string> netSentTask = GetLinuxNetSentAsync(cmsModel);
            Task<string> netReceivedTask = GetLinuxNetReceivedAsync(cmsModel);

            // 同时执行异步任务
            await Task.WhenAll(cpuTask, memTask, netSentTask, netReceivedTask);

            // 处理获取到的数据
            cmsModel.CPUUsage = cpuTask.Result;
            cmsModel.MEMUsage = memTask.Result;
            cmsModel.NETSent = netSentTask.Result;
            cmsModel.NETReceived = netReceivedTask.Result;
        }
        private async Task<string> GetOpenWRTCPUUsageAsync(CMSModel cmsModel)
        {
            string cpuUsageCMD = "top -bn1 | head -n 3 | grep -o 'CPU:.*' | awk '{print $2}'";
            string cpuUsageRes = await SendSSHCommandAsync(cpuUsageCMD, cmsModel);
            return cpuUsageRes.TrimEnd();
        }

        private async Task<string> GetOpenWRTMemoryUsageAsync(CMSModel cmsModel)
        {
            string memUsageCMD = "top -bn1 | head -n 1 | awk '{used=$2; total=$2+$4; printf \"%.0f\", (used/total)*100}'";
            string memUsageRes = await SendSSHCommandAsync(memUsageCMD, cmsModel);
            return memUsageRes + "%";
        }

        // Windows 信息更新
        private async Task UpdateWindowsCMSModelAsync(CMSModel cmsModel)
        {
            // 定义异步任务
            Task<string> cpuTask = GetWindowsCPUUsageAsync(cmsModel);
            Task<string> memTask = GetWindowsMemoryUsageAsync(cmsModel);
            Task<string> netSentTask = GetWindowsNetSentAsync(cmsModel);
            Task<string> netReceivedTask = GetWindowsNetReceivedAsync(cmsModel);

            // 同时执行异步任务
            await Task.WhenAll(cpuTask, memTask, netSentTask, netReceivedTask);

            // 处理获取到的数据
            cmsModel.CPUUsage = cpuTask.Result;
            cmsModel.MEMUsage = memTask.Result;
            cmsModel.NETSent = netSentTask.Result;
            cmsModel.NETReceived = netReceivedTask.Result;
        }
        private async Task<string> GetWindowsCPUUsageAsync(CMSModel cmsModel)
        {
            string cpuUsageCMD = "powershell -Command \"(Get-Counter '\\Processor Information(_Total)\\% Processor Utility').CounterSamples.CookedValue\"";
            string cpuUsageRes = await SendSSHCommandAsync(cpuUsageCMD, cmsModel);
            int cpuUsageResValue = int.Parse(cpuUsageRes.Split('.')[0]);
            return Math.Min(Math.Max(cpuUsageResValue, 0), 100).ToString() + "%";
        }

        private async Task<string> GetWindowsMemoryUsageAsync(CMSModel cmsModel)
        {
            string memUsageCMD = "powershell -Command \"((($totalMemory = (Get-WmiObject -Class Win32_OperatingSystem).TotalVisibleMemorySize) - (Get-WmiObject -Class Win32_OperatingSystem).FreePhysicalMemory) / $totalMemory * 100)\"";
            string memUsageRes = await SendSSHCommandAsync(memUsageCMD, cmsModel);
            int memUsageResValue = int.Parse(memUsageRes.Split('.')[0]);
            return Math.Min(Math.Max(memUsageResValue, 0), 100).ToString() + "%";
        }
        private async Task<string> GetWindowsNetSentAsync(CMSModel cmsModel)
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

        private async Task<string> GetWindowsNetReceivedAsync(CMSModel cmsModel)
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

        private async Task<string> SendSSHCommandAsync(string command, CMSModel cmsModel)
        {
            return await Task.Run(() =>
            {
                return Method.SendSSHCommand(command, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
            });
        }

        private async void Timer_Tick(object sender, object e)
        {
            List<Task> tasks = new List<Task>();

            foreach (CMSModel cmsModel in dataList)
            {
                Task updateTask = cmsModel.OSType switch
                {
                    "Linux" => UpdateLinuxCMSModelAsync(cmsModel),
                    "OpenWRT" => UpdateOpenWRTCMSModelAsync(cmsModel),
                    "Windows" => UpdateWindowsCMSModelAsync(cmsModel),
                    _ => Task.CompletedTask
                };

                tasks.Add(updateTask);
            }

            await Task.WhenAll(tasks);
        }

        // 添加/修改配置按钮点击
        private async void AddConfigButton_Click(object sender, RoutedEventArgs e)
        {
            // 创建一个初始的CMSModel对象
            CMSModel initialCMSModelData = new CMSModel();

            // 创建一个新的dialog对象
            AddServer dialog = new AddServer(initialCMSModelData);
            // 对此dialog对象进行配置
            dialog.XamlRoot = this.XamlRoot;
            dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            dialog.PrimaryButtonText = resourceLoader.GetString("DialogAdd");
            dialog.CloseButtonText = resourceLoader.GetString("DialogClose");
            // 默认按钮为PrimaryButton
            dialog.DefaultButton = ContentDialogButton.Primary;

            // 显示Dialog并等待其关闭
            ContentDialogResult result = await dialog.ShowAsync();

            // 如果按下了Primary
            if (result == ContentDialogResult.Primary)
            {
                // 实例化SQLiteHelper
                SQLiteHelper dbHelper = new SQLiteHelper();
                // 插入新数据
                dbHelper.InsertData(initialCMSModelData);
                // 加载数据
                LoadData();
            }
        }
        // 导入配置按钮点击
        private async void ImportConfig_Click(object sender, RoutedEventArgs e)
        {
            HomePageImportConfig.IsEnabled = false;
            // 实例化SQLiteHelper
            SQLiteHelper dbHelper = new SQLiteHelper();
            // 获取导入的数据
            CMSModel cmsModel = await Method.ImportConfig();
            if (cmsModel != null)
            {
                // 插入新数据
                dbHelper.InsertData(cmsModel);
                // 重新加载数据
                LoadData();
            }
            HomePageImportConfig.IsEnabled = true;
        }
        private async void EditThisConfig(CMSModel cmsModel)
        {
            // 创建一个新的dialog对象
            AddServer dialog = new AddServer(cmsModel);
            // 对此dialog对象进行配置
            dialog.XamlRoot = this.XamlRoot;
            dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            dialog.PrimaryButtonText = resourceLoader.GetString("DialogChange");
            dialog.CloseButtonText = resourceLoader.GetString("DialogClose");
            // 默认按钮为PrimaryButton
            dialog.DefaultButton = ContentDialogButton.Primary;

            // 显示Dialog并等待其关闭
            ContentDialogResult result = await dialog.ShowAsync();

            // 如果按下了Primary
            if (result == ContentDialogResult.Primary)
            {
                // 实例化SQLiteHelper
                SQLiteHelper dbHelper = new SQLiteHelper();
                // 更新数据
                dbHelper.UpdateData(cmsModel);
                // 重新加载数据
                LoadData();
            }
        }
        private void ConfirmDelete_Click(object sender, RoutedEventArgs e)
        {
            // 关闭二次确认Flyout
            confirmationDelFlyout.Hide();
            // 获取NSModel对象
            CMSModel selectedModel = (CMSModel)dataListView.SelectedItem;
            // 实例化SQLiteHelper
            SQLiteHelper dbHelper = new SQLiteHelper();
            // 删除数据
            dbHelper.DeleteData(selectedModel);
            // 重新加载数据
            LoadData();
        }
        private void CancelDelete_Click(object sender, RoutedEventArgs e)
        {
            // 关闭二次确认Flyout
            confirmationDelFlyout.Hide();
        }
        private async void ExportConfigFunction(CMSModel cmsModel)
        {
            string result = await Method.ExportConfig(cmsModel);
        }
        private void OnListViewDoubleTapped(object sender, RoutedEventArgs e)
        { }
        private void OnListViewRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            // 获取右键点击的ListViewItem
            FrameworkElement listViewItem = (sender as FrameworkElement);

            // 获取右键点击的数据对象（NSModel）
            CMSModel selectedItem = listViewItem?.DataContext as CMSModel;

            if (selectedItem != null)
            {

                // 将右键点击的项设置为选中项
                dataListView.SelectedItem = selectedItem;
                // 创建ContextMenu
                MenuFlyout menuFlyout = new MenuFlyout();

                MenuFlyoutItem editMenuItem = new MenuFlyoutItem
                {
                    Text = resourceLoader.GetString("editMenuItemText")
                };
                editMenuItem.Click += (sender, e) =>
                {
                    EditThisConfig(selectedItem);
                };
                menuFlyout.Items.Add(editMenuItem);

                MenuFlyoutItem deleteMenuItem = new MenuFlyoutItem
                {
                    Text = resourceLoader.GetString("deleteMenuItemText")
                };
                deleteMenuItem.Click += (sender, e) =>
                {
                    // 弹出二次确认Flyout
                    confirmationDelFlyout.ShowAt(listViewItem);
                };
                menuFlyout.Items.Add(deleteMenuItem);

                // 添加分割线
                MenuFlyoutSeparator separator = new MenuFlyoutSeparator();
                menuFlyout.Items.Add(separator);

                MenuFlyoutItem exportMenuItem = new MenuFlyoutItem
                {
                    Text = resourceLoader.GetString("exportMenuItemText")
                };
                exportMenuItem.Click += (sender, e) =>
                {
                    ExportConfigFunction(selectedItem);
                };
                menuFlyout.Items.Add(exportMenuItem);

                Thread.Sleep(10);

                // 在指定位置显示ContextMenu
                menuFlyout.ShowAt(listViewItem, e.GetPosition(listViewItem));
            }
        }
    }
}
