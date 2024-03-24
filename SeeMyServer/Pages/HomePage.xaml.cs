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

            // 创建并配置 DispatcherTimer
            timer = new DispatcherTimer();
            timer.Tick += Timer_Tick;
            // 每隔段时间触发一次
            timer.Interval = TimeSpan.FromSeconds(5);

            // 先执行一次事件处理方法
            Timer_Tick(null, null);

            // 启动计时器
            timer.Start();
        }

        //private async void Timer_Tick(object sender, object e)
        //{
        //    // 停止计时器
        //    //timer.Stop();

        //    foreach (CMSModel cmsModel in dataList)
        //    {
        //        if (cmsModel.OSType == "Linux")
        //        {
        //            // CPU 占用
        //            string cpuUsageCMD = "top -bn1 | grep '^%Cpu' | sed 's/^.*://; s/,.*//; s/ *//g'";
        //            string cpuUsageRes = await Task.Run(() =>
        //            {
        //                return Method.SendSSHCommand(cpuUsageCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
        //            });
        //            // 获取命令输出的值并转换为整数
        //            int cpuUsageResValue = int.Parse(cpuUsageRes.Split('.')[0]);
        //            cpuUsageRes = cpuUsageResValue.ToString();

        //            // 内存占用
        //            string memUsageCMD = "free -m | awk 'NR==2{printf \"%.1f\", $3/$2*100}'";
        //            string memUsageRes = await Task.Run(() =>
        //            {
        //                return Method.SendSSHCommand(memUsageCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
        //            });
        //            // 获取命令输出的值并转换为整数
        //            int memUsageResValue = int.Parse(memUsageRes.Split('.')[0]);
        //            memUsageRes = memUsageResValue.ToString();

        //            cmsModel.CPUUsage = cpuUsageRes + "%";
        //            cmsModel.MEMUsage = memUsageRes + "%";
        //        }
        //        else if (cmsModel.OSType == "OpenWRT")
        //        {
        //            // CPU 占用
        //            string cpuUsageCMD = "top -bn1 | head -n 3 | grep -o 'CPU:.*' | awk '{print $2}'";
        //            string cpuUsageRes = await Task.Run(() =>
        //            {
        //                return Method.SendSSHCommand(cpuUsageCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
        //            });

        //            // 内存占用
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
        //            // 会引起 Windows Defender 实时保护的警觉，导致 Antimalware Service 占用高。
        //            // CPU 占用（Processor Utility对应任务管理器性能页面的CPU占用，Processor Time对应任务管理器详情信息页面的CPU）
        //            string cpuUsageCMD = "powershell -Command \"(Get-Counter '\\Processor Information(_Total)\\% Processor Utility').CounterSamples.CookedValue\"";
        //            string cpuUsageRes = await Task.Run(() =>
        //            {
        //                return Method.SendSSHCommand(cpuUsageCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
        //            });
        //            // 获取命令输出的值并转换为整数
        //            int cpuUsageResValue = int.Parse(cpuUsageRes.Split('.')[0]);
        //            // 将值限制在 0 到 100 之间，并转换回字符串形式
        //            cpuUsageRes = Math.Min(Math.Max(cpuUsageResValue, 0), 100).ToString();

        //            // 内存占用
        //            string memUsageCMD = "powershell -Command \"((($totalMemory = (Get-WmiObject -Class Win32_OperatingSystem).TotalVisibleMemorySize) - (Get-WmiObject -Class Win32_OperatingSystem).FreePhysicalMemory) / $totalMemory * 100)\"";
        //            string memUsageRes = await Task.Run(() =>
        //            {
        //                return Method.SendSSHCommand(memUsageCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
        //            });
        //            // 获取命令输出的值并转换为整数
        //            int memUsageResValue = int.Parse(memUsageRes.Split('.')[0]);
        //            // 将值限制在 0 到 100 之间，并转换回字符串形式
        //            memUsageRes = Math.Min(Math.Max(memUsageResValue, 0), 100).ToString();

        //            // 网络 发送
        //            string netSentCMD = "powershell -Command \"(Get-Counter '\\Network Interface(*)\\Bytes Sent/sec').CounterSamples.CookedValue | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum\"";
        //            string netSentRes = await Task.Run(() =>
        //            {
        //                return Method.SendSSHCommand(netSentCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
        //            });
        //            // 获取命令输出的值并转换为整数
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

        //            // 网络 接收
        //            string netReceivedCMD = "powershell -Command \"(Get-Counter '\\Network Interface(*)\\Bytes Received/sec').CounterSamples.CookedValue | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum\"";
        //            string netReceivedRes = await Task.Run(() =>
        //            {
        //                return Method.SendSSHCommand(netReceivedCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
        //            });
        //            // 获取命令输出的值并转换为整数
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
        //            cmsModel.NETSent = netSentRes + " ↑";
        //            cmsModel.NETReceived = netReceivedRes + " ↓";
        //        }
        //    }

        //    // 重新启动计时器
        //    //timer.Start();
        //}

        private async Task UpdateLinuxCMSModelAsync(CMSModel cmsModel)
        {
            // CPU 占用
            string cpuUsageCMD = "top -bn1 | grep '^%Cpu' | sed 's/^.*://; s/,.*//; s/ *//g'";
            string cpuUsageRes = await SendSSHCommandAsync(cpuUsageCMD, cmsModel);
            int cpuUsageResValue = int.Parse(cpuUsageRes.Split('.')[0]);
            cmsModel.CPUUsage = cpuUsageResValue.ToString() + "%";

            // 内存占用
            string memUsageCMD = "free -m | awk 'NR==2{printf \"%.1f\", $3/$2*100}'";
            string memUsageRes = await SendSSHCommandAsync(memUsageCMD, cmsModel);
            int memUsageResValue = int.Parse(memUsageRes.Split('.')[0]);
            cmsModel.MEMUsage = memUsageResValue.ToString() + "%";
        }

        private async Task UpdateOpenWRTCMSModelAsync(CMSModel cmsModel)
        {
            // CPU 占用
            string cpuUsageCMD = "top -bn1 | head -n 3 | grep -o 'CPU:.*' | awk '{print $2}'";
            string cpuUsageRes = await SendSSHCommandAsync(cpuUsageCMD, cmsModel);
            cmsModel.CPUUsage = cpuUsageRes.TrimEnd();

            // 内存占用
            string memUsageCMD = "top -bn1 | head -n 1 | awk '{used=$2; total=$2+$4; printf \"%.0f\", (used/total)*100}'";
            string memUsageRes = await SendSSHCommandAsync(memUsageCMD, cmsModel);
            cmsModel.MEMUsage = memUsageRes + "%";
        }

        private async Task UpdateWindowsCMSModelAsync(CMSModel cmsModel)
        {
            // CPU 占用
            string cpuUsageCMD = "powershell -Command \"(Get-Counter '\\Processor Information(_Total)\\% Processor Utility').CounterSamples.CookedValue\"";
            string cpuUsageRes = await SendSSHCommandAsync(cpuUsageCMD, cmsModel);
            int cpuUsageResValue = int.Parse(cpuUsageRes.Split('.')[0]);
            cmsModel.CPUUsage = Math.Min(Math.Max(cpuUsageResValue, 0), 100).ToString() + "%";

            // 内存占用
            string memUsageCMD = "powershell -Command \"((($totalMemory = (Get-WmiObject -Class Win32_OperatingSystem).TotalVisibleMemorySize) - (Get-WmiObject -Class Win32_OperatingSystem).FreePhysicalMemory) / $totalMemory * 100)\"";
            string memUsageRes = await SendSSHCommandAsync(memUsageCMD, cmsModel);
            int memUsageResValue = int.Parse(memUsageRes.Split('.')[0]);
            cmsModel.MEMUsage = Math.Min(Math.Max(memUsageResValue, 0), 100).ToString() + "%";

            // 网络 发送
            string netSentCMD = "powershell -Command \"(Get-Counter '\\Network Interface(*)\\Bytes Sent/sec').CounterSamples.CookedValue | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum\"";
            string netSentRes = await Task.Run(() =>
            {
                return Method.SendSSHCommand(netSentCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
            });
            // 获取命令输出的值并转换为整数
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
            cmsModel.NETSent = netSentRes + "/s ↑";

            // 网络 接收
            string netReceivedCMD = "powershell -Command \"(Get-Counter '\\Network Interface(*)\\Bytes Received/sec').CounterSamples.CookedValue | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum\"";
            string netReceivedRes = await Task.Run(() =>
            {
                return Method.SendSSHCommand(netReceivedCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
            });
            // 获取命令输出的值并转换为整数
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
            cmsModel.NETReceived = netReceivedRes + "/s ↓";
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
        }
        private void CancelDelete_Click(object sender, RoutedEventArgs e)
        {
            // 关闭二次确认Flyout
            confirmationDelFlyout.Hide();
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

                Thread.Sleep(10);

                // 在指定位置显示ContextMenu
                menuFlyout.ShowAt(listViewItem, e.GetPosition(listViewItem));
            }
        }
    }
}
