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
        }

        private List<CMSModel> dataList;

        private void LoadData()
        {
            // 实例化SQLiteHelper
            SQLiteHelper dbHelper = new SQLiteHelper();

            // 查询数据
            dataList = dbHelper.QueryData();

            // 初始化占用
            foreach (CMSModel cmsModel in dataList)
            {
                cmsModel.CPUUsage = "0%";
                cmsModel.MEMUsage = "0%";
            }

            // 将数据列表绑定到ListView
            dataListView.ItemsSource = dataList;

            // 创建并配置 DispatcherTimer
            timer = new DispatcherTimer();
            // 每隔段时间触发一次
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;

            // 启动计时器
            timer.Start();
        }

        private async void Timer_Tick(object sender, object e)
        {
            // 停止计时器
            //timer.Stop();

            foreach (CMSModel cmsModel in dataList)
            {
                if (cmsModel.OSType == "Windows")
                {
                    // 会引起 Windows Defender 实时保护的警觉，导致 Antimalware Service 占用高。
                    // CPU 占用（Processor Utility对应任务管理器性能页面的CPU占用，Processor Time对应任务管理器详情信息页面的CPU）
                    string cpuUsageCMD = "powershell (Get-Counter '\\Processor Information(_Total)\\% Processor Utility').CounterSamples.CookedValue";
                    string cpuUsageRes = await Task.Run(() =>
                    {
                        return Method.SendSSHCommand(cpuUsageCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
                    });
                    // 获取命令输出的值并转换为整数
                    int cpuUsageResValue = int.Parse(cpuUsageRes.Split('.')[0]);
                    // 将值限制在 0 到 100 之间，并转换回字符串形式
                    cpuUsageRes = Math.Min(Math.Max(cpuUsageResValue, 0), 100).ToString();

                    // 内存占用
                    string memUsageCMD = "powershell ((($totalMemory = (Get-WmiObject -Class Win32_OperatingSystem).TotalVisibleMemorySize) - (Get-WmiObject -Class Win32_OperatingSystem).FreePhysicalMemory) / $totalMemory * 100)";
                    string memUsageRes = await Task.Run(() =>
                    {
                        return Method.SendSSHCommand(memUsageCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
                    });
                    //// 获取命令输出的值并转换为整数
                    int memUsageResValue = int.Parse(memUsageRes.Split('.')[0]);
                    //// 将值限制在 0 到 100 之间，并转换回字符串形式
                    memUsageRes = Math.Min(Math.Max(memUsageResValue, 0), 100).ToString();


                    cmsModel.CPUUsage = cpuUsageRes + "%";
                    cmsModel.MEMUsage = memUsageRes + "%";
                }
            }

            // 重新启动计时器
            //timer.Start();
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
    }
}
