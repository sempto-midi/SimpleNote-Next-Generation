using SimpleNoteNG.Data;
using SimpleNoteNG.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace SimpleNoteNG.Windows
{
    /// <summary>
    /// Логика взаимодействия для SignInWindow.xaml
    /// </summary>
    public partial class SignInWindow : Window
    {
        public SignInWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            MainWindow win = new MainWindow();
            win.Show();
            this.Close();
        }

        private void btnSignIn_Click(object sender, RoutedEventArgs e)
        {
            // Получаем значения из полей ввода
            string usernameOrEmail = txtUsername.Text.Trim();
            string password = pswPassword.Password;

            // Проверка на пустые поля
            if (string.IsNullOrEmpty(usernameOrEmail) || string.IsNullOrEmpty(password))
            {
                var msg = new ModalBox(this, "fill in all fields");
                msg.ShowDialog();
                //MessageBox.Show("fill in all fields", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                //return;
            }

            using (var context = new AppDbContext())
            {
                // Поиск пользователя по имени пользователя или email
                var user = context.Users.FirstOrDefault(u => u.Username == usernameOrEmail || u.Email == usernameOrEmail);

                if (user == null)
                {
                    MessageBox.Show("User not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Проверка пароля
                if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    MessageBox.Show("Incorrect password.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Открываем окно проектов
                ProjectsWindow projWin = new ProjectsWindow(user.Username, user.UserId);
                this.Close();
                projWin.Show();
            }
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();

            if (string.IsNullOrEmpty(username))
            {
                var msg = new ModalBox(this, "Please enter your username first");
                msg.ShowDialog();
                return;
            }

            using (var context = new AppDbContext())
            {
                var user = context.Users.FirstOrDefault(u => u.Username == username);
                if (user == null)
                {
                    var msg = new ModalBox(this, "User not found");
                    msg.ShowDialog();
                    return;
                }

                var forgotPasswordWindow = new ForgotPasswordWindow(user.Username, user.Email, user.UserId);
                forgotPasswordWindow.Show();
                this.Close();
            }
        }
    }
}
