using SimpleNoteNG.Data;
using SimpleNoteNG.Models;
using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Threading.Tasks;

namespace SimpleNoteNG.Windows
{
    public partial class ForgotPasswordWindow : Window
    {
        private string _confirmationCode;
        private string _username;
        private string _email;
        private int _userId;
        private bool _isCodeValidated = false;

        public ForgotPasswordWindow(string username, string email, int userId)
        {
            InitializeComponent();

            _username = username;
            _email = email;
            _userId = userId;

            FirstSymb.Focus();

            // Автоматически отправляем код при открытии окна
            SendConfirmationCode();
        }

        private async void SendConfirmationCode()
        {
            _confirmationCode = GenerateConfirmationCode();

            try
            {
                await SendEmail(_email, _confirmationCode);
                var msg = new ModalBox(this, $"Confirmation code sent to {_email}");
                msg.ShowDialog();
            }
            catch (Exception ex)
            {
                var msg = new ModalBox(this, $"Error sending email: {ex.Message}");
                msg.ShowDialog();
                this.Close();
            }
        }

        private string GenerateConfirmationCode()
        {
            return new Random().Next(10000, 99999).ToString();
        }

        private async Task SendEmail(string email, string code)
        {
            await Task.Run(() =>
            {
                using (SmtpClient smtp = new SmtpClient("smtp.mail.ru", 587))
                {
                    smtp.EnableSsl = true;
                    smtp.Credentials = new NetworkCredential("s1mplenote@mail.ru", "1KRapgDfzreMdhkgQbgk");
                    smtp.Timeout = 10000;

                    using (MailMessage msg = new MailMessage())
                    {
                        msg.From = new MailAddress("s1mplenote@mail.ru");
                        msg.To.Add(email);
                        msg.Subject = "Password Recovery Code - SimpleNote";
                        msg.IsBodyHtml = true;
                        msg.Body = $@"
                            <html>
                            <body style='font-family: Kode-Mono; background: #393939; color: white; padding: 20px;'>
                                <div style='max-width: 600px; margin: 0 auto; background: #252525; padding: 30px; border-radius: 10px;'>
                                    <h2 style='color: #CF6E00;'>Password Recovery</h2>
                                    <p>Your confirmation code:</p>
                                    <div style='font-size: 24px; padding: 15px; background: #393939; 
                                                border-radius: 5px; margin: 20px 0; justify-content: center;'>
                                        {code}
                                    </div>
                                    <p>If you didn't request this code, please ignore this email.</p>
                                </div>
                            </body>
                            </html>";

                        smtp.Send(msg);
                    }
                }
            });
        }

        private void ValidateCode()
        {
            string enteredCode = $"{FirstSymb.Text}{SecondSymb.Text}{ThirdSymb.Text}{FourthSymb.Text}{FifthSymb.Text}";

            if (enteredCode == _confirmationCode)
            {
                _isCodeValidated = true;
                PlaySuccessAnimation();
            }
            else
            {
                PlayErrorAnimation();
            }
        }

