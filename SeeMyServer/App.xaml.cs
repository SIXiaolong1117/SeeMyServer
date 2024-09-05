using Microsoft.UI.Xaml;
using SeeMyServer.Helper;
using System;

namespace SeeMyServer
{
    public partial class App : Application
    {
        // 静态属性，保存 MainWindow 实例，提供全局访问
        public static MainWindow MainWindow => m_window;

        // 私有的 m_window，用于引用 MainWindow 实例
        public static MainWindow m_window;

        // 日志记录器
        private Logger logger;

        public App()
        {
            this.InitializeComponent();

            // 设置日志，最大1MB
            logger = new Logger(1);
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // 创建 MainWindow 实例并赋值给 m_window
            m_window = new MainWindow();

            // 获取窗口句柄
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(m_window);

            // 设置窗口大小（如果需要，取消注释）
            // SetWindowSize(hwnd, 1110, 800);

            // 记录启动日志
            logger.LogInfo("See My Server starts.");

            // 激活窗口
            m_window.Activate();
        }

        // 调整窗口大小方法
        private void SetWindowSize(IntPtr hwnd, int width, int height)
        {
            // 获取 DPI 缩放比例
            var dpi = PInvoke.User32.GetDpiForWindow(hwnd);
            float scalingFactor = (float)dpi / 96;

            // 根据缩放比例调整窗口尺寸
            width = (int)(width * scalingFactor);
            height = (int)(height * scalingFactor);

            // 设置窗口大小，不移动窗口位置
            PInvoke.User32.SetWindowPos(
                hwnd,
                PInvoke.User32.SpecialWindowHandles.HWND_TOP,
                0, 0, width, height,
                PInvoke.User32.SetWindowPosFlags.SWP_NOMOVE
            );
        }
    }

}
