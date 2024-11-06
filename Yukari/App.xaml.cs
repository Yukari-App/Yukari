using Microsoft.UI.Xaml;

namespace Yukari
{
    public partial class App : Application
    {
        private MainWindow? MainWindow;

        public App() =>
            InitializeComponent();
        
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // Carrega a janela principal
            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
    }
}
