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
        }

        private List<CMSModel> dataList;

        private void LoadData()
        {
            // ʵ����SQLiteHelper
            SQLiteHelper dbHelper = new SQLiteHelper();

            // ��ѯ����
            dataList = dbHelper.QueryData();

            // ��ʼ��ռ��
            foreach (CMSModel cmsModel in dataList)
            {
                cmsModel.CPUUsage = "0%";
                cmsModel.MEMUsage = "0%";
            }

            // �������б�󶨵�ListView
            dataListView.ItemsSource = dataList;

            // ���������� DispatcherTimer
            timer = new DispatcherTimer();
            // ÿ����ʱ�䴥��һ��
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;

            // ������ʱ��
            timer.Start();
        }

        private async void Timer_Tick(object sender, object e)
        {
            // ֹͣ��ʱ��
            //timer.Stop();

            foreach (CMSModel cmsModel in dataList)
            {
                if (cmsModel.OSType == "Windows")
                {
                    // ������ Windows Defender ʵʱ�����ľ��������� Antimalware Service ռ�øߡ�
                    // CPU ռ�ã�Processor Utility��Ӧ�������������ҳ���CPUռ�ã�Processor Time��Ӧ���������������Ϣҳ���CPU��
                    string cpuUsageCMD = "powershell (Get-Counter '\\Processor Information(_Total)\\% Processor Utility').CounterSamples.CookedValue";
                    string cpuUsageRes = await Task.Run(() =>
                    {
                        return Method.SendSSHCommand(cpuUsageCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
                    });
                    // ��ȡ���������ֵ��ת��Ϊ����
                    int cpuUsageResValue = int.Parse(cpuUsageRes.Split('.')[0]);
                    // ��ֵ������ 0 �� 100 ֮�䣬��ת�����ַ�����ʽ
                    cpuUsageRes = Math.Min(Math.Max(cpuUsageResValue, 0), 100).ToString();

                    // �ڴ�ռ��
                    string memUsageCMD = "powershell ((($totalMemory = (Get-WmiObject -Class Win32_OperatingSystem).TotalVisibleMemorySize) - (Get-WmiObject -Class Win32_OperatingSystem).FreePhysicalMemory) / $totalMemory * 100)";
                    string memUsageRes = await Task.Run(() =>
                    {
                        return Method.SendSSHCommand(memUsageCMD, cmsModel.HostIP, cmsModel.HostPort, cmsModel.SSHUser, "sshPasswd", cmsModel.SSHKey, "True");
                    });
                    //// ��ȡ���������ֵ��ת��Ϊ����
                    int memUsageResValue = int.Parse(memUsageRes.Split('.')[0]);
                    //// ��ֵ������ 0 �� 100 ֮�䣬��ת�����ַ�����ʽ
                    memUsageRes = Math.Min(Math.Max(memUsageResValue, 0), 100).ToString();


                    cmsModel.CPUUsage = cpuUsageRes + "%";
                    cmsModel.MEMUsage = memUsageRes + "%";
                }
            }

            // ����������ʱ��
            //timer.Start();
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
    }
}
