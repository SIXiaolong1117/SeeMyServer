using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SeeMyServer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;


namespace SeeMyServer.Pages.Dialogs
{
    public sealed partial class AddServer : ContentDialog
    {
        public CMSModel CMSData { get; private set; }
        public AddServer(CMSModel cmsModel)
        {
            this.InitializeComponent();

            // ��Dialog������ť����¼���
            PrimaryButtonClick += MyDialog_PrimaryButtonClick;
            SecondaryButtonClick += MyDialog_SecondaryButtonClick;

            // ��ʼ��Dialog�е��ֶΣ�ʹ�ô����CMSModel���������
            CMSData = cmsModel;
            DisplayNameTextBox.Text = cmsModel.Name;
            HostIPTextBox.Text = cmsModel.HostIP;
            HostPortTextBox.Text = cmsModel.HostPort;
            SSHUserTextBox.Text = cmsModel.SSHUser;
            SSHKeyTextBox.Text = cmsModel.SSHKey;

            // ��Ӳ���ϵͳ����
            OSTypeComboBox.Items.Add("Windows");
            OSTypeComboBox.Items.Add("Linux");            
            OSTypeComboBox.Items.Add("OpenWRT");
            OSTypeComboBox.SelectedItem = cmsModel.OSType;
        }

        private void MyDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // ��"ȷ��"��ť����¼��б����û����������
            CMSData.Name = string.IsNullOrEmpty(DisplayNameTextBox.Text) ? "<Unnamed>" : DisplayNameTextBox.Text;
            CMSData.HostIP = HostIPTextBox.Text;
            CMSData.HostPort = HostPortTextBox.Text;
            CMSData.SSHUser = SSHUserTextBox.Text;
            CMSData.SSHKey = SSHKeyTextBox.Text;
            CMSData.OSType = GetSelectedComboBoxItemAsString(OSTypeComboBox);
        }

        // ��ȡѡ�����ݲ�ת��Ϊ�ַ���
        private string GetSelectedComboBoxItemAsString(ComboBox comboBox)
        {
            if (comboBox.SelectedItem != null)
            {
                // ֱ�ӷ���ѡ������Ϊ�ַ���
                return comboBox.SelectedItem.ToString();
            }
            return "<Unknown OS>"; // ���û��ѡ����򷵻ؿ��ַ���
        }

        private void MyDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // ��"ȡ��"��ť����¼��в����κβ���
        }
    }
}
