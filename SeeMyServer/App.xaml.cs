using Microsoft.UI.Xaml;
using SeeMyServer.Helper;
using System;

namespace SeeMyServer
{
    public partial class App : Application
    {
        private Logger logger;
        public App()
        {
            this.InitializeComponent();
            
            // 设置日志，最大1MB
            logger = new Logger(1);
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();

            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(m_window);
            SetWindowSize(hwnd, 1110, 800);

            m_window.Activate();

            logger.LogInfo("See My Server starts.");
        }

        private void SetWindowSize(IntPtr hwnd, int width, int height)
        {
            var dpi = PInvoke.User32.GetDpiForWindow(hwnd);
            float scalingFactor = (float)dpi / 96;
            width = (int)(width * scalingFactor);
            height = (int)(height * scalingFactor);

            PInvoke.User32.SetWindowPos(hwnd, PInvoke.User32.SpecialWindowHandles.HWND_TOP,
                                        0, 0, width, height,
                                        PInvoke.User32.SetWindowPosFlags.SWP_NOMOVE);
        }
        public static MainWindow m_window;
    }
}
