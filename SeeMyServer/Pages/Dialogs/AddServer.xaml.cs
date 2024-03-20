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

            // 将Dialog两个按钮点击事件绑定
            PrimaryButtonClick += MyDialog_PrimaryButtonClick;
            SecondaryButtonClick += MyDialog_SecondaryButtonClick;

            // 初始化Dialog中的字段，使用传入的CMSModel对象的属性
            CMSData = cmsModel;
            DisplayNameTextBox.Text = cmsModel.Name;
            HostIPTextBox.Text = cmsModel.HostIP;
            HostPortTextBox.Text = cmsModel.HostPort;
            SSHUserTextBox.Text = cmsModel.SSHUser;
            SSHKeyTextBox.Text = cmsModel.SSHKey;
            OSTypeTextBox.Text = cmsModel.OSType;
        }

        private void MyDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // 在"确定"按钮点击事件中保存用户输入的内容
            CMSData.Name = string.IsNullOrEmpty(DisplayNameTextBox.Text) ? "<unnamed>" : DisplayNameTextBox.Text;
            CMSData.HostIP = HostIPTextBox.Text;
            CMSData.HostPort = HostPortTextBox.Text;
            CMSData.SSHUser = SSHUserTextBox.Text;
            CMSData.SSHKey = SSHKeyTextBox.Text;
            CMSData.OSType = OSTypeTextBox.Text;
        }

        private void MyDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // 在"取消"按钮点击事件中不做任何操作
        }
    }
}
