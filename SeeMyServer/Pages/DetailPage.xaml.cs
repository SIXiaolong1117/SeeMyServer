using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SeeMyServer.Datas;
using SeeMyServer.Methods;
using SeeMyServer.Models;
using SeeMyServer.Pages.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Devices.Geolocation;
using Windows.Storage;
using Windows.System;

namespace SeeMyServer.Pages
{
    public sealed partial class DetailPage : Page
    {
        // 启用本地设置数据
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        ResourceLoader resourceLoader = new ResourceLoader();
        private DispatcherTimer timer;

        public DetailPage()
        {
            this.InitializeComponent();

            LoadData();
        }

        public static void CreateProgressBars(Grid container, string[] CPUCoreUsageTokens)
        {
            int numberOfBars = CPUCoreUsageTokens.Length;

            // 清除 Grid 的行定义和子元素
            container.RowDefinitions.Clear();
            container.ColumnDefinitions.Clear();
            container.Children.Clear();

            // 检查是否需要添加列定义
            if (container.ColumnDefinitions.Count == 0)
            {
                // 创建一个ColumnDefinition
                ColumnDefinition columnDefinition = new ColumnDefinition();

                // 设置宽度为自动调整大小以填充剩余空间
                columnDefinition.Width = new GridLength(1, GridUnitType.Star);

                // 将ColumnDefinition添加到Grid的ColumnDefinitions集合中
                container.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
                container.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
                container.ColumnDefinitions.Add(columnDefinition);
            }

            for (int i = 0; i < numberOfBars; i++)
            {
                // 添加新的行定义
                container.RowDefinitions.Add(new RowDefinition());

                ProgressBar progressBar = new ProgressBar();

                progressBar.Margin = new Thickness(0, 4, 0, 4);
                try
                {
                    progressBar.Value = double.Parse(CPUCoreUsageTokens[i]);
                }
                catch (Exception ex) { }

                // 创建 TextBlock 来显示与 ProgressBar 同步的值
                TextBlock textBlock = new TextBlock();
                TextBlock textCPUBlock = new TextBlock();
                textBlock.Text = $"{progressBar.Value.ToString().Split(".")[0]}%";
                textCPUBlock.Text = $"CPU{i}";
                textCPUBlock.Margin = new Thickness(0, 4, 8, 6);
                // 监听 ProgressBar 的值改变事件，更新 TextBlock 的内容
                progressBar.ValueChanged += (sender, e) =>
                {
                    textBlock.Text = $"{progressBar.Value.ToString().Split(".")[0]}%";
                    textCPUBlock.Text = $"CPU{i}";
                };
                textBlock.Margin = new Thickness(8, 4, 0, 6);
                textBlock.Width = 40;
                textBlock.HorizontalAlignment = HorizontalAlignment.Right;

                // 设置位置
                Grid.SetRow(textCPUBlock, i);
                Grid.SetColumn(textCPUBlock, 0);

                Grid.SetRow(textBlock, i);
                Grid.SetColumn(textBlock, 1);

                Grid.SetRow(progressBar, i);
                Grid.SetColumn(progressBar, 2);

                // 添加到 Grid 中
                container.Children.Add(textCPUBlock);
                container.Children.Add(progressBar);
                container.Children.Add(textBlock);
            }
        }

        CMSModel dataList;
        private void LoadData()
        {
            // 实例化SQLiteHelper
            SQLiteHelper dbHelper = new SQLiteHelper();

            // 查询数据
            dataList = dbHelper.GetDataById(Convert.ToInt32(localSettings.Values["ServerID"]));

            // 将数据列表绑定
            dataGrid.DataContext = dataList;

            // 初始化占用
            dataList.CPUUsage = "0%";
            dataList.MEMUsage = "0%";
            dataList.NETSent = "0 B/s ↑";
            dataList.NETReceived = "0 B/s ↓";

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
            string[] usages = await Method.GetLinuxUsageAsync(cmsModel);
            string HostName = await Method.GetLinuxHostName(cmsModel);
            string UpTime = await Method.GetLinuxUpTime(cmsModel);

            // 处理获取到的数据
            cmsModel.CPUUsage = usages[0];
            cmsModel.MEMUsage = usages[1];
            cmsModel.HostName = HostName;
            cmsModel.UpTime = UpTime;
            cmsModel.TotalMEM = $" of {usages[4]} GB";

            //Debug.Text = usages[2];
            //Debug2.Text = usages[3];

            string[] tokens = usages[2].Split(", ");
            CreateProgressBars(progressBarsGrid, tokens);

            // 只有当 ItemsSource 未绑定时才进行绑定
            if (MountInfosListView.ItemsSource == null)
            {
                List<MountInfo> MountInfos = await Method.GetLinuxMountInfo(cmsModel);
                cmsModel.MountInfos = MountInfos;
                MountInfosListView.ItemsSource = cmsModel.MountInfos;
            }
            if (NetworkInfosListView.ItemsSource == null)
            {
                List<NetworkInterfaceInfo> NetworkInterfaceInfos = await Method.GetLinuxNetworkInterfaceInfo(cmsModel);
                cmsModel.NetworkInterfaceInfos = NetworkInterfaceInfos;
                NetworkInfosListView.ItemsSource = cmsModel.NetworkInterfaceInfos;
            }
        }

