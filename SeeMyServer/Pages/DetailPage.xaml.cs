using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json.Linq;
using SeeMyServer.Datas;
using SeeMyServer.Helper;
using SeeMyServer.Methods;
using SeeMyServer.Models;
using SeeMyServer.Pages.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
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

            this.Loaded += Page_Loaded;
            this.Unloaded += Page_Unloaded;

            // 设置日志，最大1MB
            logger = new Logger(1);

            LoadData();
        }

        public static List<ProgressBar> CreateProgressBars(Grid container, string[] CPUCoreUsageTokens, string CPUCoreNum)
        {
            int numberOfBars = int.Parse(CPUCoreNum);

            // 清除 Grid 的行定义和子元素
            container.RowDefinitions.Clear();
            container.Children.Clear();

            // 检查是否需要添加列定义
            if (container.ColumnDefinitions.Count == 0)
            {
                // 创建一个ColumnDefinition
                ColumnDefinition columnDefinition = new ColumnDefinition();

                // 设置宽度为自动调整大小以填充剩余空间
                columnDefinition.Width = new GridLength(1, GridUnitType.Star);

                // 将ColumnDefinition添加到Grid的ColumnDefinitions集合中
                container.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(50) });
                container.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(65) });
                container.ColumnDefinitions.Add(columnDefinition);
            }

            List<ProgressBar> progressBars = new List<ProgressBar>();

            for (int i = 0; i < numberOfBars; i++)
            {
                // 添加新的行定义
                container.RowDefinitions.Add(new RowDefinition());

                ProgressBar progressBar = new ProgressBar();

                progressBar.Margin = new Thickness(0, 4, 0, 4);

                // 设置ProgressBar的前景色为指定的SolidColorBrush对象
                progressBar.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x42, 0xCD, 0xEF));

                try
                {
                    progressBar.Value = double.Parse(CPUCoreUsageTokens[i]);
                }
                catch (Exception ex) { }

                // 创建 TextBlock 来显示与 ProgressBar 同步的值
                TextBlock textBlock = new TextBlock();
                TextBlock textCPUBlock = new TextBlock();
                textBlock.Text = $"{progressBar.Value:F2}%";
                textCPUBlock.Text = $"CPU{i}";
                textCPUBlock.Margin = new Thickness(0, 4, 8, 6);
                // 监听 ProgressBar 的值改变事件，更新 TextBlock 的内容
                progressBar.ValueChanged += (sender, e) =>
                {
                    textBlock.Text = $"{progressBar.Value:F2}%";
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

                // 将创建的ProgressBar添加到列表中
                progressBars.Add(progressBar);
            }

            return progressBars;
        }

        public static void UpdateProgressBars(List<ProgressBar> progressBars, string[] CPUCoreUsageTokens, string CPUCoreNum)
        {
            try
            {
                int numberOfBars = int.Parse(CPUCoreNum);

                for (int i = 0; i < numberOfBars; i++)
                {
                    //throw new Exception($"{progressBars[0].Value}");
                    progressBars[i].Value = double.Parse(CPUCoreUsageTokens[i]);
                }
            }
            catch { }
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

            // 初始化占用（即便失败也没关系）
            try
            {
                dataList.CPUUsage = "0%";
                dataList.MEMUsage = "0%";
                dataList.NETSent = "0 B/s ↑";
                dataList.NETReceived = "0 B/s ↓";
                dataList.CPUUserUsage = "0.00%";
                dataList.CPUSysUsage = "0.00%";
                dataList.CPUIdleUsage = "0.00%";
                dataList.CPUIOUsage = "0.00%";
                dataList.MEMFree = "0.00%";
                dataList.MEMAvailable = "0.00%";
                CPULoadAverage1.Text = $"0.00";
                CPULoadAverage5.Text = $"0.00";
                CPULoadAverage15.Text = $"0.00";
            }
            catch { }
        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // 创建 DispatcherTimer 并启动
            timer = new DispatcherTimer();
            // 先执行一次事件处理方法
            Timer_Tick(null, null);
            timer.Interval = TimeSpan.FromSeconds(2);
            timer.Tick += Timer_Tick;
            timer.Start();
        }
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            // 页面卸载时停止并销毁 DispatcherTimer
            if (timer != null)
            {
                timer.Stop();
                timer.Tick -= Timer_Tick;
                timer = null;
            }
        }

        // Linux 信息更新
        List<ProgressBar> progressBars = new List<ProgressBar>();
        private async Task UpdateLinuxCMSModelAsync(CMSModel cmsModel)
        {
            // 定义异步任务
            var Usages = Method.GetLinuxCPUUsageAsync(cmsModel);

            // 同时执行异步任务
            await Task.WhenAll(Usages);

            if (Usages.Result != null)
            {
                // 解析结果
                var cpuUsages = Usages.Result.Item1;
                var memUsages = Usages.Result.Item2;
                var NetworkInterfaceInfos = Usages.Result.Item3;
                var MountInfos = Usages.Result.Item4[0];
                var DiskStatus = Usages.Result.Item4[1];
                var UpTime = Usages.Result.Item5[0];
                var HostName = Usages.Result.Item5[1];
                var CPUCoreNum = Usages.Result.Item5[2];
                var PRETTY_NAME = Usages.Result.Item5[3];
                var TOPRec = Usages.Result.Item5[4];
                var loadAverage = Usages.Result.Item6;


                // 只有HostName和UpTime为空才更新
                if (cmsModel.HostName == null)
                {
                    cmsModel.HostName = HostName;
                }
                if (cmsModel.UpTime == null)
                {
                    cmsModel.UpTime = UpTime;
                }
                if (cmsModel.OSRelease == null)
                {
                    cmsModel.OSRelease = PRETTY_NAME;
                }
                TopRec.Text = TOPRec;

                // 处理获取到的数据
                try
                {
                    cmsModel.CPUUsage = $"{cpuUsages[0][0]}%";
                    cmsModel.CPUCoreNum = CPUCoreNum.Split('\n')[0];
                    cmsModel.CPUUserUsage = $"{cpuUsages[0][1]}%";
                    cmsModel.CPUSysUsage = $"{cpuUsages[0][2]}%";
                    cmsModel.CPUIdleUsage = $"{cpuUsages[0][3]}%";
                    cmsModel.CPUIOUsage = $"{cpuUsages[0][4]}%";
                }
                catch (Exception ex) { }

                // 获取结果失败不更新
                if (loadAverage[3] != "0" || loadAverage[4] != "0" || loadAverage[5] != "0")
                {
                    cmsModel.Average1Percentage = loadAverage[3];
                    cmsModel.Average5Percentage = loadAverage[4];
                    cmsModel.Average15Percentage = loadAverage[5];
                }

                try
                {
                    // 计算内存占用百分比
                    double memUsagesValue = (double.Parse(memUsages[0]) - double.Parse(memUsages[2])) * 100 / double.Parse(memUsages[0]);
                    cmsModel.MEMUsage = $"{memUsagesValue:F2}%";
                    double memFreeValue = double.Parse(memUsages[1]) * 100 / double.Parse(memUsages[0]);
                    cmsModel.MEMFree = $"{memFreeValue:F2}%";
                    double memAvailableValue = double.Parse(memUsages[2]) * 100 / double.Parse(memUsages[0]);
                    cmsModel.MEMAvailable = $"{memAvailableValue:F2}%";
                    // 页面缓存
                    double memUsagePageCacheValue = memUsagesValue + (memAvailableValue - memFreeValue);
                    cmsModel.MEMUsagePageCache = $"{memUsagePageCacheValue:F2}%";
                }
                catch (Exception ex) { }
                try
                {
                    cmsModel.TotalMEM = $" of {Method.NetUnitConversion(decimal.Parse(memUsages[0]) * 1024)}";
                }
                catch (Exception ex) { }

                string[] tokens = new string[] { "0" };
                try
                {
                    tokens = cpuUsages.Skip(1).Select(cpuUsage => cpuUsage[0]).ToArray();
                }
                catch (Exception ex) { }

                if (tokens != new string[] { "0" })
                {
                    if (progressBarsGrid.ColumnDefinitions.Count == 0)
                    {
                        progressBars = CreateProgressBars(progressBarsGrid, tokens, CPUCoreNum);
                    }
                    else if (progressBars != null)
                    {
                        UpdateProgressBars(progressBars, tokens, CPUCoreNum);
                    }
                }

                // 挂载和网络信息
                if (cmsModel.MountInfos != null)
                {
                    // 禁用过渡动画
                    MountInfosListView.ItemContainerTransitions = null;
                }
                if (cmsModel.NetworkInterfaceInfos != null)
                {
                    // 禁用过渡动画
                    NetworkInfosListView.ItemContainerTransitions = null;
                }
                cmsModel.MountInfos = MountInfos;
                MountInfosListView.ItemsSource = cmsModel.MountInfos;
                cmsModel.NetworkInterfaceInfos = NetworkInterfaceInfos;
                NetworkInfosListView.ItemsSource = cmsModel.NetworkInterfaceInfos;

                cmsModel.NETSent = $"{Method.NetUnitConversion(cmsModel.NetworkInterfaceInfos.Sum(iface => iface.TransmitSpeedByte))}/s ↑";
                cmsModel.NETReceived = $"{Method.NetUnitConversion(cmsModel.NetworkInterfaceInfos.Sum(iface => iface.ReceiveSpeedByte))}/s ↓";

                cmsModel.DISKRead = $"{Method.NetUnitConversion(DiskStatus.Sum(dstatus => dstatus.SectorsReadPerSecondOrigin))}/s R";
                cmsModel.DISKWrite = $"{Method.NetUnitConversion(DiskStatus.Sum(dstatus => dstatus.SectorsWrittenPerSecondOrigin))}/s W";

                // 获取结果失败不更新
                if (loadAverage[3] != "0" || loadAverage[4] != "0" || loadAverage[5] != "0")
                {
                    cmsModel.Average1Percentage = loadAverage[3];
                    cmsModel.Average5Percentage = loadAverage[4];
                    cmsModel.Average15Percentage = loadAverage[5];
                    CPULoadAverage1.Text = $"{loadAverage[0]}";
                    CPULoadAverage5.Text = $"{loadAverage[1]}";
                    CPULoadAverage15.Text = $"{loadAverage[2]}";
                }
            }
            else
            {
                logger.LogError($"The SSH result for {cmsModel.Name} is empty.");
            }
        }

        private async void Timer_Tick(object sender, object e)
        {
            List<Task> tasks = new List<Task>();

            Task updateTask = dataList.OSType switch
            {
                "Linux" => UpdateLinuxCMSModelAsync(dataList),
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

        private async void ReloadPage_Click(object sender, RoutedEventArgs e)
        {
            App.m_window.NavigateToPage(typeof(DetailPage));
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
