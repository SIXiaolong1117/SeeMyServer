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

            // 设置日志，最大1MB
            logger = new Logger(1);

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

            // 先执行一次事件处理方法
            Timer_Tick(null, null);

            // 每隔段时间触发一次
            timer.Interval = TimeSpan.FromSeconds(3);

            // 启动计时器
            timer.Start();
            logger.LogInfo("Timer_Tick starts.");
        }

        // Linux 信息更新
        private async Task UpdateLinuxCMSModelAsync(CMSModel cmsModel)
        {
            // 定义异步任务
            Task<List<List<string>>> cpuUsages = Method.GetLinuxCPUUsageAsync(cmsModel);
            Task<List<string>> memUsages = Method.GetLinuxMEMUsageAsync(cmsModel);
            Task<string[]> loadAverage = Method.GetLinuxLoadAverageAsync(cmsModel);

            //Task<string[]> usages = Method.GetLinuxUsageAsync(cmsModel);
            Task<string[]> netUsages = Method.GetLinuxNetAsync(cmsModel);

            // 同时执行异步任务
            await Task.WhenAll(cpuUsages, memUsages, loadAverage, netUsages);

            // 处理获取到的数据
            try
            {
                cmsModel.CPUUsage = $"{cpuUsages.Result[0][0].Split(".")[0]}%";
            }
            catch (Exception ex) { }
            try
            {
                // 计算内存占用百分比
                double memUsagesValue = (double.Parse(memUsages.Result[0]) - double.Parse(memUsages.Result[2])) * 100 / double.Parse(memUsages.Result[0]);
                cmsModel.MEMUsage = $"{memUsagesValue:F0}%";
            }
            catch (Exception ex) { }
            cmsModel.NETReceived = netUsages.Result[0];
            cmsModel.NETSent = netUsages.Result[1];
            cmsModel.Average1Percentage = loadAverage.Result[3];
            cmsModel.Average5Percentage = loadAverage.Result[4];
            cmsModel.Average15Percentage = loadAverage.Result[5];
        }

        // OpenWRT 信息更新
        private async Task UpdateOpenWRTCMSModelAsync(CMSModel cmsModel)
        {
            // 定义异步任务
            Task<string[]> usages = Method.GetOpenWRTUsageAsync(cmsModel);
            // OpenWRT也可以用ifconfig查询网速
            Task<string[]> netUsages = Method.GetLinuxNetAsync(cmsModel);

            // 同时执行异步任务
            await Task.WhenAll(usages, netUsages);

            // 处理获取到的数据
            //cmsModel.CPUUsage = usages.Result[0];
            cmsModel.CPUUsage = $"{Math.Round(double.Parse(usages.Result[0]))}%";
            //cmsModel.MEMUsage = usages.Result[1];
            cmsModel.MEMUsage = $"{Math.Round(double.Parse(usages.Result[1]))}%";
            cmsModel.NETReceived = netUsages.Result[0];
            cmsModel.NETSent = netUsages.Result[1];
            cmsModel.Average1Percentage = usages.Result[5];
            cmsModel.Average5Percentage = usages.Result[6];
            cmsModel.Average15Percentage = usages.Result[7];
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
                dbHelper.InsertData(cmsModel);
                // 重新加载数据
                LoadData();
                logger.LogInfo("Import Config is completed.");
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
            LoadData();
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
                MenuFlyoutSeparator separator2 = new MenuFlyoutSeparator();
                menuFlyout.Items.Add(separator2);

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
        // 处理左键单击事件的代码
        private void OnListViewTapped(object sender, TappedRoutedEventArgs e)
        {
            FrameworkElement listViewItem = (sender as FrameworkElement);

            if (listViewItem != null)
            {
                // 获取右键点击的数据对象（WoLModel）
                CMSModel selectedItem = listViewItem?.DataContext as CMSModel;

                localSettings.Values["ServerID"] = selectedItem.Id.ToString();

                // 导航到页面
                App.m_window.NavigateToPage(typeof(DetailPage));
            }
        }
    }
}
