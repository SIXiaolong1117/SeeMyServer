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
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using static PInvoke.User32;
using static System.Net.Mime.MediaTypeNames;

namespace SeeMyServer.Pages
{
    public sealed partial class DetailPage : Page
    {
        // 启用本地设置数据
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        ResourceLoader resourceLoader = new ResourceLoader();
        private DispatcherQueue _dispatcherQueue;
        private DispatcherTimer timer;

        public DetailPage()
        {
            this.InitializeComponent();

            // 获取UI线程的DispatcherQueue
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

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
                container.ColumnDefinitions.Add(columnDefinition);

                // 添加列定义
                container.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            }

            for (int i = 0; i < numberOfBars; i++)
            {
                // 添加新的行定义
                container.RowDefinitions.Add(new RowDefinition());

                ProgressBar progressBar = new ProgressBar();

                progressBar.Margin = new Thickness(0, 5, 0, 5);
                progressBar.Value = double.Parse(CPUCoreUsageTokens[i]);

                // 创建 TextBlock 来显示与 ProgressBar 同步的值
                TextBlock textBlock = new TextBlock();
                textBlock.Text = $"{progressBar.Value.ToString().Split(".")[0]}%";
                // 监听 ProgressBar 的值改变事件，更新 TextBlock 的内容
                progressBar.ValueChanged += (sender, e) =>
                {
                    textBlock.Text = $"{progressBar.Value.ToString().Split(".")[0]}%";
                };
                textBlock.Margin = new Thickness(5, 0, 0, 0);
                textBlock.Width = 30;
                textBlock.HorizontalAlignment = HorizontalAlignment.Right;

                // 设置 ProgressBar 和 TextBlock 的位置
                Grid.SetRow(progressBar, i);
                Grid.SetColumn(progressBar, 0);

                Grid.SetRow(textBlock, i);
                Grid.SetColumn(textBlock, 1);

                // 将 ProgressBar 和 TextBlock 添加到 Grid 中
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
            // 定义异步任务
            string[] usages = await Method.GetLinuxUsageAsync(cmsModel);
            string[] netUsages = await Method.GetLinuxNetAsync(cmsModel);
            string HostName = await Method.GetLinuxHostName(cmsModel);
            string UpTime = await Method.GetLinuxUpTime(cmsModel);
            List<MountInfo> MountInfos = await Method.GetLinuxMountInfo(cmsModel);
            List<NetworkInterfaceInfo> NetworkInterfaceInfos = await Method.GetLinuxNetworkInterfaceInfo(cmsModel);

            // 处理获取到的数据
            cmsModel.CPUUsage = usages[0];
            cmsModel.MEMUsage = usages[1];
            cmsModel.NETReceived = netUsages[0];
            cmsModel.NETSent = netUsages[1];
            cmsModel.HostName = HostName;
            cmsModel.UpTime = UpTime;
            cmsModel.TotalMEM = $" of {usages[4]} GB";
            cmsModel.MountInfos = MountInfos;
            cmsModel.NetworkInterfaceInfos = NetworkInterfaceInfos;

            //Debug.Text = usages[2];
            //Debug2.Text = usages[3];

            string[] tokens = usages[2].Split(", ");
            CreateProgressBars(progressBarsGrid, tokens);

            // 只有当 ItemsSource 未绑定时才进行绑定
            if (MountInfosListView.ItemsSource == null)
            {
                MountInfosListView.ItemsSource = cmsModel.MountInfos;
            }
            if (NetworkInfosListView.ItemsSource == null)
            {
                NetworkInfosListView.ItemsSource = cmsModel.NetworkInterfaceInfos;
            }
        }

        // OpenWRT 信息更新
        private async Task UpdateOpenWRTCMSModelAsync(CMSModel cmsModel)
        {
            // 定义异步任务
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
        }

        // Windows 信息更新
        private async Task UpdateWindowsCMSModelAsync(CMSModel cmsModel)
        {
            // 定义异步任务
            Task<string> cpuTask = Method.GetWindowsCPUUsageAsync(cmsModel);
            Task<string> memTask = Method.GetWindowsMemoryUsageAsync(cmsModel);
            Task<string> netSentTask = Method.GetWindowsNetSentAsync(cmsModel);
            Task<string> netReceivedTask = Method.GetWindowsNetReceivedAsync(cmsModel);

            // 同时执行异步任务
            await Task.WhenAll(cpuTask, memTask, netSentTask, netReceivedTask);

            // 处理获取到的数据
            cmsModel.CPUUsage = cpuTask.Result;
            cmsModel.MEMUsage = memTask.Result;
            cmsModel.NETSent = netSentTask.Result;
            cmsModel.NETReceived = netReceivedTask.Result;
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
    }
}
