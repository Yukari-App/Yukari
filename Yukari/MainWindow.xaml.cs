using Microsoft.UI.Xaml;

namespace Yukari
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow() =>
            InitializeComponent();

        private void myButton_Click(object sender, RoutedEventArgs e)
        {
            myButton.Content = "Clicked";
        }
    }
}
