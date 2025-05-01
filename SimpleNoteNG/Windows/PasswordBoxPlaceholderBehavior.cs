using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;

namespace SimpleNoteNG.Windows
{
    public class PasswordBoxPlaceholderBehavior : Behavior<PasswordBox>
    {
        protected override void OnAttached()
        {
            base.OnAttached();

            // Wait for the template to be applied
            if (AssociatedObject.IsLoaded)
            {
                HookEvents();
            }
            else
            {
                AssociatedObject.Loaded += OnLoaded;
            }
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            UnhookEvents();
            AssociatedObject.Loaded -= OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            AssociatedObject.Loaded -= OnLoaded;
            HookEvents();
        }

        private void HookEvents()
        {
            AssociatedObject.PasswordChanged += OnPasswordChanged;
            AssociatedObject.GotFocus += OnGotFocus;
            AssociatedObject.LostFocus += OnLostFocus;
            UpdatePlaceholderVisibility();
        }

        private void UnhookEvents()
        {
            AssociatedObject.PasswordChanged -= OnPasswordChanged;
            AssociatedObject.GotFocus -= OnGotFocus;
            AssociatedObject.LostFocus -= OnLostFocus;
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdatePlaceholderVisibility();
        }

        private void OnGotFocus(object sender, RoutedEventArgs e)
        {
            UpdatePlaceholderVisibility();
        }

        private void OnLostFocus(object sender, RoutedEventArgs e)
        {
            UpdatePlaceholderVisibility();
        }

        private void UpdatePlaceholderVisibility()
        {
            // Ensure template is applied
            if (AssociatedObject.Template == null) return;

            if (AssociatedObject.Template.FindName("placeholderText", AssociatedObject) is TextBlock placeholder)
            {
                placeholder.Visibility =
                    string.IsNullOrEmpty(AssociatedObject.Password) && !AssociatedObject.IsKeyboardFocused
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }
    }
}