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
using Windows.Web.AtomPub;

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

        public ProjectsWindow(string username, int userId)
        {
            InitializeComponent();
            _username = username;
            _userId = userId;

            InitializeUI();
            LoadProjects();
            SetupSearch();
        }

        private void InitializeUI()
        {
            StartText.Text = $"LET'S GO, {_username.ToUpper()}!";
            SearchBox.TextChanged += (s, e) => FilterProjects(SearchBox.Text);
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

        private void LoadProjects()
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    _allProjects = db.Projects
                        .Where(p => p.UserId == _userId)
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
            return new Button
            {
                Style = (Style)FindResource("FileButtonStyle"),
                Content = project.ProjectName,
                Tag = project.UpdatedAt.ToString("dd.MM.yyyy (HH:mm)"),
                Cursor = Cursors.Hand
            };
        }

        private void ProjectButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && _projectIds.TryGetValue(button, out int projectId))
            {
                try
                {
                    OpenWorkspace(projectId);
                }
                catch (Exception ex)
                {
                    ShowError("Error opening project", ex);
                }
            }
        }

        private void OpenWorkspace(int projectId)
        {
            var workspace = new Workspace(projectId, _userId);
            workspace.Show();
            this.Close();
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    var newProject = new Project
                    {
                        UserId = _userId,
                        ProjectName = "New Project",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        Path = ""
                    };

                    db.Projects.Add(newProject);
                    db.SaveChanges();

                    OpenWorkspace(newProject.ProjectId);
                }
            }
            catch (Exception ex)
            {
                ShowError("Error creating project", ex);
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