        // OpenWRT 信息更新
        private async Task UpdateOpenWRTCMSModelAsync(CMSModel cmsModel)
        {
            string[] usages = await Method.GetOpenWRTCPUUsageAsync(cmsModel);
            string HostName = await Method.GetOpenWRTHostName(cmsModel);
            // OpenWRT也可以用部分Linux命令
            string[] netUsages = await Method.GetLinuxNetAsync(cmsModel);
            string UpTime = await Method.GetLinuxUpTime(cmsModel);

            // 处理获取到的数据
            cmsModel.CPUUsage = usages[0];
            cmsModel.MEMUsage = usages[1];
            cmsModel.NETReceived = netUsages[0];
            cmsModel.NETSent = netUsages[1];
            cmsModel.HostName = HostName;
            cmsModel.UpTime = UpTime;

            // OpenWRT的Top无法查看单独核心占用
            string[] tokens = new string[] { usages[0].Split("%")[0] };
            CreateProgressBars(progressBarsGrid, tokens);

            // 只有当 ItemsSource 未绑定时才进行绑定
            if (MountInfosListView.ItemsSource == null)
            {
                List<MountInfo> MountInfos = await Method.GetLinuxMountInfo(cmsModel);
                MountInfosListView.ItemsSource = cmsModel.MountInfos;
                cmsModel.MountInfos = MountInfos;
            }
            if (NetworkInfosListView.ItemsSource == null)
            {
                List<NetworkInterfaceInfo> NetworkInterfaceInfos = await Method.GetLinuxNetworkInterfaceInfo(cmsModel);
                cmsModel.NetworkInterfaceInfos = NetworkInterfaceInfos;
                NetworkInfosListView.ItemsSource = cmsModel.NetworkInterfaceInfos;
            }
        }

        // Windows 信息更新
        private async Task UpdateWindowsCMSModelAsync(CMSModel cmsModel)
        {
            string[] usages = await Method.GetWindowsUsageAsync(cmsModel);
            string upTime = await Method.GetWindowsUpTime(cmsModel);
            Task<string> memTask = Method.GetWindowsMemoryUsageAsync(cmsModel);
            Task<string> netSentTask = Method.GetWindowsNetSentAsync(cmsModel);
            Task<string> netReceivedTask = Method.GetWindowsNetReceivedAsync(cmsModel);
            // Windows上可以使用相同的命令
            string HostName = await Method.GetLinuxHostName(cmsModel);

            // 同时执行异步任务
            await Task.WhenAll(memTask, netSentTask, netReceivedTask);

            // 处理获取到的数据
            cmsModel.CPUUsage = usages[0];
            cmsModel.MEMUsage = memTask.Result;
            cmsModel.NETSent = netSentTask.Result;
            cmsModel.NETReceived = netReceivedTask.Result;
            cmsModel.HostName = HostName.TrimEnd();
            cmsModel.UpTime = upTime;
            cmsModel.TotalMEM = $" of {usages[4]} GB";

            string[] tokens = usages[2].Split(", ");
            CreateProgressBars(progressBarsGrid, tokens);

            // 只有当 ItemsSource 未绑定时才进行
            if (MountInfosListView.ItemsSource == null)
            {
                List<MountInfo> MountInfos = await Method.GetWindowsMountInfo(cmsModel);
                cmsModel.MountInfos = MountInfos;
                MountInfosListView.ItemsSource = cmsModel.MountInfos;
            }
            if (NetworkInfosListView.ItemsSource == null)
            {
                List<NetworkInterfaceInfo> NetworkInterfaceInfos = await Method.GetWindowsNetworkInterfaceInfo(cmsModel);
                cmsModel.NetworkInterfaceInfos = NetworkInterfaceInfos;
                NetworkInfosListView.ItemsSource = cmsModel.NetworkInterfaceInfos;
            }
        }

        private async void Timer_Tick(object sender, object e)
        {
            List<Task> tasks = new List<Task>();

            Task updateTask = dataList.OSType switch
            {
                "Linux" => UpdateLinuxCMSModelAsync(dataList),
                "OpenWRT" => UpdateOpenWRTCMSModelAsync(dataList),
                "Windows" => UpdateWindowsCMSModelAsync(dataList),
                _ => Task.CompletedTask
            };

            tasks.Add(updateTask);

            await Task.WhenAll(tasks);
        }
        private void OpenSSHTerminal_Click(object sender, RoutedEventArgs e)
        {
            SSHTerminal(dataList.SSHKey, dataList.SSHUser, dataList.HostIP, dataList.HostPort);
        }
        private void EditConfig_Click(object sender, RoutedEventArgs e)
        {
            EditThisConfig(dataList);
        }
        public static void SSHTerminal(string KeyPath, string User, string Domain, string Port)
        {
            // 创建一个新的进程
            Process process = new Process();
            // 指定运行PowerShell
            process.StartInfo.FileName = "PowerShell.exe";
            // 命令
            process.StartInfo.Arguments = $"ssh -i {KeyPath} {User}@{Domain} -p {Port}";
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
                // 去掉绑定
                MountInfosListView.ItemsSource = null;
                NetworkInfosListView.ItemsSource = null;
            }
        }
    }
}
