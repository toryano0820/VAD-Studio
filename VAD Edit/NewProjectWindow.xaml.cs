using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VADEdit
{
    /// <summary>
    /// Interaction logic for NewProjectWindow.xaml
    /// </summary>
    public partial class NewProjectWindow : Window
    {
        
        public string ProjectName
        {
            get { return (string)GetValue(ProjectNameProperty); }
            set { SetValue(ProjectNameProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ProjectName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ProjectNameProperty =
            DependencyProperty.Register("ProjectName", typeof(string), typeof(NewProjectWindow), new PropertyMetadata("", (o, e) =>
            {
                (o as NewProjectWindow).TryEnableOkButton();
            }));

        public string ProjectBaseLocation
        {
            get { return (string)GetValue(ProjectLocationProperty); }
            set { SetValue(ProjectLocationProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ProjectLocation.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ProjectLocationProperty =
            DependencyProperty.Register("ProjectLocation", typeof(string), typeof(NewProjectWindow), new PropertyMetadata("", (o, e) =>
            {
                (o as NewProjectWindow).TryEnableOkButton();
            }));

        public string MediaLocation
        {
            get { return (string)GetValue(MediaLocationProperty); }
            set { SetValue(MediaLocationProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MediaLocation.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MediaLocationProperty =
            DependencyProperty.Register("MediaLocation", typeof(string), typeof(NewProjectWindow), new PropertyMetadata("", (o, e) =>
            {
                (o as NewProjectWindow).TryEnableOkButton();
            }));
        

        public static ProjectInfo? GetNewProjectInfo()
        {
            var win = new NewProjectWindow();
            if (win.ShowDialog() == true)
            {
                return new ProjectInfo() {
                    MediaLocation = win.MediaLocation,
                    ProjectBaseLocation = win.ProjectBaseLocation,
                    ProjectName = win.ProjectName
                };
            }
            return null;
        }

        private NewProjectWindow()
        {
            InitializeComponent();

            DataContext = this;
            Owner = App.Current.MainWindow;

            ProjectBaseLocation = Settings.ProjectBaseLocation;
            var projectName = System.IO.Path.Combine(Settings.ProjectBaseLocation, "VAD Project");
            while (Directory.Exists(projectName))
            {
                if (Regex.IsMatch(projectName, @" \d+$"))
                    projectName = Regex.Replace(projectName, @" \d+$", " " + (int.Parse(projectName.Split(' ').Last()) + 1).ToString());
                else
                    projectName = projectName + " 1";
            }
            ProjectName = Path.GetFileName(projectName);
        }

        private void TryEnableOkButton()
        {
            var nameOk = !Directory.Exists(Path.Combine(ProjectBaseLocation, ProjectName));
            txtProjectName.Foreground = nameOk ? SystemColors.ControlTextBrush : Brushes.Red;
            btnOk.IsEnabled = !string.IsNullOrWhiteSpace(ProjectName) && !string.IsNullOrWhiteSpace(ProjectBaseLocation) && !string.IsNullOrWhiteSpace(MediaLocation) && nameOk;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void btnProjectLocation_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog()
            {
                Title = "Project Base Location",
                InitialDirectory = txtProjectLocation.Text,
                IsFolderPicker = true,
                AddToMostRecentlyUsedList = false,
                AllowNonFileSystemItems = false,
                EnsureFileExists = true,
                EnsurePathExists = true,
                EnsureReadOnly = false,
                EnsureValidNames = true,
                Multiselect = false,
                ShowPlacesList = true
            };
            var res = dlg.ShowDialog();
            if (res == CommonFileDialogResult.Ok)
            {
                ProjectBaseLocation = dlg.FileName;
            }
            dlg.Dispose();
        }

        private void btnMediaLocation_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.OpenFileDialog()
            {
                Filter = "Media Files|*.wav;*.mp3;*.flac;*.mp4;*.avi;*.flv",
                InitialDirectory = Settings.LastMediaLocation,
                Title = "Load Media"
            };
            var res = dlg.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.OK)
            {
                MediaLocation = dlg.FileName;
            }
            dlg.Dispose();
        }
    }

    public struct ProjectInfo
    {
        public string ProjectName { get; set; }
        public string ProjectBaseLocation { get; set; }
        public string MediaLocation { get; set; }
    }
}
