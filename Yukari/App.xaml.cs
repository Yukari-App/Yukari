using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Yukari.Services;
using Yukari.Views;

namespace Yukari
{
    public partial class App : Application
    {
        private static Window? MainWindow;

        public App()
        {
            InitializeComponent();
            Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "en-US"; // Change the default language of the application
        }


        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // Load Main Window
            MainWindow = new Window
            {
                SystemBackdrop = new MicaBackdrop(),
                ExtendsContentIntoTitleBar = true,
                Title = "Yukari",
                Content = new MainPage()
            };

            MainWindow.AppWindow.SetIcon("Assets/AppIcon.ico");

            var win32WindowService = new Win32WindowService(MainWindow);
            win32WindowService.SetWindowMinMaxSize(new Win32WindowService.POINT() { x = 600, y = 500 });

            MainWindow.Activate();
        }
    }
}
