using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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

        CMSModel dataList;
        private void LoadData()
        {
            // 实例化SQLiteHelper
            SQLiteHelper dbHelper = new SQLiteHelper();

            // 查询数据
            dataList = dbHelper.GetDataById(Convert.ToInt32(localSettings.Values["ServerID"]));

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

            // 处理获取到的数据
            cmsModel.CPUUsage = usages[0];
            cmsModel.MEMUsage = usages[1];
            cmsModel.NETReceived = netUsages[0];
            cmsModel.NETSent = netUsages[1];
        }

        // OpenWRT 信息更新
        private async Task UpdateOpenWRTCMSModelAsync(CMSModel cmsModel)
        {
            // 定义异步任务
            string[] usages = await Method.GetOpenWRTCPUUsageAsync(cmsModel);
            // OpenWRT也可以用ifconfig查询网速
            string[] netUsages = await Method.GetLinuxNetAsync(cmsModel);

            // 处理获取到的数据
            cmsModel.CPUUsage = usages[0];
            cmsModel.MEMUsage = usages[1];
            cmsModel.NETReceived = netUsages[0];
            cmsModel.NETSent = netUsages[1];
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
