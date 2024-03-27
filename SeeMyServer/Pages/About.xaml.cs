using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;

namespace SeeMyServer.Pages
{
    public sealed partial class About : Page
    {
        public About()
        {
            this.InitializeComponent();

            // 在构造函数或其他适当位置设置版本号
            var package = Package.Current;
            var version = package.Id.Version;

            APPVersion.Text = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
    }
}
