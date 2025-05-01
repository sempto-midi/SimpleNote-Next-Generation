using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SimpleNoteNG.Data;

namespace SimpleNoteNG.Windows
{
    /// <summary>
    /// Логика взаимодействия для ConfirmEmail.xaml
    /// </summary>
    public partial class ConfirmEmail : Window
    {
        private string _username;
        private string _email;
        private string _confirmationCode;
        private int _userId;
        private bool _isCodeValidating = false;

        public ConfirmEmail(string username, string email, int userId)
        {
            InitializeComponent();
            _username = username;
            _email = email;
            _userId = userId;
            FirstSymb.Focus();

            if (!IsValidEmail(_email))
            {
                System.Windows.MessageBox.Show("Некорректный формат email");
                this.Close();
                return;
            }

            GenerateAndSendConfirmationCode();
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var mailAddress = new MailAddress(email);
                return mailAddress.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private string GenerateConfirmationCode()
        {
            return new Random().Next(10000, 99999).ToString();
        }

        private void GenerateAndSendConfirmationCode()
        {
            try
            {
                _confirmationCode = GenerateConfirmationCode();
                SendEmail();
            }
            catch (SmtpException ex)
            {
                System.Windows.MessageBox.Show($"Error sending an email ({ex.Message})");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message);
            }
        }

        private void SendEmail()
        {
            using (SmtpClient smtp = new SmtpClient("smtp.mail.ru", 587))
            {
                smtp.EnableSsl = true;
                smtp.Credentials = new NetworkCredential("s1mplenote@mail.ru", "1KRapgDfzreMdhkgQbgk");
                smtp.Timeout = 10000;

                using (MailMessage msg = new MailMessage())
                {
                    msg.From = new MailAddress("s1mplenote@mail.ru");
                    msg.To.Add(_email);
                    msg.Subject = "Код подтверждения SimpleNote";
                    msg.IsBodyHtml = true;
                    msg.Body = CreateEmailBody();

                    smtp.Send(msg);
                }
            }
        }

        private string CreateEmailBody()
        {
            return $@"
                <html>
                <body style='font-family: Kode-Mono; background: #393939; color: white; padding: 20px;'>
                    <div style='max-width: 600px; margin: 0 auto; background: #252525; padding: 30px; border-radius: 10px;'>
                        <h2 style='color: #CF6E00;'>Здравствуйте, {_username}!</h2>
                        <p>U'r confirmation code:</p>
                        <div style='font-size: 24px; padding: 15px; background: #393939; 
                                    border-radius: 5px; margin: 20px 0; justify-content: center;'>
                            {_confirmationCode}
                        </div>
                    </div>
                </body>
                </html>";
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            ValidateCode();
        }

        private void ValidateCode()
        {
            if (_isCodeValidating) return;

            string enteredCode = $"{FirstSymb.Text}{SecondSymb.Text}" +
                                $"{ThirdSymb.Text}{FourthSymb.Text}{FifthSymb.Text}";

            // Блокируем все TextBox'ы на время анимации
            foreach (var box in new[] { FirstSymb, SecondSymb, ThirdSymb, FourthSymb, FifthSymb })
            {
                box.IsEnabled = false;
            }

            if (enteredCode == _confirmationCode)
            {
                PlaySuccessAnimation();
            }
            else
            {
                PlayErrorAnimation();
            }
        }

        private void PlaySuccessAnimation()
        {
            var storyboard = new Storyboard();
            TimeSpan currentTime = TimeSpan.Zero;
            TimeSpan duration = TimeSpan.FromSeconds(0.2);
            TimeSpan delayBetween = TimeSpan.FromSeconds(0.1);

            // Анимация подпрыгивания и изменения цвета
            foreach (var box in new[] { FirstSymb, SecondSymb, ThirdSymb, FourthSymb, FifthSymb })
            {
                // Анимация подпрыгивания
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

                // Анимация цвета
                var colorAnimation = new ColorAnimation
                {
                    To = (Color)FindResource("SuccessColor"),
                    Duration = duration,
                    BeginTime = currentTime + duration,
                    FillBehavior = FillBehavior.HoldEnd
                };
                Storyboard.SetTarget(colorAnimation, box);
                Storyboard.SetTargetProperty(colorAnimation, new PropertyPath("Foreground.(SolidColorBrush.Color)"));
                storyboard.Children.Add(colorAnimation);

                currentTime += duration + delayBetween;
            }

            storyboard.Completed += async (s, e) =>
            {
                await Task.Delay(500);

                try
                {
                    using (var db = new AppDbContext())
                    {
                        var user = db.Users.Find(_userId);
                        if (user != null)
                        {
                            user.EmailConfirmed = true;
                            db.SaveChanges();
                        }
                    }

                    Dispatcher.Invoke(() =>
                    {
                        ProjectsWindow projWin = new ProjectsWindow(_username, _userId);
                        projWin.Show();
                        this.Close();
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show(ex.Message);
                        ResetTextBoxes();
                    });
                }
            };

            // Инициализация трансформаций для анимации
            foreach (var box in new[] { FirstSymb, SecondSymb, ThirdSymb, FourthSymb, FifthSymb })
            {
                box.RenderTransform = new TranslateTransform();
            }

            storyboard.Begin();
        }

