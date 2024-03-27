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

        public static void CreateProgressBars(Grid container, string[] CPUCoreUsageTokens)
        {
            int numberOfBars = CPUCoreUsageTokens.Length;

            // ��� Grid ���ж������Ԫ��
            container.RowDefinitions.Clear();
            container.ColumnDefinitions.Clear();
            container.Children.Clear();

            // ����Ƿ���Ҫ����ж���
            if (container.ColumnDefinitions.Count == 0)
            {
                // ����һ��ColumnDefinition
                ColumnDefinition columnDefinition = new ColumnDefinition();

                // ���ÿ��Ϊ�Զ�������С�����ʣ��ռ�
                columnDefinition.Width = new GridLength(1, GridUnitType.Star);

                // ��ColumnDefinition��ӵ�Grid��ColumnDefinitions������
                container.ColumnDefinitions.Add(columnDefinition);

                // ����ж���
                container.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            }

            for (int i = 0; i < numberOfBars; i++)
            {
                // ����µ��ж���
                container.RowDefinitions.Add(new RowDefinition());

                ProgressBar progressBar = new ProgressBar();

                progressBar.Margin = new Thickness(0, 5, 0, 5);
                progressBar.Value = double.Parse(CPUCoreUsageTokens[i]);

                // ���� TextBlock ����ʾ�� ProgressBar ͬ����ֵ
                TextBlock textBlock = new TextBlock();
                textBlock.Text = $"{progressBar.Value.ToString().Split(".")[0]}%";
                // ���� ProgressBar ��ֵ�ı��¼������� TextBlock ������
                progressBar.ValueChanged += (sender, e) =>
                {
                    textBlock.Text = $"{progressBar.Value.ToString().Split(".")[0]}%";
                };
                textBlock.Margin = new Thickness(5, 0, 0, 0);
                textBlock.Width = 30;
                textBlock.HorizontalAlignment = HorizontalAlignment.Right;

                // ���� ProgressBar �� TextBlock ��λ��
                Grid.SetRow(progressBar, i);
                Grid.SetColumn(progressBar, 0);

                Grid.SetRow(textBlock, i);
                Grid.SetColumn(textBlock, 1);

                // �� ProgressBar �� TextBlock ��ӵ� Grid ��
                container.Children.Add(progressBar);
                container.Children.Add(textBlock);
            }
        }

        CMSModel dataList;
        private void LoadData()
        {
            // ʵ����SQLiteHelper
            SQLiteHelper dbHelper = new SQLiteHelper();

            // ��ѯ����
            dataList = dbHelper.GetDataById(Convert.ToInt32(localSettings.Values["ServerID"]));

            // �������б��
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
            string HostName = await Method.GetLinuxHostName(cmsModel);
            string UpTime = await Method.GetLinuxUpTime(cmsModel);
            List<MountInfo> MountInfos = await Method.GetLinuxMountInfo(cmsModel);
            List<NetworkInterfaceInfo> NetworkInterfaceInfos = await Method.GetLinuxNetworkInterfaceInfo(cmsModel);

            // �����ȡ��������
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

            // ֻ�е� ItemsSource δ��ʱ�Ž��а�
            if (MountInfosListView.ItemsSource == null)
            {
                MountInfosListView.ItemsSource = cmsModel.MountInfos;
            }
            if (NetworkInfosListView.ItemsSource == null)
            {
                NetworkInfosListView.ItemsSource = cmsModel.NetworkInterfaceInfos;
            }
        }

        // OpenWRT ��Ϣ����
        private async Task UpdateOpenWRTCMSModelAsync(CMSModel cmsModel)
        {
            // �����첽����
            string[] usages = await Method.GetOpenWRTCPUUsageAsync(cmsModel);
            string HostName = await Method.GetOpenWRTHostName(cmsModel);
            // OpenWRTҲ�����ò���Linux����
            string[] netUsages = await Method.GetLinuxNetAsync(cmsModel);
            string UpTime = await Method.GetLinuxUpTime(cmsModel);
            List<MountInfo> MountInfos = await Method.GetLinuxMountInfo(cmsModel);
            List<NetworkInterfaceInfo> NetworkInterfaceInfos = await Method.GetLinuxNetworkInterfaceInfo(cmsModel);

            // �����ȡ��������
            cmsModel.CPUUsage = usages[0];
            cmsModel.MEMUsage = usages[1];
            cmsModel.NETReceived = netUsages[0];
            cmsModel.NETSent = netUsages[1];
            cmsModel.HostName = HostName;
            cmsModel.UpTime = UpTime;
            cmsModel.MountInfos = MountInfos;
            cmsModel.NetworkInterfaceInfos = NetworkInterfaceInfos;

            // OpenWRT��Top�޷��鿴��������ռ��
            string[] tokens = new string[] { usages[0].Split("%")[0] };
            CreateProgressBars(progressBarsGrid, tokens);

            // ֻ�е� ItemsSource δ��ʱ�Ž��а�
            if (MountInfosListView.ItemsSource == null)
            {
                MountInfosListView.ItemsSource = cmsModel.MountInfos;
            }
            if (NetworkInfosListView.ItemsSource == null)
            {
                NetworkInfosListView.ItemsSource = cmsModel.NetworkInterfaceInfos;
            }
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
