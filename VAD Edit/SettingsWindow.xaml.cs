using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace VADEdit
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            
            txtMaxSilence.Text = Settings.MaxSilence.ToString();
            txtMinLength.Text = Settings.MinLength.ToString();
            txtMinVolume.Text = Settings.MinVolume.ToString();
            cmbLanguage.Text = Settings.LanguageCode;
            txtSttCredentialPath.Text = Settings.STTCredentialtPath.Contains(appDir) ? Settings.STTCredentialtPath.Substring(appDir.Length) : Settings.STTCredentialtPath;
            chkIncludeSttResult.IsChecked = Settings.IncludeSttResult;
            chkIncludeAudioFileSize.IsChecked = Settings.IncludeAudioFileSize;

            txtMaxSilence.PreviewTextInput += (o, e) =>
            {
                var textBox = o as TextBox;
                var proposedText = textBox.Text;
                proposedText = proposedText.Remove(textBox.SelectionStart, textBox.SelectionLength);
                proposedText = proposedText.Insert(textBox.SelectionStart, e.Text);

                if (!int.TryParse(proposedText, out int res))
                {
                    e.Handled = true;
                }
            };

            txtMinVolume.PreviewTextInput += (o, e) =>
            {
                var textBox = o as TextBox;
                var proposedText = textBox.Text;
                proposedText = proposedText.Remove(textBox.SelectionStart, textBox.SelectionLength);
                proposedText = proposedText.Insert(textBox.SelectionStart, e.Text);

                if (!float.TryParse(proposedText, out float res) || !(res >= 0 && res <= 100))
                {
                    e.Handled = true;
                }
            };

            txtMinLength.PreviewTextInput += (o, e) =>
            {
                var textBox = o as TextBox;
                var proposedText = textBox.Text;
                proposedText = proposedText.Remove(textBox.SelectionStart, textBox.SelectionLength);
                proposedText = proposedText.Insert(textBox.SelectionStart, e.Text);

                if (!int.TryParse(proposedText, out int res) || res < 0)
                {
                    e.Handled = true;
                }
            };
        }

        public static new void Show()
        {
            var win = new SettingsWindow();

            if (win.ShowDialog() == true)
            {
                Settings.MaxSilence = int.Parse(win.txtMaxSilence.Text);
                Settings.MinLength = int.Parse(win.txtMinLength.Text);
                Settings.MinVolume = float.Parse(win.txtMinVolume.Text);
                Settings.LanguageCode = win.cmbLanguage.Text;
                Settings.STTCredentialtPath = win.txtSttCredentialPath.Text.Contains(@"\") ? win.txtSttCredentialPath.Text : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, win.txtSttCredentialPath.Text);
                Settings.IncludeSttResult = win.chkIncludeSttResult.IsChecked.Value;
                Settings.IncludeAudioFileSize = win.chkIncludeAudioFileSize.IsChecked.Value;

                Settings.Save();
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnSttCredentialPath_Click(object sender, RoutedEventArgs e)
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var dlg = new System.Windows.Forms.OpenFileDialog()
            {
                Filter = "JSON Files|*.json",
                InitialDirectory = appDir
            };
            var res = dlg.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.OK)
            {
                var fileDir = dlg.FileName;
                if (fileDir.StartsWith(appDir))
                    fileDir = fileDir.Substring(appDir.Length);
                txtSttCredentialPath.Text = fileDir;
            }
            dlg.Dispose();
        }
    }
}
