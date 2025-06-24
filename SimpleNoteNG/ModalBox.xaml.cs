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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SimpleNoteNG
{
    /// <summary>
    /// Логика взаимодействия для ModalBox.xaml
    /// </summary>
    public partial class ModalBox : Window
    {
        public ModalBox(Window window, string message)
        {
            InitializeComponent();
            Owner = window;
            MessageText.Text = message;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Создаем и запускаем анимацию
            var storyboard = new Storyboard();

            var opacityAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(opacityAnim, this);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(OpacityProperty));

            var marginAnim = new ThicknessAnimation
            {
                From = new Thickness(0, 20, 0, 0),
                To = new Thickness(0),
                Duration = TimeSpan.FromSeconds(0.4),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(marginAnim, (Border)Content);
            Storyboard.SetTargetProperty(marginAnim, new PropertyPath(MarginProperty));

            storyboard.Children.Add(opacityAnim);
            storyboard.Children.Add(marginAnim);
            storyboard.Begin();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Анимация закрытия
            var storyboard = new Storyboard();

            var opacityAnim = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromSeconds(0.2)
            };
            Storyboard.SetTarget(opacityAnim, this);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(OpacityProperty));

            storyboard.Children.Add(opacityAnim);
            storyboard.Completed += (s, args) => this.DialogResult = true;
            storyboard.Begin();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
