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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace SimpleNoteNG.Windows
{
    /// <summary>
    /// Логика взаимодействия для SignUpWindow.xaml
    /// </summary>
    public partial class SignUpWindow : Window
    {
        public SignUpWindow()
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

        private void btnSignUp_Click(object sender, RoutedEventArgs e)
        {
            // Получаем значения из полей ввода
            string username = txtUsername.Text.Trim();
            string email = txtEmail.Text.Trim();
            string password = pswPassword.Password;
            string confirmPassword = pswConfirmPassword.Password;
            int userId;

            // Проверка на пустые поля
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) ||
                string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
            {
                MessageBox.Show("Please fill in all fields.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Проверка подтверждения пароля
            if (password != confirmPassword)
            {
                MessageBox.Show("Passwords do not match.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using (var context = new AppDbContext())
            {
                // Проверка существования пользователя с таким же именем или email
                if (context.Users.Any(u => u.Username == username || u.Email == email))
                {
                    MessageBox.Show("Username or email already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Хэшируем пароль
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
                // Создаем нового пользователя
                var newUser = new User
                {
                    Username = username,
                    Email = email,
                    PasswordHash = hashedPassword,
                    Role = "User" // По умолчанию роль "User"

                };

                // Добавляем пользователя в базу данных
                context.Users.Add(newUser);
                context.SaveChanges();
                userId = newUser.UserId;

                // Открываем окно подтверждения email (или другое действие после успешной регистрации)
                ConfirmEmail confemWin = new ConfirmEmail(username, email, userId);
                this.Close();
                confemWin.Show();
            }
        }
    }
}
