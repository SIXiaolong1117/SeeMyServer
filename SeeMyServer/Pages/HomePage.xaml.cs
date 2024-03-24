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
            timer.Interval = TimeSpan.FromSeconds(5);

            // ��ִ��һ���¼�������
            Timer_Tick(null, null);

            // ������ʱ��
            timer.Start();
        }

        //private async void Timer_Tick(object sender, object e)
        //{
        //    // ֹͣ��ʱ��
        //    //timer.Stop();

        //    foreach (CMSModel cmsModel in dataList)
        //    {
        //        if (cmsModel.OSType == "Linux")
        //        {
        //            // CPU ռ��
        //            string cpuUsageCMD = "top -bn1 | grep '^%Cpu' | sed 's/^.*://; s/,.*//; s/ *//g'";
        //            string cpuUsageRes = await Task.Run(() =>
        //            {
        //                return Method.SendSSHCommand(cpuUsageCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
        //            });
        //            // ��ȡ���������ֵ��ת��Ϊ����
        //            int cpuUsageResValue = int.Parse(cpuUsageRes.Split('.')[0]);
        //            cpuUsageRes = cpuUsageResValue.ToString();

        //            // �ڴ�ռ��
        //            string memUsageCMD = "free -m | awk 'NR==2{printf \"%.1f\", $3/$2*100}'";
        //            string memUsageRes = await Task.Run(() =>
        //            {
        //                return Method.SendSSHCommand(memUsageCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
        //            });
        //            // ��ȡ���������ֵ��ת��Ϊ����
        //            int memUsageResValue = int.Parse(memUsageRes.Split('.')[0]);
        //            memUsageRes = memUsageResValue.ToString();

        //            cmsModel.CPUUsage = cpuUsageRes + "%";
        //            cmsModel.MEMUsage = memUsageRes + "%";
        //        }
        //        else if (cmsModel.OSType == "OpenWRT")
        //        {
        //            // CPU ռ��
        //            string cpuUsageCMD = "top -bn1 | head -n 3 | grep -o 'CPU:.*' | awk '{print $2}'";
        //            string cpuUsageRes = await Task.Run(() =>
        //            {
        //                return Method.SendSSHCommand(cpuUsageCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
        //            });

        //            // �ڴ�ռ��
        //            string memUsageCMD = "top -bn1 | head -n 1 | awk '{used=$2; total=$2+$4; printf \"%.0f\", (used/total)*100}'";
        //            string memUsageRes = await Task.Run(() =>
        //            {
        //                return Method.SendSSHCommand(memUsageCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
        //            });

        //            cmsModel.CPUUsage = cpuUsageRes.TrimEnd();
        //            cmsModel.MEMUsage = memUsageRes + "%";
        //        }
        //        else if (cmsModel.OSType == "Windows")
        //        {
        //            // ������ Windows Defender ʵʱ�����ľ��������� Antimalware Service ռ�øߡ�
        //            // CPU ռ�ã�Processor Utility��Ӧ�������������ҳ���CPUռ�ã�Processor Time��Ӧ���������������Ϣҳ���CPU��
        //            string cpuUsageCMD = "powershell -Command \"(Get-Counter '\\Processor Information(_Total)\\% Processor Utility').CounterSamples.CookedValue\"";
        //            string cpuUsageRes = await Task.Run(() =>
        //            {
        //                return Method.SendSSHCommand(cpuUsageCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
        //            });
        //            // ��ȡ���������ֵ��ת��Ϊ����
        //            int cpuUsageResValue = int.Parse(cpuUsageRes.Split('.')[0]);
        //            // ��ֵ������ 0 �� 100 ֮�䣬��ת�����ַ�����ʽ
        //            cpuUsageRes = Math.Min(Math.Max(cpuUsageResValue, 0), 100).ToString();

        //            // �ڴ�ռ��
        //            string memUsageCMD = "powershell -Command \"((($totalMemory = (Get-WmiObject -Class Win32_OperatingSystem).TotalVisibleMemorySize) - (Get-WmiObject -Class Win32_OperatingSystem).FreePhysicalMemory) / $totalMemory * 100)\"";
        //            string memUsageRes = await Task.Run(() =>
        //            {
        //                return Method.SendSSHCommand(memUsageCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
        //            });
        //            // ��ȡ���������ֵ��ת��Ϊ����
        //            int memUsageResValue = int.Parse(memUsageRes.Split('.')[0]);
        //            // ��ֵ������ 0 �� 100 ֮�䣬��ת�����ַ�����ʽ
        //            memUsageRes = Math.Min(Math.Max(memUsageResValue, 0), 100).ToString();

        //            // ���� ����
        //            string netSentCMD = "powershell -Command \"(Get-Counter '\\Network Interface(*)\\Bytes Sent/sec').CounterSamples.CookedValue | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum\"";
        //            string netSentRes = await Task.Run(() =>
        //            {
        //                return Method.SendSSHCommand(netSentCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
        //            });
        //            // ��ȡ���������ֵ��ת��Ϊ����
        //            int netSentValue = int.Parse(netSentRes.Split('.')[0]);
        //            if (netSentValue >= 1024)
        //            {
        //                netSentRes = (netSentValue / 1024).ToString() + " KB";
        //            }
        //            else if (netSentValue >= 1024 * 1024)
        //            {
        //                netSentRes = (netSentValue / 1024 / 1024).ToString() + " MB";
        //            }
        //            else if (netSentValue >= 1024 * 1024 * 1024)
        //            {
        //                netSentRes = (netSentValue / 1024 / 1024 / 1024).ToString() + " GB";
        //            }
        //            else
        //            {
        //                netSentRes = netSentValue + " B";
        //            }

        //            // ���� ����
        //            string netReceivedCMD = "powershell -Command \"(Get-Counter '\\Network Interface(*)\\Bytes Received/sec').CounterSamples.CookedValue | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum\"";
        //            string netReceivedRes = await Task.Run(() =>
        //            {
        //                return Method.SendSSHCommand(netReceivedCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
        //            });
        //            // ��ȡ���������ֵ��ת��Ϊ����
        //            int netReceivedValue = int.Parse(netReceivedRes.Split('.')[0]);
        //            if (netReceivedValue >= 1024)
        //            {
        //                netReceivedRes = (netReceivedValue / 1024).ToString() + " KB";
        //            }
        //            else if (netReceivedValue >= 1024 * 1024)
        //            {
        //                netReceivedRes = (netReceivedValue / 1024 / 1024).ToString() + " MB";
        //            }
        //            else if (netReceivedValue >= 1024 * 1024 * 1024)
        //            {
        //                netReceivedRes = (netReceivedValue / 1024 / 1024 / 1024).ToString() + " GB";
        //            }
        //            else
        //            {
        //                netReceivedRes = netReceivedValue + " B";
        //            }

        //            cmsModel.CPUUsage = cpuUsageRes + "%";
        //            cmsModel.MEMUsage = memUsageRes + "%";
        //            cmsModel.NETSent = netSentRes + " ��";
        //            cmsModel.NETReceived = netReceivedRes + " ��";
        //        }
        //    }

        //    // ����������ʱ��
        //    //timer.Start();
        //}

        private async Task UpdateLinuxCMSModelAsync(CMSModel cmsModel)
        {
            // CPU ռ��
            string cpuUsageCMD = "top -bn1 | grep '^%Cpu' | sed 's/^.*://; s/,.*//; s/ *//g'";
            string cpuUsageRes = await SendSSHCommandAsync(cpuUsageCMD, cmsModel);
            int cpuUsageResValue = int.Parse(cpuUsageRes.Split('.')[0]);
            cmsModel.CPUUsage = cpuUsageResValue.ToString() + "%";

            // �ڴ�ռ��
            string memUsageCMD = "free -m | awk 'NR==2{printf \"%.1f\", $3/$2*100}'";
            string memUsageRes = await SendSSHCommandAsync(memUsageCMD, cmsModel);
            int memUsageResValue = int.Parse(memUsageRes.Split('.')[0]);
            cmsModel.MEMUsage = memUsageResValue.ToString() + "%";
        }

        private async Task UpdateOpenWRTCMSModelAsync(CMSModel cmsModel)
        {
            // CPU ռ��
            string cpuUsageCMD = "top -bn1 | head -n 3 | grep -o 'CPU:.*' | awk '{print $2}'";
            string cpuUsageRes = await SendSSHCommandAsync(cpuUsageCMD, cmsModel);
            cmsModel.CPUUsage = cpuUsageRes.TrimEnd();

            // �ڴ�ռ��
            string memUsageCMD = "top -bn1 | head -n 1 | awk '{used=$2; total=$2+$4; printf \"%.0f\", (used/total)*100}'";
            string memUsageRes = await SendSSHCommandAsync(memUsageCMD, cmsModel);
            cmsModel.MEMUsage = memUsageRes + "%";
        }

        private async Task UpdateWindowsCMSModelAsync(CMSModel cmsModel)
        {
            // CPU ռ��
            string cpuUsageCMD = "powershell -Command \"(Get-Counter '\\Processor Information(_Total)\\% Processor Utility').CounterSamples.CookedValue\"";
            string cpuUsageRes = await SendSSHCommandAsync(cpuUsageCMD, cmsModel);
            int cpuUsageResValue = int.Parse(cpuUsageRes.Split('.')[0]);
            cmsModel.CPUUsage = Math.Min(Math.Max(cpuUsageResValue, 0), 100).ToString() + "%";

            // �ڴ�ռ��
            string memUsageCMD = "powershell -Command \"((($totalMemory = (Get-WmiObject -Class Win32_OperatingSystem).TotalVisibleMemorySize) - (Get-WmiObject -Class Win32_OperatingSystem).FreePhysicalMemory) / $totalMemory * 100)\"";
            string memUsageRes = await SendSSHCommandAsync(memUsageCMD, cmsModel);
            int memUsageResValue = int.Parse(memUsageRes.Split('.')[0]);
            cmsModel.MEMUsage = Math.Min(Math.Max(memUsageResValue, 0), 100).ToString() + "%";

            // ���� ����
            string netSentCMD = "powershell -Command \"(Get-Counter '\\Network Interface(*)\\Bytes Sent/sec').CounterSamples.CookedValue | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum\"";
            string netSentRes = await Task.Run(() =>
            {
                return Method.SendSSHCommand(netSentCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
            });
            // ��ȡ���������ֵ��ת��Ϊ����
            int netSentValue = int.Parse(netSentRes.Split('.')[0]);
            if (netSentValue >= 1024)
            {
                netSentRes = (netSentValue / 1024).ToString() + " KB";
            }
            else if (netSentValue >= 1024 * 1024)
            {
                netSentRes = (netSentValue / 1024 / 1024).ToString() + " MB";
            }
            else if (netSentValue >= 1024 * 1024 * 1024)
            {
                netSentRes = (netSentValue / 1024 / 1024 / 1024).ToString() + " GB";
            }
            else
            {
                netSentRes = netSentValue + " B";
            }
            cmsModel.NETSent = netSentRes + "/s ��";

            // ���� ����
            string netReceivedCMD = "powershell -Command \"(Get-Counter '\\Network Interface(*)\\Bytes Received/sec').CounterSamples.CookedValue | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum\"";
            string netReceivedRes = await Task.Run(() =>
            {
                return Method.SendSSHCommand(netReceivedCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
            });
            // ��ȡ���������ֵ��ת��Ϊ����
            int netReceivedValue = int.Parse(netReceivedRes.Split('.')[0]);
            if (netReceivedValue >= 1024)
            {
                netReceivedRes = (netReceivedValue / 1024).ToString() + " KB";
            }
            else if (netReceivedValue >= 1024 * 1024)
            {
                netReceivedRes = (netReceivedValue / 1024 / 1024).ToString() + " MB";
            }
            else if (netReceivedValue >= 1024 * 1024 * 1024)
            {
                netReceivedRes = (netReceivedValue / 1024 / 1024 / 1024).ToString() + " GB";
            }
            else
            {
                netReceivedRes = netReceivedValue + " B";
            }
            cmsModel.NETReceived = netReceivedRes + "/s ��";
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
