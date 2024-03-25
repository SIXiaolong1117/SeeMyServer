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
using System.Numerics;
using System.Diagnostics;

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

            // ��ȡUI�̵߳�DispatcherQueue
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            // ҳ���ʼ���󣬼�������
            LoadData();
            LoadString();

        }
        private void LoadString()
        {
            // �����߳���ִ������
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
            // ʵ����SQLiteHelper
            SQLiteHelper dbHelper = new SQLiteHelper();

            // ��ѯ����
            dataList = dbHelper.QueryData();

            // �������б�󶨵�ListView
            dataListView.ItemsSource = dataList;

            // ��ʼ��ռ��
            foreach (CMSModel cmsModel in dataList)
            {
                cmsModel.CPUUsage = "0%";
                cmsModel.MEMUsage = "0%";
                cmsModel.NETSent = "0 B/s ��";
                cmsModel.NETReceived = "0 B/s ��";
            }

            // ���������� DispatcherTimer
            timer = new DispatcherTimer();
            timer.Tick += Timer_Tick;
            // ÿ����ʱ�䴥��һ��
            timer.Interval = TimeSpan.FromSeconds(3);

            // ��ִ��һ���¼�������
            Timer_Tick(null, null);

            // ������ʱ��
            timer.Start();
        }

        // Linux ��Ϣ����
        private async Task UpdateLinuxCMSModelAsync(CMSModel cmsModel)
        {
            // �����첽����
            Task<string> cpuTask = GetLinuxCPUUsageAsync(cmsModel);
            Task<string> memTask = GetLinuxMemoryUsageAsync(cmsModel);
            Task<string> netSentTask = GetLinuxNetSentAsync(cmsModel);
            Task<string> netReceivedTask = GetLinuxNetReceivedAsync(cmsModel);

            // ͬʱִ���첽����
            await Task.WhenAll(cpuTask, memTask, netSentTask, netReceivedTask);

            // �����ȡ��������
            cmsModel.CPUUsage = cpuTask.Result;
            cmsModel.MEMUsage = memTask.Result;
            cmsModel.NETSent = netSentTask.Result;
            cmsModel.NETReceived = netReceivedTask.Result;
        }
        private async Task<string> GetLinuxCPUUsageAsync(CMSModel cmsModel)
        {
            string cpuUsageCMD = "top -bn1 | grep '^%Cpu' | sed 's/^.*://; s/,.*//; s/ *//g'";
            string cpuUsageRes = await SendSSHCommandAsync(cpuUsageCMD, cmsModel);
            int cpuUsageResValue = int.Parse(cpuUsageRes.Split('.')[0]);
            return cpuUsageResValue.ToString() + "%";
        }
        private async Task<string> GetLinuxMemoryUsageAsync(CMSModel cmsModel)
        {
            string memUsageCMD = "free -m | awk 'NR==2{printf \"%.1f\", $3/$2*100}'";
            string memUsageRes = await SendSSHCommandAsync(memUsageCMD, cmsModel);
            int memUsageResValue = int.Parse(memUsageRes.Split('.')[0]);
            return memUsageResValue.ToString() + "%";
        }
        private async Task<string> GetLinuxNetSentAsync(CMSModel cmsModel)
        {
            // ��ȡ���Ƿ�����������
            string netSentCMD = "ifconfig eth0 | grep 'RX bytes\\|TX bytes' | awk '{print $6}' | sed 's/.*bytes://'";

            // ���� Stopwatch ʵ��
            Stopwatch stopwatch = new Stopwatch();

            string result0s = await SendSSHCommandAsync(netSentCMD, cmsModel);
            // ��ʼ��ʱ
            stopwatch.Start();
            string result1s = await SendSSHCommandAsync(netSentCMD, cmsModel);
            // ֹͣ��ʱ
            stopwatch.Stop();
            // ��ȡ������ʱ��
            BigInteger elapsedTime = new BigInteger(stopwatch.ElapsedMilliseconds);

            // �������Ϊ BigInteger
            BigInteger netSentValue0s = BigInteger.Parse(result0s);
            BigInteger netSentValue1s = BigInteger.Parse(result1s);
            BigInteger netSentValue = (netSentValue1s - netSentValue0s) * 1000 / elapsedTime;
            string netSentRes;
            if (netSentValue >= (1024 * 1024 * 1024))
            {
                netSentRes = (netSentValue / 1024 / 1024 / 1024).ToString() + " GB";
            }
            else if (netSentValue >= (1024 * 1024))
            {
                netSentRes = (netSentValue / 1024 / 1024).ToString() + " MB";
            }
            else if (netSentValue >= 1024)
            {
                netSentRes = (netSentValue / 1024).ToString() + " KB";
            }
            else
            {
                netSentRes = netSentValue + " B";
            }
            return netSentRes + "/s ��";
        }
        private async Task<string> GetLinuxNetReceivedAsync(CMSModel cmsModel)
        {
            string netReceivedCMD = "ifconfig eth0 | grep 'RX bytes\\|TX bytes' | awk '{print $2}' | sed 's/.*bytes://'";

            // ���� Stopwatch ʵ��
            Stopwatch stopwatch = new Stopwatch();

            string result0s = await SendSSHCommandAsync(netReceivedCMD, cmsModel);
            // ��ʼ��ʱ
            stopwatch.Start();
            string result1s = await SendSSHCommandAsync(netReceivedCMD, cmsModel);
            // ֹͣ��ʱ
            stopwatch.Stop();
            // ��ȡ������ʱ��
            BigInteger elapsedTime = new BigInteger(stopwatch.ElapsedMilliseconds);

            // �������Ϊ BigInteger
            BigInteger netReceivedValue0s = BigInteger.Parse(result0s);
            BigInteger netReceivedValue1s = BigInteger.Parse(result1s);
            BigInteger netReceivedValue = (netReceivedValue1s - netReceivedValue0s) * 1000 / elapsedTime;
            string netReceivedRes;
            if (netReceivedValue >= 1024 * 1024 * 1024)
            {
                netReceivedRes = (netReceivedValue / 1024 / 1024 / 1024).ToString() + " GB";
            }
            else if (netReceivedValue >= 1024 * 1024)
            {
                netReceivedRes = (netReceivedValue / 1024 / 1024).ToString() + " MB";
            }
            else if (netReceivedValue >= 1024)
            {
                netReceivedRes = (netReceivedValue / 1024).ToString() + " KB";
            }
            else
            {
                netReceivedRes = netReceivedValue + " B";
            }
            return netReceivedRes + "/s ��";
        }

        // OpenWRT ��Ϣ����
        private async Task UpdateOpenWRTCMSModelAsync(CMSModel cmsModel)
        {
            // �����첽����
            Task<string> cpuTask = GetOpenWRTCPUUsageAsync(cmsModel);
            Task<string> memTask = GetOpenWRTMemoryUsageAsync(cmsModel);
            // OpenWRTҲ������ifconfig��ѯ����
            Task<string> netSentTask = GetLinuxNetSentAsync(cmsModel);
            Task<string> netReceivedTask = GetLinuxNetReceivedAsync(cmsModel);

            // ͬʱִ���첽����
            await Task.WhenAll(cpuTask, memTask, netSentTask, netReceivedTask);

            // �����ȡ��������
            cmsModel.CPUUsage = cpuTask.Result;
            cmsModel.MEMUsage = memTask.Result;
            cmsModel.NETSent = netSentTask.Result;
            cmsModel.NETReceived = netReceivedTask.Result;
        }
        private async Task<string> GetOpenWRTCPUUsageAsync(CMSModel cmsModel)
        {
            string cpuUsageCMD = "top -bn1 | head -n 3 | grep -o 'CPU:.*' | awk '{print $2}'";
            string cpuUsageRes = await SendSSHCommandAsync(cpuUsageCMD, cmsModel);
            return cpuUsageRes.TrimEnd();
        }

        private async Task<string> GetOpenWRTMemoryUsageAsync(CMSModel cmsModel)
        {
            string memUsageCMD = "top -bn1 | head -n 1 | awk '{used=$2; total=$2+$4; printf \"%.0f\", (used/total)*100}'";
            string memUsageRes = await SendSSHCommandAsync(memUsageCMD, cmsModel);
            return memUsageRes + "%";
        }

        // Windows ��Ϣ����
        private async Task UpdateWindowsCMSModelAsync(CMSModel cmsModel)
        {
            // �����첽����
            Task<string> cpuTask = GetWindowsCPUUsageAsync(cmsModel);
            Task<string> memTask = GetWindowsMemoryUsageAsync(cmsModel);
            Task<string> netSentTask = GetWindowsNetSentAsync(cmsModel);
            Task<string> netReceivedTask = GetWindowsNetReceivedAsync(cmsModel);

            // ͬʱִ���첽����
            await Task.WhenAll(cpuTask, memTask, netSentTask, netReceivedTask);

            // �����ȡ��������
            cmsModel.CPUUsage = cpuTask.Result;
            cmsModel.MEMUsage = memTask.Result;
            cmsModel.NETSent = netSentTask.Result;
            cmsModel.NETReceived = netReceivedTask.Result;
        }
        private async Task<string> GetWindowsCPUUsageAsync(CMSModel cmsModel)
        {
            string cpuUsageCMD = "powershell -Command \"(Get-Counter '\\Processor Information(_Total)\\% Processor Utility').CounterSamples.CookedValue\"";
            string cpuUsageRes = await SendSSHCommandAsync(cpuUsageCMD, cmsModel);
            int cpuUsageResValue = int.Parse(cpuUsageRes.Split('.')[0]);
            return Math.Min(Math.Max(cpuUsageResValue, 0), 100).ToString() + "%";
        }

        private async Task<string> GetWindowsMemoryUsageAsync(CMSModel cmsModel)
        {
            string memUsageCMD = "powershell -Command \"((($totalMemory = (Get-WmiObject -Class Win32_OperatingSystem).TotalVisibleMemorySize) - (Get-WmiObject -Class Win32_OperatingSystem).FreePhysicalMemory) / $totalMemory * 100)\"";
            string memUsageRes = await SendSSHCommandAsync(memUsageCMD, cmsModel);
            int memUsageResValue = int.Parse(memUsageRes.Split('.')[0]);
            return Math.Min(Math.Max(memUsageResValue, 0), 100).ToString() + "%";
        }
        private async Task<string> GetWindowsNetSentAsync(CMSModel cmsModel)
        {
            string netSentCMD = "powershell -Command \"(Get-Counter '\\Network Interface(*)\\Bytes Sent/sec').CounterSamples.CookedValue | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum\"";
            string netSentRes = await SendSSHCommandAsync(netSentCMD, cmsModel);
            // ��ȡ���������ֵ��ת��Ϊ����
            int netSentValue = int.Parse(netSentRes.Split('.')[0]);
            if (netSentValue >= (1024 * 1024 * 1024))
            {
                netSentRes = (netSentValue / 1024 / 1024 / 1024).ToString() + " GB";
            }
            else if (netSentValue >= (1024 * 1024))
            {
                netSentRes = (netSentValue / 1024 / 1024).ToString() + " MB";
            }
            else if (netSentValue >= 1024)
            {
                netSentRes = (netSentValue / 1024).ToString() + " KB";
            }
            else
            {
                netSentRes = netSentValue + " B";
            }
            return netSentRes + "/s ��";
        }

        private async Task<string> GetWindowsNetReceivedAsync(CMSModel cmsModel)
        {
            string netReceivedCMD = "powershell -Command \"(Get-Counter '\\Network Interface(*)\\Bytes Received/sec').CounterSamples.CookedValue | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum\"";
            string netReceivedRes = await SendSSHCommandAsync(netReceivedCMD, cmsModel);
            // ��ȡ���������ֵ��ת��Ϊ����
            int netReceivedValue = int.Parse(netReceivedRes.Split('.')[0]);
            if (netReceivedValue >= 1024 * 1024 * 1024)
            {
                netReceivedRes = (netReceivedValue / 1024 / 1024 / 1024).ToString() + " GB";
            }
            else if (netReceivedValue >= 1024 * 1024)
            {
                netReceivedRes = (netReceivedValue / 1024 / 1024).ToString() + " MB";
            }
            else if (netReceivedValue >= 1024)
            {
                netReceivedRes = (netReceivedValue / 1024).ToString() + " KB";
            }
            else
            {
                netReceivedRes = netReceivedValue + " B";
            }
            return netReceivedRes + "/s ��";
        }

        private async Task<string> SendSSHCommandAsync(string command, CMSModel cmsModel)
        {
            return await Task.Run(() =>
            {
                return Method.SendSSHCommand(command, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
            });
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
                    "Windows" => UpdateWindowsCMSModelAsync(cmsModel),
                    _ => Task.CompletedTask
                };

                tasks.Add(updateTask);
            }

            await Task.WhenAll(tasks);
        }

        // ���/�޸����ð�ť���
        private async void AddConfigButton_Click(object sender, RoutedEventArgs e)
        {
            // ����һ����ʼ��CMSModel����
            CMSModel initialCMSModelData = new CMSModel();

            // ����һ���µ�dialog����
            AddServer dialog = new AddServer(initialCMSModelData);
            // �Դ�dialog�����������
            dialog.XamlRoot = this.XamlRoot;
            dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            dialog.PrimaryButtonText = resourceLoader.GetString("DialogAdd");
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
                // ����������
                dbHelper.InsertData(initialCMSModelData);
                // ��������
                LoadData();
            }
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
            }
        }
        private void ConfirmDelete_Click(object sender, RoutedEventArgs e)
        {
            // �رն���ȷ��Flyout
            confirmationDelFlyout.Hide();
            // ��ȡNSModel����
            CMSModel selectedModel = (CMSModel)dataListView.SelectedItem;
            // ʵ����SQLiteHelper
            SQLiteHelper dbHelper = new SQLiteHelper();
            // ɾ������
            dbHelper.DeleteData(selectedModel);
            // ���¼�������
            LoadData();
        }
        private void CancelDelete_Click(object sender, RoutedEventArgs e)
        {
            // �رն���ȷ��Flyout
            confirmationDelFlyout.Hide();
        }
        private void OnListViewDoubleTapped(object sender, RoutedEventArgs e)
        { }
        private void OnListViewRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            // ��ȡ�Ҽ������ListViewItem
            FrameworkElement listViewItem = (sender as FrameworkElement);

            // ��ȡ�Ҽ���������ݶ���NSModel��
            CMSModel selectedItem = listViewItem?.DataContext as CMSModel;

            if (selectedItem != null)
            {

                // ���Ҽ������������Ϊѡ����
                dataListView.SelectedItem = selectedItem;
                // ����ContextMenu
                MenuFlyout menuFlyout = new MenuFlyout();

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
                    // ��������ȷ��Flyout
                    confirmationDelFlyout.ShowAt(listViewItem);
                };
                menuFlyout.Items.Add(deleteMenuItem);

                Thread.Sleep(10);

                // ��ָ��λ����ʾContextMenu
                menuFlyout.ShowAt(listViewItem, e.GetPosition(listViewItem));
            }
        }
    }
}
