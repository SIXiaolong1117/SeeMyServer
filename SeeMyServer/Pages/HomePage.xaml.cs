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

            // �������б�󶨵�ListView
            dataListView.ItemsSource = dataList;

            // ���������� DispatcherTimer
            timer = new DispatcherTimer();
            // ÿ����ʱ�䴥��һ��
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;

            // ������ʱ��
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

                // ��ȡ���������ֵ��ת��Ϊ����
                int value = int.Parse(res.Split('.')[0]);
                // ��ֵ������ 0 �� 100 ֮�䣬��ת�����ַ�����ʽ
                res = Math.Min(Math.Max(value, 0), 100).ToString();
                cmsModel.CPUUsage = res + "%";
            }
        }

        //private void LoadData()
        //{
        //    // �����߳���ִ������
        //    Thread subThread = new Thread(new ThreadStart(() =>
        //    {
        //        while (true)
        //        {
        //            // ��UI�߳��ϸ���
        //            _dispatcherQueue.TryEnqueue(() =>
        //            {

        //                // ʵ����SQLiteHelper
        //                SQLiteHelper dbHelper = new SQLiteHelper();

        //                // ��ѯ����
        //                List<CMSModel> dataList = dbHelper.QueryData();

        //                // �������б�󶨵�ListView
        //                dataListView.ItemsSource = dataList;

        //            });

        //            // �ӳ�1s
        //            Thread.Sleep(1000);
        //        }
        //    }));
        //    subThread.Start();
        //}

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
        private void SSHSendThread(List<CMSModel> dataList)
        {
            foreach (CMSModel cmsModel in dataList)
            {
                // �����߳���ִ������
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
