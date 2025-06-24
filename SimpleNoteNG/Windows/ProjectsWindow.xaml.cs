using Microsoft.EntityFrameworkCore;
using NAudio.Midi;
using SimpleNoteNG.Data;
using SimpleNoteNG.Models;
using SimpleNoteNG.Pages;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SimpleNoteNG.Windows
{
    /// <summary>
    /// Логика взаимодействия для ProjectsWindow.xaml
    /// </summary>
    public partial class ProjectsWindow : Window
    {
        private string _username;
        private int _userId;
        private List<Project> _allProjects = new List<Project>();
        private readonly Dictionary<Button, int> _projectIds = new Dictionary<Button, int>();

        private bool _isProfileShown = false;
        private Profile _profilePage;

        public ProjectsWindow(string username, int userId)
        {
            InitializeComponent();
            _username = username;
            _userId = userId;

            InitializeUI();
            InitializeProfilePage();
            LoadProjects();
            SetupSearch();

            // Добавляем обработчик для кнопки Upload
            Upload.Click += Upload_Click;

            using (var db = new AppDbContext())
            {
                var user = db.Users.FirstOrDefault(u => u.UserId == _userId);

                if(user.EmailConfirmed == false)
                {
                    Create.IsEnabled = false;
                    Upload.IsEnabled = false;
                    txtConfirm.Text = "confirm or edit your email address in profile to get started";
                    Create.Opacity = 50;
                    Upload.Opacity = 50;
                    LetsgoText.Text = "WAIT, ";
                }
            }
        }
        private void StartText_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (_isProfileShown)
                {
                    HideProfile();
                }
                else
                {
                    ShowProfile();
                }
            }
        }
        private void InitializeUI()
        {
            StartText.Text = $"{_username.ToUpper()}!";
            SearchBox.TextChanged += (s, e) => FilterProjects(SearchBox.Text);

            // Убираем фокус с StartText при загрузке
            Loaded += (s, e) => { Keyboard.ClearFocus(); };
        }

        private void InitializeProfilePage()
        {
            using (var db = new AppDbContext())
            {
                var user = db.Users.FirstOrDefault(u => u.UserId == _userId);
                if (user != null)
                {
                    _profilePage = new Profile(_userId, _username, user.Email, user.EmailConfirmed);
                    ProfileFrame.Content = _profilePage;
                }
            }
        }

        private void ShowProfile()
        {
            _isProfileShown = true;
            ProfileFrame.Visibility = Visibility.Visible;

            var animation = new DoubleAnimation
            {
                From = 0,
                To = 296,
                Duration = TimeSpan.FromSeconds(0.6),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            ProfileFrame.BeginAnimation(FrameworkElement.HeightProperty, animation);
        }

        public void DeleteAccount()
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    // Удаляем все проекты пользователя
                    var userProjects = db.Projects.Where(p => p.UserId == _userId).ToList();
                    db.Projects.RemoveRange(userProjects);

                    // Удаляем самого пользователя
                    var user = db.Users.FirstOrDefault(u => u.UserId == _userId);
                    if (user != null)
                    {
                        db.Users.Remove(user);
                    }

                    db.SaveChanges();
                }

                // Возвращаемся на главный экран
                var mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting account: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Logout()
        {
            // Возвращаемся на главный экран
            var mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }

        public void HideProfile()
        {
            _isProfileShown = false;

            var animation = new DoubleAnimation
            {
                From = 296,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            animation.Completed += (s, _) =>
            {
                ProfileFrame.Visibility = Visibility.Collapsed;
            };

            ProfileFrame.BeginAnimation(FrameworkElement.HeightProperty, animation);
        }

        private void SetupSearch()
        {
            SearchBox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    SearchBox.Text = "";
                    SearchBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
            };
        }

        public void LoadProjects()
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    _allProjects = db.Projects
                        .Where(p => p.UserId == _userId && !string.IsNullOrEmpty(p.Path)) // Только сохраненные проекты
                        .OrderByDescending(p => p.UpdatedAt)
                        .ToList();

                    FilterProjects("");
                }
            }
            catch (Exception ex)
            {
                ShowError("Error loading projects", ex);
            }
        }

        private void FilterProjects(string searchText)
        {
            try
            {
                var filteredProjects = _allProjects
                    .Where(p => p.ProjectName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                DisplayProjects(filteredProjects);
            }
            catch (Exception ex)
            {
                ShowError("Error filtering projects", ex);
            }
        }

        private void DisplayProjects(List<Project> projects)
        {
            FilesList.Children.Clear();
            _projectIds.Clear();

            if (!projects.Any())
            {
                AddNoProjectsMessage();
                return;
            }

            var todayProjects = projects.Where(p => p.UpdatedAt.Date == DateTime.Today).ToList();
            var recentlyProjects = projects.Where(p =>
                p.UpdatedAt.Date >= DateTime.Today.AddDays(-2) &&
                p.UpdatedAt.Date < DateTime.Today).ToList();
            var earlierProjects = projects.Where(p =>
                p.UpdatedAt.Date < DateTime.Today.AddDays(-2)).ToList();

            if (todayProjects.Any()) AddProjectGroup("Today", todayProjects);
            if (recentlyProjects.Any()) AddProjectGroup("Recently", recentlyProjects);
            if (earlierProjects.Any()) AddProjectGroup("Earlier", earlierProjects);
        }

        private void AddNoProjectsMessage()
        {
            var message = new TextBlock
            {
                Text = "No projects found",
                Foreground = Brushes.Gray,
                FontSize = 20,
                Margin = new Thickness(0, 20, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            FilesList.Children.Add(message);
        }

        private void AddProjectGroup(string header, List<Project> projects)
        {
            var headerText = new TextBlock
            {
                Text = header,
                Foreground = Brushes.White,
                FontSize = 20,
                Margin = new Thickness(0, 10, 0, 5)
            };
            FilesList.Children.Add(headerText);

            foreach (var project in projects)
            {
                var projectButton = CreateProjectButton(project);
                _projectIds[projectButton] = project.ProjectId;
                FilesList.Children.Add(projectButton);
            }
        }

        private Button CreateProjectButton(Project project)
        {
            var button = new Button
            {
                Style = (Style)FindResource("FileButtonStyle"),
                Content = project.ProjectName,
                Tag = project.UpdatedAt.ToString("dd.MM.yyyy (HH:mm)"),
                Cursor = Cursors.Hand
            };

            button.Click += ProjectButton_Click;
            return button;
        }

        private void ProjectButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && _projectIds.TryGetValue(button, out int projectId))
            {
                try
                {
                    using (var db = new AppDbContext())
                    {
                        var project = db.Projects.FirstOrDefault(p => p.ProjectId == projectId);
                        if (project != null)
                        {
                            OpenWorkspace(projectId, project.Path);
                        }
                        else
                        {
                            MessageBox.Show("Project not found in database", "Error",
                                          MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ShowError("Error opening project", ex);
                }
            }
        }

        private void OpenWorkspace(int projectId, string projectPath)
        {
            var workspace = new Workspace(projectId, _userId);
            workspace.Show();

            workspace.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(projectPath) && File.Exists(projectPath))
                    {
                        workspace.LoadProject(projectPath);
                    }
                    else
                    {
                        workspace.InitializeEmptyProject();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading project: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }, DispatcherPriority.Loaded);

            this.Close();
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    // Получаем список всех существующих проектов с именами, начинающимися на "New Project"
                    var existingProjects = db.Projects
                        .Where(p => p.ProjectName.StartsWith("New Project"))
                        .ToList();

                    // Находим максимальный номер в существующих проектах
                    int maxNumber = 0;
                    foreach (var project in existingProjects)
                    {
                        if (int.TryParse(project.ProjectName.Replace("New Project", "").Trim(), out int number))
                        {
                            if (number > maxNumber)
                                maxNumber = number;
                        }
                    }

                    // Создаем новое имя проекта с номером на 1 больше максимального
                    string newProjectName = $"New Project {maxNumber + 1}";

                    var newProject = new Project
                    {
                        UserId = _userId,
                        ProjectName = newProjectName,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        Path = ""
                    };

                    db.Projects.Add(newProject);
                    db.SaveChanges();

                    OpenWorkspace(newProject.ProjectId, "");
                }
            }
            catch (Exception ex)
            {
                ShowError("Error creating project", ex);
            }
        }
        private void Upload_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MIDI Files (*.mid;*.midi)|*.mid;*.midi",
                DefaultExt = ".mid",
                Multiselect = false
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    // Проверка существования файла
                    if (!File.Exists(openDialog.FileName))
                    {
                        MessageBox.Show("Selected file does not exist", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Проверка валидности MIDI файла (без using)
                    try
                    {
                        var midiFile = new MidiFile(openDialog.FileName);
                        if (midiFile.Tracks == 0)
                        {
                            MessageBox.Show("The selected MIDI file contains no tracks", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Invalid MIDI file: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Создание нового проекта
                    using (var db = new AppDbContext())
                    {
                        var projectName = Path.GetFileNameWithoutExtension(openDialog.FileName);

                        var newProject = new Project
                        {
                            UserId = _userId,
                            ProjectName = projectName,
                            Path = openDialog.FileName,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };

                        db.Projects.Add(newProject);
                        db.SaveChanges();

                        var workspace = new Workspace(newProject.ProjectId, _userId);
                        workspace.Show();

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            workspace.LoadProject(openDialog.FileName);
                            
                        }));

                        this.Close();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error creating project: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ShowError(string message, Exception ex)
        {
            MessageBox.Show($"{message}: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
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

