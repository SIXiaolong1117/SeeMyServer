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
using System.Diagnostics;
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
        // ���ñ�����������
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        ResourceLoader resourceLoader = new ResourceLoader();
        private DispatcherTimer timer;

        public DetailPage()
        {
            this.InitializeComponent();

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
                container.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
                container.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
                container.ColumnDefinitions.Add(columnDefinition);
            }

            for (int i = 0; i < numberOfBars; i++)
            {
                // ����µ��ж���
                container.RowDefinitions.Add(new RowDefinition());

                ProgressBar progressBar = new ProgressBar();

                progressBar.Margin = new Thickness(0, 4, 0, 4);
                try { 
                progressBar.Value = double.Parse(CPUCoreUsageTokens[i]);
                }catch (Exception ex) { }

                // ���� TextBlock ����ʾ�� ProgressBar ͬ����ֵ
                TextBlock textBlock = new TextBlock();
                TextBlock textCPUBlock = new TextBlock();
                textBlock.Text = $"{progressBar.Value.ToString().Split(".")[0]}%";
                textCPUBlock.Text = $"CPU{i}";
                textCPUBlock.Margin = new Thickness(0, 4, 8, 6);
                // ���� ProgressBar ��ֵ�ı��¼������� TextBlock ������
                progressBar.ValueChanged += (sender, e) =>
                {
                    textBlock.Text = $"{progressBar.Value.ToString().Split(".")[0]}%";
                    textCPUBlock.Text = $"CPU{i}";
                };
                textBlock.Margin = new Thickness(8, 4, 0, 6);
                textBlock.Width = 40;
                textBlock.HorizontalAlignment = HorizontalAlignment.Right;

                // ����λ��
                Grid.SetRow(textCPUBlock, i);
                Grid.SetColumn(textCPUBlock, 0);

                Grid.SetRow(textBlock, i);
                Grid.SetColumn(textBlock, 1);

                Grid.SetRow(progressBar, i);
                Grid.SetColumn(progressBar, 2);

                // ��ӵ� Grid ��
                container.Children.Add(textCPUBlock);
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
            string[] usages = await Method.GetLinuxUsageAsync(cmsModel);
            string[] netUsages = await Method.GetLinuxNetAsync(cmsModel);
            string HostName = await Method.GetLinuxHostName(cmsModel);
            string UpTime = await Method.GetLinuxUpTime(cmsModel);

            // �����ȡ��������
            cmsModel.CPUUsage = usages[0];
            cmsModel.MEMUsage = usages[1];
            cmsModel.NETReceived = netUsages[0];
            cmsModel.NETSent = netUsages[1];
            cmsModel.HostName = HostName;
            cmsModel.UpTime = UpTime;
            cmsModel.TotalMEM = $" of {usages[4]} GB";

            //Debug.Text = usages[2];
            //Debug2.Text = usages[3];

            string[] tokens = usages[2].Split(", ");
            CreateProgressBars(progressBarsGrid, tokens);

            // ֻ�е� ItemsSource δ��ʱ�Ž��а�
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

        // OpenWRT ��Ϣ����
        private async Task UpdateOpenWRTCMSModelAsync(CMSModel cmsModel)
        {
            string[] usages = await Method.GetOpenWRTCPUUsageAsync(cmsModel);
            string HostName = await Method.GetOpenWRTHostName(cmsModel);
            // OpenWRTҲ�����ò���Linux����
            string[] netUsages = await Method.GetLinuxNetAsync(cmsModel);
            string UpTime = await Method.GetLinuxUpTime(cmsModel);

            // �����ȡ��������
            cmsModel.CPUUsage = usages[0];
            cmsModel.MEMUsage = usages[1];
            cmsModel.NETReceived = netUsages[0];
            cmsModel.NETSent = netUsages[1];
            cmsModel.HostName = HostName;
            cmsModel.UpTime = UpTime;

            // OpenWRT��Top�޷��鿴��������ռ��
            string[] tokens = new string[] { usages[0].Split("%")[0] };
            CreateProgressBars(progressBarsGrid, tokens);

            // ֻ�е� ItemsSource δ��ʱ�Ž��а�
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

        // Windows ��Ϣ����
        private async Task UpdateWindowsCMSModelAsync(CMSModel cmsModel)
        {
            string[] usages = await Method.GetWindowsUsageAsync(cmsModel);
            string upTime = await Method.GetWindowsUpTime(cmsModel);
            Task<string> memTask = Method.GetWindowsMemoryUsageAsync(cmsModel);
            Task<string> netSentTask = Method.GetWindowsNetSentAsync(cmsModel);
            Task<string> netReceivedTask = Method.GetWindowsNetReceivedAsync(cmsModel);
            // Windows�Ͽ���ʹ����ͬ������
            string HostName = await Method.GetLinuxHostName(cmsModel);

            // ͬʱִ���첽����
            await Task.WhenAll(memTask, netSentTask, netReceivedTask);

            // �����ȡ��������
            cmsModel.CPUUsage = usages[0];
            cmsModel.MEMUsage = memTask.Result;
            cmsModel.NETSent = netSentTask.Result;
            cmsModel.NETReceived = netReceivedTask.Result;
            cmsModel.HostName = HostName.TrimEnd();
            cmsModel.UpTime = upTime;
            cmsModel.TotalMEM = $" of {usages[4]} GB";

            string[] tokens = usages[2].Split(", ");
            CreateProgressBars(progressBarsGrid, tokens);

            // ֻ�е� ItemsSource δ��ʱ�Ž���
            if (MountInfosListView.ItemsSource == null)
            {
                List<MountInfo> MountInfos = await Method.GetWindowsMountInfo(cmsModel);
                cmsModel.MountInfos = MountInfos;
                MountInfosListView.ItemsSource = cmsModel.MountInfos;
            }
            if (NetworkInfosListView.ItemsSource == null)
            {
                List<NetworkInterfaceInfo> NetworkInterfaceInfos = await Method.GetWindowsNetworkInterfaceInfo(cmsModel);
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
                "OpenWRT" => UpdateOpenWRTCMSModelAsync(dataList),
                "Windows" => UpdateWindowsCMSModelAsync(dataList),
                _ => Task.CompletedTask
            };

            tasks.Add(updateTask);

            await Task.WhenAll(tasks);
        }
        private void OpenSSHTerminal_Click(object sender, RoutedEventArgs e)
        {
            SSHTerminal(dataList.SSHKey, dataList.SSHUser, dataList.HostIP, dataList.HostPort);
        }
        private void EditConfig_Click(object sender, RoutedEventArgs e)
        {
            EditThisConfig(dataList);
        }
        public static void SSHTerminal(string KeyPath, string User, string Domain, string Port)
        {
            // ����һ���µĽ���
            Process process = new Process();
            // ָ������PowerShell
            process.StartInfo.FileName = "PowerShell.exe";
            // ����
            process.StartInfo.Arguments = $"ssh -i {KeyPath} {User}@{Domain} -p {Port}";
            // �Ƿ�ʹ�ò���ϵͳshell����
            process.StartInfo.UseShellExecute = false;
            // �Ƿ����´����������ý��̵�ֵ (����ʾ���򴰿�)
            process.StartInfo.CreateNoWindow = false;
            // ���̿�ʼ
            process.Start();
            // �ȴ�ִ�н���
            //process.WaitForExit();
            // ���̹ر�
            process.Close();
        }
        private async void EditThisConfig(CMSModel cmsModel)
        {
            // ����һ���µ�dialog����
            AddServer dialog = new AddServer(cmsModel);
            // �Դ�dialog�����������
            dialog.XamlRoot = this.XamlRoot;
            dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            dialog.PrimaryButtonText = resourceLoader.GetString("DialogChange");
            dialog.CloseButtonText = resourceLoader.GetString("DialogClose");
            // Ĭ�ϰ�ťΪPrimaryButton
            dialog.DefaultButton = ContentDialogButton.Primary;

            // ��ʾDialog���ȴ���ر�
            ContentDialogResult result = await dialog.ShowAsync();

            // ���������Primary
            if (result == ContentDialogResult.Primary)
            {
                // ʵ����SQLiteHelper
                SQLiteHelper dbHelper = new SQLiteHelper();
                // ��������
                dbHelper.UpdateData(cmsModel);
                // ���¼�������
                LoadData();
                // ȥ����
                MountInfosListView.ItemsSource = null;
                NetworkInfosListView.ItemsSource = null;
            }
        }
    }
}
