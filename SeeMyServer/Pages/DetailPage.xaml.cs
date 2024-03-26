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
        // ���ñ�����������
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        ResourceLoader resourceLoader = new ResourceLoader();
        private DispatcherQueue _dispatcherQueue;
        private DispatcherTimer timer;

        public DetailPage()
        {
            this.InitializeComponent();

            // ��ȡUI�̵߳�DispatcherQueue
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            LoadData();
        }

        CMSModel dataList;
        private void LoadData()
        {
            // ʵ����SQLiteHelper
            SQLiteHelper dbHelper = new SQLiteHelper();

            // ��ѯ����
            dataList = dbHelper.GetDataById(Convert.ToInt32(localSettings.Values["ServerID"]));

            dataGrid.DataContext = dataList;

            // ��ʼ��ռ��
            dataList.CPUUsage = "0%";
            dataList.MEMUsage = "0%";
            dataList.NETSent = "0 B/s ��";
            dataList.NETReceived = "0 B/s ��";

            // ����������DispatcherTimer
            timer = new DispatcherTimer();
            timer.Tick += Timer_Tick;

            // ÿ����ʱ�䴥��һ��
            timer.Interval = TimeSpan.FromSeconds(5);

            // ��ִ��һ���¼�������
            Timer_Tick(null, null);

            // ������ʱ��
            timer.Start();
        }

        // Linux ��Ϣ����
        private async Task UpdateLinuxCMSModelAsync(CMSModel cmsModel)
        {
            // �����첽����
            string[] usages = await Method.GetLinuxUsageAsync(cmsModel);
            string[] netUsages = await Method.GetLinuxNetAsync(cmsModel);

            // �����ȡ��������
            cmsModel.CPUUsage = usages[0];
            cmsModel.MEMUsage = usages[1];
            cmsModel.NETReceived = netUsages[0];
            cmsModel.NETSent = netUsages[1];
        }

        // OpenWRT ��Ϣ����
        private async Task UpdateOpenWRTCMSModelAsync(CMSModel cmsModel)
        {
            // �����첽����
            string[] usages = await Method.GetOpenWRTCPUUsageAsync(cmsModel);
            // OpenWRTҲ������ifconfig��ѯ����
            string[] netUsages = await Method.GetLinuxNetAsync(cmsModel);

            // �����ȡ��������
            cmsModel.CPUUsage = usages[0];
            cmsModel.MEMUsage = usages[1];
            cmsModel.NETReceived = netUsages[0];
            cmsModel.NETSent = netUsages[1];
        }

        // Windows ��Ϣ����
        private async Task UpdateWindowsCMSModelAsync(CMSModel cmsModel)
        {
            // �����첽����
            Task<string> cpuTask = Method.GetWindowsCPUUsageAsync(cmsModel);
            Task<string> memTask = Method.GetWindowsMemoryUsageAsync(cmsModel);
            Task<string> netSentTask = Method.GetWindowsNetSentAsync(cmsModel);
            Task<string> netReceivedTask = Method.GetWindowsNetReceivedAsync(cmsModel);

            // ͬʱִ���첽����
            await Task.WhenAll(cpuTask, memTask, netSentTask, netReceivedTask);

            // �����ȡ��������
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
