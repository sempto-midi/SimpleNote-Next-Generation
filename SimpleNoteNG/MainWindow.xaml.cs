using SimpleNoteNG.Windows;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Windows;

namespace SimpleNoteNG
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            SignInWindow win = new SignInWindow();
            win.Show();
            this.Close();
        }
        private void btnRegister_Click(object sender, RoutedEventArgs e)
        {
            SignUpWindow win = new SignUpWindow();
            win.Show();
            this.Close();
        }

        private void qrGitBook_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://app.gitbook.com/o/ldEHZ5SUJol8mlVNKFle/s/zN70XyLamoy7hiQe5CVY/") { UseShellExecute = true });

        }

        private void qrGitHub_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/sempto-midi") { UseShellExecute = true });
        }
    }
}