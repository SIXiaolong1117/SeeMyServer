using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;

namespace SeeMyServer.Pages
{
    public sealed partial class About : Page
    {
        public About()
        {
            this.InitializeComponent();

            // �ڹ��캯���������ʵ�λ�����ð汾��
            var package = Package.Current;
            var version = package.Id.Version;

            APPVersion.Text = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
    }
}
