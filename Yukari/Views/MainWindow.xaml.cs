using Microsoft.UI.Xaml;

namespace Yukari.Views
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            AppWindow.SetIcon(@"Assets\AppIcon.ico");

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
        }
    }
}
