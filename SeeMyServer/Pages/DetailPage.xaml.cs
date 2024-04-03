using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SeeMyServer.Datas;
using SeeMyServer.Helper;
using SeeMyServer.Methods;
using SeeMyServer.Models;
using SeeMyServer.Pages.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        private Logger logger;

        public DetailPage()
        {
            this.InitializeComponent();

            // 设置日志，最大1MB
            logger = new Logger(1);

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
                container.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(55) });
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
                textBlock.Text = $"{CPUCoreUsageTokens[i]}%";
                textCPUBlock.Text = $"CPU{i}";
                textCPUBlock.Margin = new Thickness(0, 4, 8, 6);
                // 监听 ProgressBar 的值改变事件，更新 TextBlock 的内容
                progressBar.ValueChanged += (sender, e) =>
                {
                    textBlock.Text = $"{CPUCoreUsageTokens[i]}%";
                    textCPUBlock.Text = $"CPU{i}";
                };
                textBlock.Margin = new Thickness(0, 4, 8, 6);
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

            // 先执行一次事件处理方法
            Timer_Tick(null, null);

            // 每隔段时间触发一次
            timer.Interval = TimeSpan.FromSeconds(3);

            // 启动计时器
            timer.Start();
        }

        // Linux 信息更新
        private async Task UpdateLinuxCMSModelAsync(CMSModel cmsModel)
        {
            // 定义异步任务
            Task<List<List<string>>> cpuUsages = Method.GetLinuxCPUUsageAsync(cmsModel);
            Task<List<string>> memUsages = Method.GetLinuxMEMUsageAsync(cmsModel);
            Task<string> HostName;
            HostName = Method.GetLinuxHostName(cmsModel);
            Task<string> UpTime = Method.GetLinuxUpTime(cmsModel);

            // 同时执行异步任务
            await Task.WhenAll(cpuUsages, memUsages, HostName, UpTime);

            // 处理获取到的数据
            try
            {
                cmsModel.CPUUsage = $"{cpuUsages.Result[0][0]}%";
                cmsModel.CPUUserUsage = $"{cpuUsages.Result[0][1]}%";
                cmsModel.CPUSysUsage = $"{cpuUsages.Result[0][2]}%";
                cmsModel.CPUIdleUsage = $"{cpuUsages.Result[0][3]}%";
                cmsModel.CPUIOUsage = $"{cpuUsages.Result[0][4]}%";
            }
            catch (Exception ex) { }
            try
            {
                // 计算内存占用百分比
                double memUsagesValue = (double.Parse(memUsages.Result[0]) - double.Parse(memUsages.Result[2])) * 100 / double.Parse(memUsages.Result[0]);
                cmsModel.MEMUsage = $"{memUsagesValue:F2}%";
            }
            catch (Exception ex) { }
            // 只有HostName和UpTime为空才更新
            if (cmsModel.HostName == null)
            {
                cmsModel.HostName = HostName.Result;
            }
            if (cmsModel.UpTime == null)
            {
                cmsModel.UpTime = UpTime.Result;
            }
            try
            {
                cmsModel.TotalMEM = $" of {Method.NetUnitConversion(decimal.Parse(memUsages.Result[0]) * 1024)}";
            }
            catch (Exception ex) { }

            string[] tokens = new string[] { "0" };
            try
            {
                tokens = cpuUsages.Result.Skip(1).Select(cpuUsage => cpuUsage[0]).ToArray();
            }
            catch (Exception ex) { }

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
            Task<List<List<string>>> cpuUsages = Method.GetLinuxCPUUsageAsync(cmsModel);
            Task<List<string>> memUsages = Method.GetLinuxMEMUsageAsync(cmsModel);
            Task<string[]> usages = Method.GetOpenWRTUsageAsync(cmsModel);
            Task<string> HostName = Method.GetOpenWRTHostName(cmsModel);
            Task<string> CPUCoreUsage = Method.GetOpenWRTCPUCoreUsageAsync(cmsModel);
            // OpenWRT也可以用部分Linux命令
            Task<string[]> netUsages = Method.GetLinuxNetAsync(cmsModel);
            Task<string> UpTime = Method.GetLinuxUpTime(cmsModel);

            // 同时执行异步任务
            await Task.WhenAll(cpuUsages, memUsages, usages, HostName, netUsages, UpTime, CPUCoreUsage);

            // 处理获取到的数据
            try
            {
                cmsModel.CPUUsage = $"{cpuUsages.Result[0]}%";
            }
            catch (Exception ex) { }
            //cmsModel.CPUUsage = $"{Math.Round(double.Parse(usages.Result[0]))}%";
            try
            {
                // 计算内存占用百分比
                int memUsagesValue = (int.Parse(memUsages.Result[0]) - int.Parse(memUsages.Result[2])) * 100 / int.Parse(memUsages.Result[0]);
                cmsModel.MEMUsage = $"{memUsagesValue}%";
            }
            catch (Exception ex) { }
            //cmsModel.MEMUsage = $"{Math.Round(double.Parse(usages.Result[1]))}%";
            cmsModel.TotalMEM = $" of {usages.Result[8]}";
            cmsModel.NETReceived = netUsages.Result[0];
            cmsModel.NETSent = netUsages.Result[1];
            cmsModel.Average1Percentage = usages.Result[5];
            cmsModel.Average5Percentage = usages.Result[6];
            cmsModel.Average15Percentage = usages.Result[7];
            // 只有HostName和UpTime为空才更新
            if (cmsModel.HostName == null)
            {
                cmsModel.HostName = HostName.Result;
            }
            if (cmsModel.UpTime == null)
            {
                cmsModel.UpTime = UpTime.Result;
            }

            // OpenWRT的Top无法查看单独核心占用
            string[] tokens = CPUCoreUsage.Result.Split(", ");
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

        private async void Timer_Tick(object sender, object e)
        {
            List<Task> tasks = new List<Task>();

            Task updateTask = dataList.OSType switch
            {
                "Linux" => UpdateLinuxCMSModelAsync(dataList),
                "OpenWRT" => UpdateLinuxCMSModelAsync(dataList),
                _ => Task.CompletedTask
            };

            tasks.Add(updateTask);

            await Task.WhenAll(tasks);
        }
        private void OpenSSHTerminal_Click(object sender, RoutedEventArgs e)
        {
            // dataList.SSHKey, dataList.SSHUser, dataList.HostIP, dataList.HostPort
            Method.SSHTerminal(dataList);
            logger.LogInfo("OpenSSHTerminal() completed.");
        }
        private void EditConfig_Click(object sender, RoutedEventArgs e)
        {
            EditThisConfig(dataList);
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
                logger.LogInfo("Edit Config is completed.");
            }
        }
    }
}
