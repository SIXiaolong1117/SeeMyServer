using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
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
        CMSModel dataList;

        public DetailPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            dataList = (CMSModel)e.Parameter;
            base.OnNavigatedTo(e);

            LoadData();

            this.Loaded += Page_Loaded;
            this.Unloaded += Page_Unloaded;

            // 设置日志，最大1MB
            logger = new Logger(1);
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
                container.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(45) });
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
                textBlock.Text = $"{progressBar.Value:F0}%";
                textCPUBlock.Text = $"CPU{i}";
                textCPUBlock.Margin = new Thickness(0, 4, 8, 6);
                // 监听 ProgressBar 的值改变事件，更新 TextBlock 的内容
                progressBar.ValueChanged += (sender, e) =>
                {
                    textBlock.Text = $"{progressBar.Value:F0}%";
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
        private void LoadData()
        {
            try
            {
                if (dataList.CPUCoreTokens != new string[] { "0" })
                {
                    if (progressBarsGrid.ColumnDefinitions.Count == 0)
                    {
                        progressBars = CreateProgressBars(progressBarsGrid, dataList.CPUCoreTokens, dataList.CPUCoreNum);
                    }
                    else if (progressBars != null)
                    {
                        UpdateProgressBars(progressBars, dataList.CPUCoreTokens, dataList.CPUCoreNum);
                    }
                }
            }
            catch { }

            try
            {
                MountInfosListView.ItemsSource = dataList.MountInfos;
                NetworkInfosListView.ItemsSource = dataList.NetworkInterfaceInfos;
            }
            catch { }

            try
            {
                if (dataList.SwapUsage != "0%" && dataList.SwapUsage != null)
                {
                    SwapCase1.Visibility = Visibility.Visible;
                    SwapCase2.Visibility = Visibility.Visible;
                    SwapTips1.Visibility = Visibility.Visible;
                    SwapTips2.Visibility = Visibility.Visible;
                }
                else
                {
                    SwapCase1.Visibility = Visibility.Collapsed;
                    SwapCase2.Visibility = Visibility.Collapsed;
                    SwapTips1.Visibility = Visibility.Collapsed;
                    SwapTips2.Visibility = Visibility.Collapsed;

                    dataList.SwapUsage = $"0%";
                    dataList.SwapCached = $"0%";
                    dataList.SwapCachedDisplay = $"0%";
                }
            }
            catch (Exception ex) { }

            // 将数据列表绑定
            dataGrid.DataContext = dataList;
        }
        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // 创建 DispatcherTimer 并启动
            timer = new DispatcherTimer();
            // 先执行一次事件处理方法
            Timer_Tick(null, null);
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();

            if (await dataList.UpdateSemaphore.WaitAsync(0))
            {
                // 确保释放信号量
                dataList.UpdateSemaphore.Release();
            }
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
                var LinuxKernelVersion = Usages.Result.Item5[5];
                var loadAverage = Usages.Result.Item6;


                // 只有HostName和UpTime为空才更新
                if (cmsModel.HostName == null || cmsModel.HostName == "")
                {
                    cmsModel.HostName = HostName;
                }
                cmsModel.UpTime = UpTime;
                if (cmsModel.OSRelease == null || cmsModel.OSRelease == "")
                {
                    cmsModel.OSRelease = PRETTY_NAME;
                }
                if (cmsModel.CPUCoreNum == null || cmsModel.CPUCoreNum == "")
                {
                    cmsModel.CPUCoreNum = CPUCoreNum;
                }
                cmsModel.TopRes = TOPRec;
                cmsModel.LinuxKernelVersionRes = LinuxKernelVersion;

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
                catch (Exception ex)
                {
                    logger.LogError(ex.Message);
                }

                // 获取结果失败不更新
                if (loadAverage[3] != "0" || loadAverage[4] != "0" || loadAverage[5] != "0")
                {
                    cmsModel.Average1Percentage = loadAverage[3];
                    cmsModel.Average5Percentage = loadAverage[4];
                    cmsModel.Average15Percentage = loadAverage[5];
                }

                try
                {
                    double memTotal = double.Parse(memUsages[0]);
                    double memFree = double.Parse(memUsages[1]);
                    double memAvailable = double.Parse(memUsages[2]);

                    // 计算内存占用百分比
                    double memUsagesValue = (memTotal - memAvailable) * 100 / memTotal;
                    cmsModel.MEMUsage = $"{memUsagesValue:F0}%";
                    // Free 百分比
                    double memFreeValue = memFree * 100 / memTotal;
                    cmsModel.MEMFree = $"{memFreeValue:F2}%";
                    // Available 百分比
                    double memAvailableValue = memAvailable * 100 / memTotal;
                    cmsModel.MEMAvailable = $"{memAvailableValue:F2}%";
                    // 页面缓存
                    double memUsagePageCacheValue = memUsagesValue + (memAvailableValue - memFreeValue);
                    cmsModel.MEMUsagePageCache = $"{memUsagePageCacheValue:F2}%";
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.Message);
                }
                try
                {
                    double swapCached = double.Parse(memUsages[3]);
                    double swapTotal = double.Parse(memUsages[4]);
                    double swapFree = double.Parse(memUsages[5]);

                    if (swapTotal != 0)
                    {
                        SwapCase1.Visibility = Visibility.Visible;
                        SwapCase2.Visibility = Visibility.Visible;
                        SwapTips1.Visibility = Visibility.Visible;
                        SwapTips2.Visibility = Visibility.Visible;

                        // Swap 占用百分比
                        double swapUsagesValue = (swapTotal - swapFree) * 100 / swapTotal;
                        cmsModel.SwapUsage = $"{swapUsagesValue:F0}%";
                        // Swap Cached 百分比
                        double swapCachedValue = swapCached * 100 / swapTotal;
                        cmsModel.SwapCached = $"{swapCachedValue:F2}%";
                        double swapCachedDisplay = swapUsagesValue + swapCachedValue;
                        cmsModel.SwapCachedDisplay = $"{swapCachedDisplay:F2}%";
                    }
                    else
                    {
                        SwapCase1.Visibility = Visibility.Collapsed;
                        SwapCase2.Visibility = Visibility.Collapsed;
                        SwapTips1.Visibility = Visibility.Collapsed;
                        SwapTips2.Visibility = Visibility.Collapsed;

                        cmsModel.SwapUsage = $"0%";
                        cmsModel.SwapCached = $"0%";
                        cmsModel.SwapCachedDisplay = $"0%";
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.Message);
                }
                try
                {
                    cmsModel.TotalMEM = $"{Method.NetUnitConversion(decimal.Parse(memUsages[0]) * 1024)}";
                    cmsModel.TotalSwap = $"{Method.NetUnitConversion(decimal.Parse(memUsages[4]) * 1024)}";
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.Message);
                }

                try
                {
                    cmsModel.CPUCoreTokens = cpuUsages.Skip(1).Select(cpuUsage => cpuUsage[0]).ToArray();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.Message);
                }

                if (cmsModel.CPUCoreTokens != new string[] { "0" })
                {
                    if (progressBarsGrid.ColumnDefinitions.Count == 0)
                    {
                        progressBars = CreateProgressBars(progressBarsGrid, cmsModel.CPUCoreTokens, cmsModel.CPUCoreNum);
                    }
                    else if (progressBars != null)
                    {
                        UpdateProgressBars(progressBars, cmsModel.CPUCoreTokens, cmsModel.CPUCoreNum);
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

                foreach (MountInfo mountInfo in cmsModel.MountInfos)
                {
                    if (mountInfo.SectorsReadPerSecond == null)
                    {
                        mountInfo.SectorsReadPerSecond = $"N/A";
                    }
                    if (mountInfo.SectorsWrittenPerSecond == null)
                    {
                        mountInfo.SectorsWrittenPerSecond = $"N/A";
                    }
                    if (mountInfo.SectorsReadBytes == null)
                    {
                        mountInfo.SectorsReadBytes = $"N/A";
                    }
                    if (mountInfo.SectorsWrittenBytes == null)
                    {
                        mountInfo.SectorsWrittenBytes = $"N/A";
                    }
                }

                // 获取结果失败不更新
                if (loadAverage[3] != "0" || loadAverage[4] != "0" || loadAverage[5] != "0")
                {
                    cmsModel.Average1 = loadAverage[0];
                    cmsModel.Average5 = loadAverage[1];
                    cmsModel.Average15 = loadAverage[2];
                    cmsModel.Average1Percentage = loadAverage[3];
                    cmsModel.Average5Percentage = loadAverage[4];
                    cmsModel.Average15Percentage = loadAverage[5];
                }
            }
        }

        private async void Timer_Tick(object sender, object e)
        {
            List<Task> tasks = new List<Task>();
            if (dataList.NumberOfFailuresSec <= 1)
            {
                dataList.NumberOfFailuresStr = $"";
                if (dataList.NumberOfFailures <= 5)
                {
                    // 清空失败计数
                    dataList.NumberOfFailures = 0;

                    // 尝试立即获取信号量，如果无法获取则跳过这次更新
                    if (await dataList.UpdateSemaphore.WaitAsync(0))
                    {
                        Task updateTask = dataList.OSType switch
                        {
                            "Linux" => UpdateLinuxCMSModelAsync(dataList),
                            _ => Task.CompletedTask
                        };

                        tasks.Add(updateTask);
                    }
                    else
                    {
                        // 失败计数
                        dataList.NumberOfFailures += 1;
                    }
                }
                else
                {
                    // 失败倒计时，设置为60
                    dataList.NumberOfFailuresSec = 60;
                    // 清空失败计数
                    dataList.NumberOfFailures = 0;
                }
            }
            else
            {
                dataList.NumberOfFailuresSec -= 1;
                dataList.NumberOfFailuresStr = $"SSH failed ({dataList.NumberOfFailuresSec})";
            }

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
            dataList.NumberOfFailuresStr = null;
            dataList.NumberOfFailures = 0;
            dataList.NumberOfFailuresSec = 0;
            App.m_window.NavigateToPage(typeof(DetailPage), dataList);
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
