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

            // 将数据列表绑定到ListView
            dataListView.ItemsSource = dataList;

            // 创建并配置 DispatcherTimer
            timer = new DispatcherTimer();
            // 每隔段时间触发一次
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;

            // 启动计时器
            timer.Start();

            //SSHSendThread(dataList);
        }

        private async void Timer_Tick(object sender, object e)
        {
            foreach (CMSModel cmsModel in dataList)
            {
                string sshCMD = "powershell (Get-Counter '\\Processor Information(_Total)\\% Processor Utility').CounterSamples.CookedValue";
                string res = await Task.Run(() =>
                {
                    return Method.SendSSHCommand(sshCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
                });

                // 获取命令输出的值并转换为整数
                int value = int.Parse(res.Split('.')[0]);
                // 将值限制在 0 到 100 之间，并转换回字符串形式
                res = Math.Min(Math.Max(value, 0), 100).ToString();
                cmsModel.CPUUsage = res + "%";
            }
        }

        //private void LoadData()
        //{
        //    // 在子线程中执行任务
        //    Thread subThread = new Thread(new ThreadStart(() =>
        //    {
        //        while (true)
        //        {
        //            // 在UI线程上更新
        //            _dispatcherQueue.TryEnqueue(() =>
        //            {

        //                // 实例化SQLiteHelper
        //                SQLiteHelper dbHelper = new SQLiteHelper();

        //                // 查询数据
        //                List<CMSModel> dataList = dbHelper.QueryData();

        //                // 将数据列表绑定到ListView
        //                dataListView.ItemsSource = dataList;

        //            });

        //            // 延迟1s
        //            Thread.Sleep(1000);
        //        }
        //    }));
        //    subThread.Start();
        //}

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
        private void SSHSendThread(List<CMSModel> dataList)
        {
            foreach (CMSModel cmsModel in dataList)
            {
                // 在子线程中执行任务
                Thread subThread = new Thread(new ThreadStart(() =>
                {
                    while (true)
                    {                        //string sshCMD = "powershell (Get-Counter '\\Processor(_Total)\\% Processor Time').CounterSamples.CookedValue";
                        string sshCMD = "powershell (Get-Counter '\\Processor Information(_Total)\\% Processor Utility').CounterSamples.CookedValue";
                        string res = Method.SendSSHCommand(sshCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
                        res = res.Split('.')[0];
                        cmsModel.CPUUsage = res + "%";
                        //_dispatcherQueue.TryEnqueue(() =>
                        //{

                        //});
                        //Thread.Sleep(1000);
                    }
                }));
                subThread.Start();
            }
        }
    }
}
