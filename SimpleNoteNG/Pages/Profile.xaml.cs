﻿using SimpleNoteNG.Data;
using SimpleNoteNG.Models;
using SimpleNoteNG.Converters;
using SimpleNoteNG.Windows;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace SimpleNoteNG.Pages
{
    public partial class Profile : Page
    {
        private int _userId;
        private bool _isEditing = false;
        private bool _isChangingPassword = false;
        private string _originalUsername;
        private string _originalEmail;

        public string Username { get; set; }
        public string Email { get; set; }
        public bool EmailConfirmed { get; set; }

        public Profile(int userId, string username, string email, bool emailConfirmed)
        {
            InitializeComponent();
            _userId = userId;
            Username = username;
            Email = email;
            EmailConfirmed = emailConfirmed;
            _originalUsername = username;
            _originalEmail = email;

            DataContext = this;
            UpdateEmailStatusText();
        }

        private void UpdateEmailStatusText()
        {
            EmailStatusText.Text = EmailConfirmed ? "(confirmed)" : "(not confirmed)";
        }

        private void CloseProfile_Click(object sender, RoutedEventArgs e)
        {
            var parentWindow = Window.GetWindow(this) as ProjectsWindow;
            parentWindow?.HideProfile();
        }

        private void EditSaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isEditing)
            {
                // Переход в режим редактирования
                _isEditing = true;
                ViewModePanel.Visibility = Visibility.Collapsed;
                EditModePanel.Visibility = Visibility.Visible;
                EditSaveButton.Content = "Save";
                _isChangingPassword = false;
            }
            else
            {
                // Сохранение изменений
                if (ValidateChanges())
                {
                    SaveChanges();
                    _isEditing = false;
                    ViewModePanel.Visibility = Visibility.Visible;
                    EditModePanel.Visibility = Visibility.Collapsed;
                    EditSaveButton.Content = Image.SourceProperty;
                }
            }
        }

        private bool ValidateChanges()
        {
            // Проверка имени пользователя
            if (string.IsNullOrWhiteSpace(UsernameEditBox.Text))
            {
                MessageBox.Show("Username cannot be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // Проверка email
            if (string.IsNullOrWhiteSpace(EmailEditBox.Text) || !EmailEditBox.Text.Contains("@"))
            {
                MessageBox.Show("Please enter a valid email address", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private void SaveChanges()
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    var user = db.Users.FirstOrDefault(u => u.UserId == _userId);
                    if (user != null)
                    {
                        // Обновляем имя пользователя, если оно изменилось
                        if (user.Username != UsernameEditBox.Text)
                        {
                            user.Username = UsernameEditBox.Text;
                            Username = user.Username;
                            UsernameText.Text = Username;
                        }

                        // Обновляем email, если он изменился
                        if (user.Email != EmailEditBox.Text)
                        {
                            user.Email = EmailEditBox.Text;
                            user.EmailConfirmed = false; // Сбрасываем подтверждение при изменении email
                            Email = user.Email;
                            EmailConfirmed = user.EmailConfirmed;
                            EmailText.Text = Email;
                            UpdateEmailStatusText();
                        }

                        db.SaveChanges();
                        MessageBox.Show("Changes saved successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving changes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to delete your account? All your projects will be lost.",
                "Confirm Deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var parentWindow = Window.GetWindow(this) as ProjectsWindow;
                parentWindow?.DeleteAccount();
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to logout?",
                "Confirm Logout",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var parentWindow = Window.GetWindow(this) as ProjectsWindow;
                parentWindow?.Logout();
            }
        }
    }

    // Конвертер для цвета статуса email
    public class EmailStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? Brushes.LightGreen : Brushes.OrangeRed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}