        private async void PlaySuccessAnimation()
        {
            var storyboard = new Storyboard();
            TimeSpan currentTime = TimeSpan.Zero;
            TimeSpan duration = TimeSpan.FromSeconds(0.2);
            TimeSpan delayBetween = TimeSpan.FromSeconds(0.1);

            foreach (var box in new[] { FirstSymb, SecondSymb, ThirdSymb, FourthSymb, FifthSymb })
            {
                var bounceAnimation = new DoubleAnimation
                {
                    To = -10,
                    Duration = duration,
                    BeginTime = currentTime,
                    AutoReverse = true
                };
                Storyboard.SetTarget(bounceAnimation, box);
                Storyboard.SetTargetProperty(bounceAnimation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
                storyboard.Children.Add(bounceAnimation);

                var colorAnimation = new ColorAnimation
                {
                    To = Colors.LimeGreen,
                    Duration = duration,
                    BeginTime = currentTime + duration,
                    FillBehavior = FillBehavior.HoldEnd
                };
                Storyboard.SetTarget(colorAnimation, box);
                Storyboard.SetTargetProperty(colorAnimation, new PropertyPath("Foreground.(SolidColorBrush.Color)"));
                storyboard.Children.Add(colorAnimation);

                currentTime += duration + delayBetween;
            }

            foreach (var box in new[] { FirstSymb, SecondSymb, ThirdSymb, FourthSymb, FifthSymb })
            {
                box.RenderTransform = new TranslateTransform();
            }

            storyboard.Begin();

            // Ждем завершения анимации
            await Task.Delay(1000);

            // Скрываем панель с кодом и показываем панель с паролем
            CodePanel.Visibility = Visibility.Collapsed;
            PasswordPanel.Visibility = Visibility.Visible;
        }

        private async void PlayErrorAnimation()
        {
            var storyboard = new Storyboard();
            TimeSpan currentTime = TimeSpan.Zero;
            TimeSpan duration = TimeSpan.FromSeconds(0.1);
            TimeSpan delayBetween = TimeSpan.FromSeconds(0.05);

            foreach (var box in new[] { FirstSymb, SecondSymb, ThirdSymb, FourthSymb, FifthSymb })
            {
                var bounceAnimation = new DoubleAnimation
                {
                    To = -5,
                    Duration = duration,
                    BeginTime = currentTime,
                    AutoReverse = true
                };
                Storyboard.SetTarget(bounceAnimation, box);
                Storyboard.SetTargetProperty(bounceAnimation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
                storyboard.Children.Add(bounceAnimation);

                var colorAnimation = new ColorAnimation
                {
                    To = Colors.Red,
                    Duration = duration,
                    BeginTime = currentTime + duration,
                    FillBehavior = FillBehavior.HoldEnd
                };
                Storyboard.SetTarget(colorAnimation, box);
                Storyboard.SetTargetProperty(colorAnimation, new PropertyPath("Foreground.(SolidColorBrush.Color)"));
                storyboard.Children.Add(colorAnimation);

                for (int i = 0; i < 3; i++)
                {
                    var shakeRight = new DoubleAnimation
                    {
                        To = 5,
                        Duration = TimeSpan.FromSeconds(0.05),
                        BeginTime = currentTime + duration * 2 + TimeSpan.FromSeconds(0.05 * i * 2)
                    };
                    var shakeLeft = new DoubleAnimation
                    {
                        To = -5,
                        Duration = TimeSpan.FromSeconds(0.05),
                        BeginTime = currentTime + duration * 2 + TimeSpan.FromSeconds(0.05 * (i * 2 + 1))
                    };

                    Storyboard.SetTarget(shakeRight, box);
                    Storyboard.SetTargetProperty(shakeRight, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
                    storyboard.Children.Add(shakeRight);

                    Storyboard.SetTarget(shakeLeft, box);
                    Storyboard.SetTargetProperty(shakeLeft, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
                    storyboard.Children.Add(shakeLeft);
                }

                currentTime += duration + delayBetween;
            }

            foreach (var box in new[] { FirstSymb, SecondSymb, ThirdSymb, FourthSymb, FifthSymb })
            {
                box.RenderTransform = new TranslateTransform();
            }

            storyboard.Begin();

            await Task.Delay(1000);
            ResetTextBoxes();
        }

        private void ResetTextBoxes()
        {
            FirstSymb.Text = "";
            SecondSymb.Text = "";
            ThirdSymb.Text = "";
            FourthSymb.Text = "";
            FifthSymb.Text = "";

            foreach (var box in new[] { FirstSymb, SecondSymb, ThirdSymb, FourthSymb, FifthSymb })
            {
                box.Foreground = new SolidColorBrush(Colors.White);
                var transform = box.RenderTransform as TranslateTransform;
                if (transform != null)
                {
                    transform.X = 0;
                    transform.Y = 0;
                }
            }

            FirstSymb.Focus();
        }

        private void btnResetPassword_Click(object sender, RoutedEventArgs e)
        {
            if (!_isCodeValidated)
            {
                ValidateCode();
                return;
            }

            string newPassword = pswNewPassword.Password;
            string confirmPassword = pswConfirmPassword.Password;

            if (string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            {
                var msg = new ModalBox(this, "Please enter and confirm your new password");
                msg.ShowDialog();
                return;
            }

            if (newPassword != confirmPassword)
            {
                var msg = new ModalBox(this, "Passwords do not match");
                msg.ShowDialog();
                return;
            }

            try
            {
                using (var context = new AppDbContext())
                {
                    var user = context.Users.Find(_userId);
                    if (user != null)
                    {
                        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                        context.SaveChanges();

                        var msg = new ModalBox(this, "Password has been reset successfully");
                        msg.ShowDialog();

                        var signInWindow = new SignInWindow();
                        signInWindow.Show();
                        this.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                var msg = new ModalBox(this, $"Error: {ex.Message}");
                msg.ShowDialog();
            }
        }

        private void CodeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var currentBox = (TextBox)sender;
            var currentTag = int.Parse(currentBox.Tag.ToString());

            if (string.IsNullOrEmpty(currentBox.Text) && currentTag > 1)
            {
                var previousBox = FindName(GetTextBoxName(currentTag - 1)) as TextBox;
                previousBox?.Focus();
                return;
            }

            if (!string.IsNullOrEmpty(currentBox.Text) && currentTag < 5)
            {
                var nextBox = FindName(GetTextBoxName(currentTag + 1)) as TextBox;
                nextBox?.Focus();
            }

            CheckAllFieldsFilled();
        }

        private void CheckAllFieldsFilled()
        {
            bool allFilled = !string.IsNullOrEmpty(FirstSymb.Text) &&
                            !string.IsNullOrEmpty(SecondSymb.Text) &&
                            !string.IsNullOrEmpty(ThirdSymb.Text) &&
                            !string.IsNullOrEmpty(FourthSymb.Text) &&
                            !string.IsNullOrEmpty(FifthSymb.Text);

            if (allFilled)
            {
                ValidateCode();
            }
        }

        private string GetTextBoxName(int tag)
        {
            return tag switch
            {
                1 => "FirstSymb",
                2 => "SecondSymb",
                3 => "ThirdSymb",
                4 => "FourthSymb",
                5 => "FifthSymb",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private void CodeTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = (TextBox)sender;
            textBox.SelectAll();
        }

        private void CodeTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!char.IsDigit(e.Text, 0))
            {
                e.Handled = true;
            }
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
            var signInWindow = new SignInWindow();
            signInWindow.Show();
            this.Close();
        }
    }
}