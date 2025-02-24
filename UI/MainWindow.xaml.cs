using System.Windows;

namespace UI
{

    public partial class MainWindow : Window
    {
        MainWindow init;

        public MainWindow()
        {
            InitializeComponent();
            frame.Navigate(new Pages.Login(this));
        }
    }
}
