using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SeeMyServer.Datas;
using SeeMyServer.Helper;
using SeeMyServer.Methods;
using SeeMyServer.Models;
using SeeMyServer.Pages.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using CommunityToolkit.WinUI.Controls;
using static PInvoke.User32;
using PInvoke;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Specialized;
using Microsoft.UI.Xaml.Navigation;

namespace SeeMyServer.Pages
{
    public sealed partial class HomePage : Page
    {
        // 启用本地设置数据
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        ResourceLoader resourceLoader = new ResourceLoader();
        private DispatcherQueue _dispatcherQueue;
        private DispatcherTimer timer;
        private Logger logger;

        public HomePage()
        {
            this.InitializeComponent();

            this.Loaded += Page_Loaded;
            this.Unloaded += Page_Unloaded;

            // 设置日志，最大1MB
            logger = new Logger(1);

            // 获取UI线程的DispatcherQueue
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            // 页面初始化后，加载数据
            LoadString();
            LoadData();
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            dataListView.SelectedItem = null;
        }
        private async void LoadString()
        {
            // 在异步方法中执行任务
            await Task.Run(() =>
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    ConfirmDelete.Content = resourceLoader.GetString("Confirm");
                    CancelDelete.Content = resourceLoader.GetString("Cancel");
                });
            });
        }

        private ObservableCollection<CMSModel> dataList;

        private void LoadData()
        {
            // 加载数据
            dataList = new ObservableCollection<CMSModel>(LoadDataFromDatabase());


            // 解析排序序列字符串为整数列表
            List<int> sortOrder = new List<int>();
            string sortOrderString = null;

            if (localSettings.Values["DataListOrder"] as string != null && localSettings.Values["DataListOrder"] as string != "")
            {
                sortOrderString = localSettings.Values["DataListOrder"] as string;
                sortOrder = sortOrderString.Split(',')
                                                     .Select(str => int.Parse(str.Trim()))
                                                     .ToList();
            }
            else if (dataList != null)
            {
                // 获取当前排序序列
                sortOrder = dataList.Select(item => item.Id).ToList();
                // 将排序序列转换为逗号分隔的字符串
                sortOrderString = string.Join(",", sortOrder);
                // 将排序序列字符串保存在本地设置中
                localSettings.Values["DataListOrder"] = sortOrderString;
            }

            // 根据排序序列对 dataList 进行排序
            dataList = new ObservableCollection<CMSModel>(sortOrder
                                            .Select(id => dataList.FirstOrDefault(item => item.Id == id))
                                            .Where(item => item != null));

            // 添加事件处理程序
            dataList.CollectionChanged += DataList_CollectionChanged;

            // 设置数据源
            dataListView.ItemsSource = dataList;

            string idsString = string.Join(", ", dataList.Select(item => item.Id));

            // 初始化占用
            foreach (CMSModel cmsModel in dataList)
            {
                InitItemDisplay(cmsModel);
            }
        }
        private void DataList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            string idsString = "";
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    logger.LogInfo("Items added:");
                    foreach (var item in e.NewItems)
                    {
                        logger.LogInfo($"Id: {(item as CMSModel).Id}, Name: {(item as CMSModel).Name}");
                    }
                    idsString = string.Join(", ", dataList.Select(item => item.Id));
                    logger.LogInfo(idsString);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    logger.LogInfo("Items removed:");
                    foreach (var item in e.OldItems)
                    {
                        logger.LogInfo($"Id: {(item as CMSModel).Id}, Name: {(item as CMSModel).Name}");
                    }
                    //idsString = string.Join(", ", dataList.Select(item => item.Id));
                    //logger.LogInfo(idsString);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    logger.LogInfo("Items replaced:");
                    foreach (var newItem in e.NewItems)
                    {
                        logger.LogInfo($"New Id: {(newItem as CMSModel).Id}, New Name: {(newItem as CMSModel).Name}");
                    }
                    foreach (var oldItem in e.OldItems)
                    {
                        logger.LogInfo($"Old Id: {(oldItem as CMSModel).Id}, Old Name: {(oldItem as CMSModel).Name}");
                    }
                    idsString = string.Join(", ", dataList.Select(item => item.Id));
                    logger.LogInfo(idsString);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    logger.LogInfo("Collection reset.");
                    idsString = string.Join(", ", dataList.Select(item => item.Id));
                    logger.LogInfo(idsString);
                    break;
                case NotifyCollectionChangedAction.Move:
                    logger.LogInfo($"Item moved from index {e.OldStartingIndex} to index {e.NewStartingIndex}.");
                    idsString = string.Join(", ", dataList.Select(item => item.Id));
                    logger.LogInfo(idsString);
                    break;
                default:
                    break;
            }
            localSettings.Values["DataListOrder"] = idsString;
        }

        private List<CMSModel> LoadDataFromDatabase()
        {
            // 实例化 SQLiteHelper
            SQLiteHelper dbHelper = new SQLiteHelper();

            // 查询数据
            return dbHelper.QueryData();
        }
        // 初始化显示
        private void InitItemDisplay(CMSModel cmsModel)
        {
            cmsModel.CPUUsage = "0%";
            cmsModel.MEMUsage = "0%";
            cmsModel.NETSent = "0 B/s ↑";
            cmsModel.NETReceived = "0 B/s ↓";
            cmsModel.DISKRead = "0 B/s R";
            cmsModel.DISKWrite = "0 B/s W";
        }
        // 在某处添加新项
        private void AddItem(CMSModel cmsModel)
        {
            dataList.Add(cmsModel);
            InitItemDisplay(cmsModel);

            // 手动通知 dataListView 更新
            RefreshListView();
        }

        // 在某处移除项
        private void RemoveItem(CMSModel cmsModel)
        {
            dataList.Remove(cmsModel);
            InitItemDisplay(cmsModel);

            // 手动通知 dataListView 更新
            RefreshListView();
        }

        // 手动更新 dataListView
        private void RefreshListView()
        {
            dataListView.ItemsSource = null;
            dataListView.ItemsSource = dataList;
            foreach (CMSModel cmsModel in dataList)
            {
                cmsModel.NumberOfFailures = 0;
                cmsModel.NumberOfFailuresStr = null;
            }
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
        private async Task UpdateLinuxCMSModelAsync(CMSModel cmsModel)
        {
            if (cmsModel.NumberOfFailures < 30)
            {
                // 定义异步任务
                var Usages = Method.GetLinuxCPUUsageAsync(cmsModel);

                // 同时执行异步任务
                await Task.WhenAll(Usages);

                if (Usages.Result != null)
                {
                    cmsModel.NumberOfFailures = 0;

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
                    //TopRec.Text = TOPRec;

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
                        cmsModel.Average1 = loadAverage[0];
                        cmsModel.Average5 = loadAverage[1];
                        cmsModel.Average15 = loadAverage[2];
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
                    catch (Exception ex) { }
                    try
                    {
                        double swapCached = double.Parse(memUsages[3]);
                        double swapTotal = double.Parse(memUsages[4]);
                        double swapFree = double.Parse(memUsages[5]);

                        if (swapTotal != 0)
                        {
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
                            cmsModel.SwapUsage = $"0%";
                            cmsModel.SwapCached = $"0%";
                            cmsModel.SwapCachedDisplay = $"0%";
                        }
                    }
                    catch (Exception ex) { }

                    try
                    {
                        cmsModel.TotalMEM = $"{Method.NetUnitConversion(decimal.Parse(memUsages[0]) * 1024)}";
                        cmsModel.TotalSwap = $"{Method.NetUnitConversion(decimal.Parse(memUsages[4]) * 1024)}";
                    }
                    catch (Exception ex) { }

                    try
                    {
                        cmsModel.CPUCoreTokens = cpuUsages.Skip(1).Select(cpuUsage => cpuUsage[0]).ToArray();
                    }
                    catch (Exception ex) { }

                    cmsModel.MountInfos = MountInfos;
                    cmsModel.NetworkInterfaceInfos = NetworkInterfaceInfos;

                    cmsModel.NETSent = $"{Method.NetUnitConversion(cmsModel.NetworkInterfaceInfos.Sum(iface => iface.TransmitSpeedByte))}/s ↑";
                    cmsModel.NETReceived = $"{Method.NetUnitConversion(cmsModel.NetworkInterfaceInfos.Sum(iface => iface.ReceiveSpeedByte))}/s ↓";

                    cmsModel.DISKRead = $"{Method.NetUnitConversion(DiskStatus.Sum(dstatus => dstatus.SectorsReadPerSecondOrigin))}/s R";
                    cmsModel.DISKWrite = $"{Method.NetUnitConversion(DiskStatus.Sum(dstatus => dstatus.SectorsWrittenPerSecondOrigin))}/s W";

                }
                else
                {
                    //logger.LogError($"The SSH result for {cmsModel.Name} is empty.");
                    cmsModel.NumberOfFailures += 30;
                }
            }
            else
            {
                cmsModel.NumberOfFailures -= 2;
            }

            if (cmsModel.NumberOfFailures > 30)
            {
                cmsModel.NumberOfFailuresStr = $"SSH failed ({cmsModel.NumberOfFailures - 30})";
            }
            else
            {
                cmsModel.NumberOfFailuresStr = null;
            }

            if (cmsModel.NumberOfFailures > 60)
            {
                cmsModel.NumberOfFailures = 60;
            }
        }
        private async void Timer_Tick(object sender, object e)
        {
            List<Task> tasks = new List<Task>();

            foreach (CMSModel cmsModel in dataList)
            {
                Task updateTask = cmsModel.OSType switch
                {
                    "Linux" => UpdateLinuxCMSModelAsync(cmsModel),
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
                int id = dbHelper.InsertData(initialCMSModelData);
                // 加载数据
                //LoadData();
                initialCMSModelData.Id = id;
                AddItem(initialCMSModelData);
                logger.LogInfo("Add Config is completed.");
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
                int id = dbHelper.InsertData(cmsModel);
                // 重新加载数据
                //LoadData();
                cmsModel.Id = id;
                AddItem(cmsModel);
                logger.LogInfo("Import Config is completed.");
            }
            HomePageImportConfig.IsEnabled = true;
        }
        private async void ReloadPage_Click(object sender, RoutedEventArgs e)
        {
            //App.m_window.NavigateToPage(typeof(HomePage));
            RefreshListView();
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
                //LoadData();
                RefreshListView();
                logger.LogInfo("Edit Config is completed.");
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
            //LoadData();
            RemoveItem(selectedModel);
            logger.LogInfo("Delete Config is completed.");
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

                // 打开终端
                MenuFlyoutItem terminalMenuItem = new MenuFlyoutItem
                {
                    Text = resourceLoader.GetString("terminalMenuItemText")
                };
                terminalMenuItem.Click += (sender, e) =>
                {
                    Method.SSHTerminal(selectedItem);
                };
                menuFlyout.Items.Add(terminalMenuItem);

                // 添加分割线
                MenuFlyoutSeparator separator = new MenuFlyoutSeparator();
                menuFlyout.Items.Add(separator);

                // 编辑
                MenuFlyoutItem editMenuItem = new MenuFlyoutItem
                {
                    Text = resourceLoader.GetString("editMenuItemText")
                };
                editMenuItem.Click += (sender, e) =>
                {
                    EditThisConfig(selectedItem);
                };
                menuFlyout.Items.Add(editMenuItem);

                // 删除
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
                MenuFlyoutSeparator separator2 = new MenuFlyoutSeparator();
                menuFlyout.Items.Add(separator2);

                // 导出
                MenuFlyoutItem exportMenuItem = new MenuFlyoutItem
                {
                    Text = resourceLoader.GetString("exportMenuItemText")
                };
                exportMenuItem.Click += (sender, e) =>
                {
                    ExportConfigFunction(selectedItem);
                };
                menuFlyout.Items.Add(exportMenuItem);

                // 在指定位置显示ContextMenu
                menuFlyout.ShowAt(listViewItem, e.GetPosition(listViewItem));
            }
        }
        // 处理单击事件的代码
        private void ContentGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem != null)
            {
                // 获取右键点击的数据对象（WoLModel）
                CMSModel selectedItem = e.ClickedItem as CMSModel;

                // 导航到页面
                App.m_window.NavigateToPage(typeof(DetailPage), selectedItem);
            }
        }
    }
}
