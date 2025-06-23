using SimpleNoteNG.Data;
using SimpleNoteNG.Models;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;
using System.Drawing;
using System.Net.Mail;

namespace SimpleNoteNG.Windows
{
    public partial class SignUpWindow : Window
    {
        private bool _isFirstValidationAttempt = true;

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

        private bool ValidateFields()
        {
            bool isValid = true;

            // Валидация имени пользователя
            if (string.IsNullOrWhiteSpace(txtUsername.Text))
            {
                ShowError(txtUsername, "Username is required");
                isValid = false;
            }
            else if (txtUsername.Text.Length < 3)
            {
                ShowError(txtUsername, "Minimum 3 characters");
                isValid = false;
            }
            else
            {
                ClearError(txtUsername);
            }

            // Валидация email
            if (string.IsNullOrWhiteSpace(txtEmail.Text))
            {
                ShowError(txtEmail, "Email is required");
                isValid = false;
            }
            else if (!IsValidEmail(txtEmail.Text))
            {
                ShowError(txtEmail, "Invalid email format");
                isValid = false;
            }
            else
            {
                ClearError(txtEmail);
            }

            // Валидация пароля
            if (string.IsNullOrWhiteSpace(pswPassword.Password))
            {
                ShowError(pswPassword, "Password is required");
                isValid = false;
            }
            else if (pswPassword.Password.Length < 6)
            {
                ShowError(pswPassword, "Minimum 6 characters");
                isValid = false;
            }
            else
            {
                ClearError(pswPassword);
            }

            // Подтверждение пароля
            if (string.IsNullOrWhiteSpace(pswConfirmPassword.Password))
            {
                ShowError(pswConfirmPassword, "Confirm password");
                isValid = false;
            }
            else if (pswPassword.Password != pswConfirmPassword.Password)
            {
                ShowError(pswConfirmPassword, "Passwords do not match");
                isValid = false;
            }
            else
            {
                ClearError(pswConfirmPassword);
            }

            return isValid;
        }
        private void ShowError(Control control, string message)
        {
            // Подсвечиваем поле с ошибкой
            var border = (Border)control.Template.FindName("border", control);
            if (border != null)
            {
                border.BorderBrush = System.Windows.Media.Brushes.Red;
                border.BorderThickness = new Thickness(2);
            }

            // Показываем текст ошибки
            var errorTextBlock = (TextBlock)this.FindName(control.Name + "Error");
            if (errorTextBlock != null)
            {
                errorTextBlock.Text = message;
                errorTextBlock.Visibility = Visibility.Visible;
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private void SetError(Control control, string message)
        {
            var border = (Border)control.Template.FindName("border", control);
            if (border != null)
            {
                border.BorderBrush = System.Windows.Media.Brushes.Red;
                border.BorderThickness = new Thickness(2);
            }

            var errorTextBlock = (TextBlock)this.FindName(control.Name + "Error");
            if (errorTextBlock != null)
            {
                errorTextBlock.Text = message;
                errorTextBlock.Visibility = Visibility.Visible;
            }
        }

        private void ClearError(Control control)
        {
            // Убираем подсветку ошибки
            var border = (Border)control.Template.FindName("border", control);
            if (border != null)
            {
                border.BorderBrush = System.Windows.Media.Brushes.Gray;
                border.BorderThickness = new Thickness(1);
            }

            // Скрываем текст ошибки
            var errorTextBlock = (TextBlock)this.FindName(control.Name + "Error");
            if (errorTextBlock != null)
            {
                errorTextBlock.Visibility = Visibility.Collapsed;
            }
        }

        private async void btnSignUp_Click(object sender, RoutedEventArgs e)
        {
            // Запускаем валидацию
            bool isValid = ValidateFields();

            // Если есть ошибки - останавливаем
            if (!isValid)
                return;

            // Если всё ок - продолжаем регистрацию
            btnSignUp.IsEnabled = false;

            string username = txtUsername.Text.Trim();
            string email = txtEmail.Text.Trim();
            string password = pswPassword.Password;

            using (var context = new AppDbContext())
            {
                // Проверка существующего имени пользователя
                if (context.Users.Any(u => u.Username == username))
                {
                    ShowError(txtUsername, "Username is already taken");
                    btnSignUp.IsEnabled = true;
                    return;
                }

                // Проверка существующего email
                if (context.Users.Any(u => u.Email == email))
                {
                    ShowError(txtEmail, "Email is already registered");
                    btnSignUp.IsEnabled = true;
                    return;
                }

                // Хеширование пароля
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

                // Создание пользователя
                var newUser = new User
                {
                    Username = username,
                    Email = email,
                    PasswordHash = hashedPassword,
                    Role = "User",
                    EmailConfirmed = false
                };

                // Сохранение в БД
                context.Users.Add(newUser);
                await context.SaveChangesAsync();

                // Открытие окна подтверждения
                ConfirmEmail confirmWin = new ConfirmEmail(username, email, newUser.UserId);
                this.Close();
                confirmWin.Show();
            }
        }
    }
}