using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using WinRT;
using System;
using Microsoft.UI.Xaml.Navigation;
using SeeMyServer.Pages;
using System.Security.Cryptography;
using SeeMyServer.Methods;
using SeeMyServer.Models;

namespace SeeMyServer
{
    public sealed partial class MainWindow : Window
    {
        WindowsSystemDispatcherQueueHelper m_wsdqHelper; // See below for implementation.
        DesktopAcrylicController a_backdropController;
        MicaController m_backdropController;
        MicaController ma_backdropController;
        SystemBackdropConfiguration m_configurationSource;
        ResourceLoader resourceLoader = new ResourceLoader();
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        public MainWindow()
        {
            this.InitializeComponent();

            // 启动个性化TitleBar
            ExtendsContentIntoTitleBar = true;
            // 将UI设置为TitleBar
            SetTitleBar(AppTitleBar);
            // 设置任务栏显示名称
            Title = resourceLoader.GetString("AppTitle");

            TrySetSystemBackdrop();

            NavView.SelectedItem = NavView.MenuItems[0];

            AppTitleTextBlock.Text = resourceLoader.GetString("AppTitle");
        }
        bool TrySetSystemBackdrop()
        {
            if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

                // Create the policy object.
                m_configurationSource = new SystemBackdropConfiguration();
                this.Activated += Window_Activated;
                this.Closed += Window_Closed;
                ((FrameworkElement)this.Content).ActualThemeChanged += Window_ThemeChanged;

                // Initial configuration state.
                m_configurationSource.IsInputActive = true;
                SetConfigurationSourceTheme();

                if (localSettings.Values["materialStatus"] as string == "Acrylic")
                {
                    a_backdropController = new Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController();
                }
                else if (localSettings.Values["materialStatus"] as string == "Mica")
                {
                    m_backdropController = new Microsoft.UI.Composition.SystemBackdrops.MicaController();
                }
                else
                {
                    ma_backdropController = new Microsoft.UI.Composition.SystemBackdrops.MicaController()
                    {
                        Kind = MicaKind.BaseAlt
                    };
                }

                // Enable the system backdrop.
                // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
                if (localSettings.Values["materialStatus"] as string == "Mica")
                {
                    m_backdropController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                    m_backdropController.SetSystemBackdropConfiguration(m_configurationSource);
                }
                else if (localSettings.Values["materialStatus"] as string == "Mica Alt")
                {
                    ma_backdropController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                    ma_backdropController.SetSystemBackdropConfiguration(m_configurationSource);
                }
                else if (localSettings.Values["materialStatus"] as string == "Acrylic")
                {
                    a_backdropController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                    a_backdropController.SetSystemBackdropConfiguration(m_configurationSource);
                }
                else
                //throw new Exception($"Invalid argument: {localSettings.Values["materialStatus"] as string}");
                {
                    localSettings.Values["materialStatus"] = "Mica Alt";
                    ma_backdropController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                    ma_backdropController.SetSystemBackdropConfiguration(m_configurationSource);
                }
                return true; // succeeded
            }

            return false; // Mica is not supported on this system
        }
        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            m_configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Make sure any Mica/Acrylic controller is disposed
            // so it doesn't try to use this closed window.
            if (localSettings.Values["materialStatus"] as string == "Acrylic")
            {
                if (a_backdropController != null)
                {
                    a_backdropController.Dispose();
                    a_backdropController = null;
                }
            }
            else if (localSettings.Values["materialStatus"] as string == "Mica")
            {
                if (m_backdropController != null)
                {
                    m_backdropController.Dispose();
                    m_backdropController = null;
                }
            }
            else
            {
                if (ma_backdropController != null)
                {
                    ma_backdropController.Dispose();
                    ma_backdropController = null;
                }
            }
            this.Activated -= Window_Activated;
            m_configurationSource = null;
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            if (m_configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
        }

        private void SetConfigurationSourceTheme()
        {
            switch (((FrameworkElement)this.Content).ActualTheme)
            {
                case ElementTheme.Dark: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Dark; break;
                case ElementTheme.Light: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Light; break;
                case ElementTheme.Default: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Default; break;
            }
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                contentFrame.Navigate(typeof(SettingsPage));
            }
            else
            {
                var selectedItem = args.SelectedItem as NavigationViewItem;
                if (selectedItem != null)
                {
                    switch (selectedItem.Tag.ToString())
                    {
                        case "HomePage":
                            contentFrame.Navigate(typeof(HomePage));
                            break;
                        case "About":
                            contentFrame.Navigate(typeof(About));
                            break;
                    }
                }
            }
        }
        public void NavigateToPage(Type pageType, CMSModel cmsModel)
        {
            contentFrame.Navigate(pageType, cmsModel);
            // 更新选中的导航项
            var selectedItem = FindSelectedItemByTag(contentFrame.CurrentSourcePageType.Name);
            if (selectedItem != null)
            {
                NavView.SelectedItem = selectedItem;
            }
            else
            {
                NavView.SelectedItem = null;
            }
        }
        private void BackButton_Click(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            contentFrame.Navigate(typeof(HomePage));
            // 更新选中的导航项
            var selectedItem = FindSelectedItemByTag(contentFrame.CurrentSourcePageType.Name);
            if (selectedItem != null)
            {
                NavView.SelectedItem = selectedItem;
            }
            else
            {
                NavView.SelectedItem = null;
            }
        }
        private NavigationViewItem FindSelectedItemByTag(string tag)
        {
            foreach (NavigationViewItem item in NavView.MenuItems)
            {
                if (item.Tag.ToString() == tag)
                {
                    return item;
                }
            }

            foreach (NavigationViewItem item in NavView.FooterMenuItems)
            {
                if (item.Tag.ToString() == tag)
                {
                    return item;
                }
            }

            return null;
        }
    }

    class WindowsSystemDispatcherQueueHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        struct DispatcherQueueOptions
        {
            internal int dwSize;
            internal int threadType;
            internal int apartmentType;
        }

        [DllImport("CoreMessaging.dll")]
        private static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object dispatcherQueueController);

        object m_dispatcherQueueController = null;
        public void EnsureWindowsSystemDispatcherQueueController()
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
            {
                // one already exists, so we'll just use it.
                return;
            }

            if (m_dispatcherQueueController == null)
            {
                DispatcherQueueOptions options;
                options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
                options.threadType = 2;    // DQTYPE_THREAD_CURRENT
                options.apartmentType = 2; // DQTAT_COM_STA

                CreateDispatcherQueueController(options, ref m_dispatcherQueueController);
            }
        }
    }
}
