using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SeeMyServer.Helper;
using SeeMyServer.Methods;
using SeeMyServer.Models;
using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Windows.Storage;
using Windows.Storage.Pickers;
using System.IO;


namespace SeeMyServer.Pages.Dialogs
{
    public sealed partial class AddServer : ContentDialog
    {
        // 启用本地设置数据
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        public CMSModel CMSData { get; private set; }
        public CMSModel IncomingData { get; private set; }
        private Logger logger;
        public AddServer(CMSModel cmsModel)
        {
            this.InitializeComponent();

            // 设置日志，最大1MB
            logger = new Logger(1);

            // 将Dialog两个按钮点击事件绑定
            PrimaryButtonClick += MyDialog_PrimaryButtonClick;
            SecondaryButtonClick += MyDialog_SecondaryButtonClick;

            // 初始化Dialog中的字段，使用传入的CMSModel对象的属性
            CMSData = cmsModel;
            DisplayNameTextBox.Text = cmsModel.Name;
            HostIPTextBox.Text = cmsModel.HostIP;
            HostPortTextBox.Text = cmsModel.HostPort;
            SSHUserTextBox.Text = cmsModel.SSHUser;
            if (cmsModel.SSHKeyIsOpen == "True")
            {
                SSHKeyOrPasswdToggleSwitch.IsOn = true;
            }
            else
            {
                SSHKeyOrPasswdToggleSwitch.IsOn = false;
            }
            if (cmsModel.SSHPasswd != null && cmsModel.SSHPasswd != "")
            {
                SSHPasswd.PlaceholderText = "<Not Changed>";
            }
            logger.LogInfo("Dialog field initialization completed.");

            if (cmsModel.SSHKey == "" || cmsModel.SSHKey == null)
            {
                string userFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string logFolder = Path.Combine(userFolderPath, ".ssh");
                string logFilePath = Path.Combine(logFolder, "id_rsa");
                SSHKeyTextBox.Text = logFilePath;
            }
            else
            {
                SSHKeyTextBox.Text = cmsModel.SSHKey;
            }

            // 添加操作系统类型
            OSTypeComboBox.Items.Add("Linux");

            if (cmsModel.OSType == null)
            {
                OSTypeComboBox.SelectedItem = "Linux";
            }
            else
            {
                OSTypeComboBox.SelectedItem = cmsModel.OSType;
            }

            // 刷新Key Auth状态
            PrivateKeyIsOpen();
        }

        private void MyDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // 在"确定"按钮点击事件中保存用户输入的内容
            CMSData.Name = string.IsNullOrEmpty(DisplayNameTextBox.Text) ? "<Unnamed>" : DisplayNameTextBox.Text;
            CMSData.HostIP = HostIPTextBox.Text;
            CMSData.HostPort = HostPortTextBox.Text;
            CMSData.SSHUser = SSHUserTextBox.Text;
            CMSData.OSType = GetSelectedComboBoxItemAsString(OSTypeComboBox);

            // 根据Key Auth状态写入
            if (SSHKeyOrPasswdToggleSwitch.IsOn == true)
            {
                CMSData.SSHKeyIsOpen = "True";
                CMSData.SSHKey = SSHKeyTextBox.Text;
                CMSData.SSHPasswd = null;
            }
            else
            {
                if (SSHPasswd.Password != "" && SSHPasswd.Password != null)
                {
                    CMSData.SSHKeyIsOpen = "False";

                    // 检查是否已经存在密钥和初始化向量，如果不存在则生成新的
                    string key = Method.LoadKeyFromLocalSettings();
                    string iv = Method.LoadIVFromLocalSettings();

                    // 如果不存在密钥和初始化向量，则生成新的
                    if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(iv))
                    {
                        key = Method.GenerateRandomKey();
                        iv = Method.GenerateRandomIV();

                        // 将新生成的密钥和初始化向量保存到 localSettings 中
                        Method.SaveKeyToLocalSettings(key);
                        Method.SaveIVToLocalSettings(iv);
                    }

                    // 使用的对称加密算法
                    SymmetricAlgorithm symmetricAlgorithm = new AesManaged();

                    // 设置加密密钥和初始化向量
                    symmetricAlgorithm.Key = Convert.FromBase64String(key);
                    symmetricAlgorithm.IV = Convert.FromBase64String(iv);

                    // 加密字符串
                    string encrypted = Method.EncryptString(SSHPasswd.Password, symmetricAlgorithm);

                    CMSData.SSHPasswd = encrypted;
                    CMSData.SSHKey = null;
                }
            }
        }

        // 获取选中内容并转换为字符串
        private string GetSelectedComboBoxItemAsString(ComboBox comboBox)
        {
            if (comboBox.SelectedItem != null)
            {
                // 直接返回选中项作为字符串
                return comboBox.SelectedItem.ToString();
            }
            // 如果没有选中项，则返回空字符串
            return "<Unknown OS>";
        }

        private void MyDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // 在"取消"按钮点击事件中不做任何操作
        }
        private async void SelectSSHKeyPath_Click(object sender, RoutedEventArgs e)
        {
            // 创建一个FileOpenPicker
            var openPicker = new FileOpenPicker();
            // 获取当前窗口句柄 (HWND) 
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.m_window);
            // 使用窗口句柄 (HWND) 初始化FileOpenPicker
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

            // 为FilePicker设置选项
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            // 建议打开位置 桌面
            openPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            // 文件类型过滤器
            openPicker.FileTypeFilter.Add("*");

            // 打开选择器供用户选择文件
            var file = await openPicker.PickSingleFileAsync();
            string filePath = null;
            if (file != null)
            {
                filePath = file.Path;
            }
            else
            {
                filePath = null;
            }
            SSHKeyTextBox.Text = filePath;
        }
        private void privateKeyIsOpen_Toggled(object sender, RoutedEventArgs e)
        {
            PrivateKeyIsOpen();
        }
        private void PrivateKeyIsOpen()
        {
            if (SSHKeyOrPasswdToggleSwitch.IsOn == true)
            {
                AddSSHKey.Visibility = Visibility.Visible;
                AddSSHPasswd.Visibility = Visibility.Collapsed;
                SSHKeyTips.Visibility = Visibility.Visible;
                SSHPasswdTips.Visibility = Visibility.Collapsed;
            }
            else
            {
                AddSSHKey.Visibility = Visibility.Collapsed;
                AddSSHPasswd.Visibility = Visibility.Visible;
                SSHKeyTips.Visibility = Visibility.Collapsed;
                SSHPasswdTips.Visibility = Visibility.Visible;
            }
            logger.LogInfo("PrivateKeyIsOpen() completed.");
        }
    }
}