        private void PlayErrorAnimation()
        {
            var storyboard = new Storyboard();
            TimeSpan currentTime = TimeSpan.Zero;
            TimeSpan duration = TimeSpan.FromSeconds(0.1);
            TimeSpan delayBetween = TimeSpan.FromSeconds(0.05);

            // Анимация подпрыгивания и изменения цвета
            foreach (var box in new[] { FirstSymb, SecondSymb, ThirdSymb, FourthSymb, FifthSymb })
            {
                // Анимация подпрыгивания
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

                // Анимация цвета
                var colorAnimation = new ColorAnimation
                {
                    To = (Color)FindResource("ErrorColor"),
                    Duration = duration,
                    BeginTime = currentTime + duration,
                    FillBehavior = FillBehavior.HoldEnd
                };
                Storyboard.SetTarget(colorAnimation, box);
                Storyboard.SetTargetProperty(colorAnimation, new PropertyPath("Foreground.(SolidColorBrush.Color)"));
                storyboard.Children.Add(colorAnimation);

                // Анимация тряски (влево-вправо)
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

            storyboard.Completed += (s, e) =>
            {
                // Возвращаем все в исходное состояние
                var resetStoryboard = new Storyboard();

                // Анимация возврата цвета
                foreach (var box in new[] { FirstSymb, SecondSymb, ThirdSymb, FourthSymb, FifthSymb })
                {
                    var colorAnimation = new ColorAnimation
                    {
                        To = Colors.White,
                        Duration = TimeSpan.FromSeconds(0.3),
                        FillBehavior = FillBehavior.HoldEnd
                    };
                    Storyboard.SetTarget(colorAnimation, box);
                    Storyboard.SetTargetProperty(colorAnimation, new PropertyPath("Foreground.(SolidColorBrush.Color)"));
                    resetStoryboard.Children.Add(colorAnimation);

                    // Возврат позиции
                    var positionAnimation = new DoubleAnimation
                    {
                        To = 0,
                        Duration = TimeSpan.FromSeconds(0.1),
                        FillBehavior = FillBehavior.HoldEnd
                    };
                    Storyboard.SetTarget(positionAnimation, box);
                    Storyboard.SetTargetProperty(positionAnimation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
                    resetStoryboard.Children.Add(positionAnimation);
                }

                resetStoryboard.Completed += (se, ev) =>
                {
                    ResetTextBoxes();
                    _isCodeValidating = false;
                };

                resetStoryboard.Begin();
            };

            // Инициализация трансформаций для анимации
            foreach (var box in new[] { FirstSymb, SecondSymb, ThirdSymb, FourthSymb, FifthSymb })
            {
                box.RenderTransform = new TranslateTransform();
            }

            storyboard.Begin();
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
                box.IsEnabled = true;
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

        private void CodeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var currentBox = (System.Windows.Controls.TextBox)sender;
            var currentTag = int.Parse(currentBox.Tag.ToString());

            // Если текст был удален - перейти на предыдущий TextBox
            if (string.IsNullOrEmpty(currentBox.Text) && currentTag > 1)
            {
                var previousBox = FindName(GetTextBoxName(currentTag - 1)) as System.Windows.Controls.TextBox;
                previousBox?.Focus();
                return;
            }

            // Если текст введен - перейти на следующий TextBox
            if (!string.IsNullOrEmpty(currentBox.Text) && currentTag < 5)
            {
                var nextBox = FindName(GetTextBoxName(currentTag + 1)) as System.Windows.Controls.TextBox;
                nextBox?.Focus();
            }

            // Проверить, заполнены ли все поля
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
            var textBox = (System.Windows.Controls.TextBox)sender;
            textBox.SelectAll();
        }

        private void CodeTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем только цифры
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
            MainWindow win = new MainWindow();
            win.Show();
            this.Close();
        }
    }
}
