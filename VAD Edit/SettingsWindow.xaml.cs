using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Xceed.Wpf.Toolkit;

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

#if !GOOGLE_STT
            lblGoogleCredential.Visibility = Visibility.Collapsed;
            grdGoogleCredential.Visibility = Visibility.Collapsed;
#endif

            chkSplitOnSilence.IsChecked = Settings.SplitOnSilence;
            txtSplitLength.Text = Settings.SplitLength.ToString();
            txtMaxSilence.Text = Settings.MaxSilence.ToString();
            txtMinLength.Text = Settings.MinLength.ToString();
            txtBatchSize.Text = Settings.BatchSize.ToString();
            txtMinVolume.Text = Settings.MinVolume.ToString();
            cmbLanguage.Text = Settings.LanguageCode;
            txtSttCredentialPath.Text = Settings.STTCredentialtPath;
            chkIncludeSttResult.IsChecked = Settings.IncludeSttResult;
            chkIncludeAudioFileSize.IsChecked = Settings.IncludeAudioFileSize;
            chkIncludeAudioLengthMillis.IsChecked = Settings.IncludeAudioLengthMillis;
            clrAudioWaveSelection.SelectedColor = (Color)ColorConverter.ConvertFromString(Settings.AudioWaveSelectionColor);
            clrAudioWave.SelectedColor = (Color)ColorConverter.ConvertFromString(Settings.AudioWaveColor);
            clrAudioWaveBackground.SelectedColor = (Color)ColorConverter.ConvertFromString(Settings.AudioWaveBackgroundColor);
            clrChunkError.SelectedColor = (Color)ColorConverter.ConvertFromString(Settings.ChunkErrorColor);
            clrChunkExport.SelectedColor = (Color)ColorConverter.ConvertFromString(Settings.ChunkExportColor);
            clrChunkSTT.SelectedColor = (Color)ColorConverter.ConvertFromString(Settings.ChunkSTTColor);
            clrChunkText.SelectedColor = (Color)ColorConverter.ConvertFromString(Settings.ChunkTextColor);
            clrChunkSelection.SelectedColor = (Color)ColorConverter.ConvertFromString(Settings.ChunkSelectionColor);
            clrChunkTextSelection.SelectedColor = (Color)ColorConverter.ConvertFromString(Settings.ChunkTextSelectionColor);
            clrAppBackground.SelectedColor = (Color)ColorConverter.ConvertFromString(Settings.AppBackgroundColor);

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

            txtSplitLength.PreviewTextInput += (o, e) =>
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

            txtBatchSize.PreviewTextInput += (o, e) =>
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
                Settings.SplitOnSilence = win.chkSplitOnSilence.IsChecked.Value;
                Settings.SplitLength = int.Parse(win.txtSplitLength.Text);
                Settings.MaxSilence = int.Parse(win.txtMaxSilence.Text);
                Settings.MinLength = int.Parse(win.txtMinLength.Text);
                Settings.MinVolume = float.Parse(win.txtMinVolume.Text);
                Settings.BatchSize = int.Parse(win.txtBatchSize.Text);
                Settings.LanguageCode = win.cmbLanguage.Text;
                Settings.STTCredentialtPath = win.txtSttCredentialPath.Text;
                Settings.IncludeSttResult = win.chkIncludeSttResult.IsChecked.Value;
                Settings.IncludeAudioFileSize = win.chkIncludeAudioFileSize.IsChecked.Value;
                Settings.IncludeAudioLengthMillis = win.chkIncludeAudioLengthMillis.IsChecked.Value;
                Settings.AudioWaveBackgroundColor = win.clrAudioWaveBackground.SelectedColorText;
                Settings.AudioWaveColor = win.clrAudioWave.SelectedColorText;
                Settings.AudioWaveSelectionColor = win.clrAudioWaveSelection.SelectedColorText;
                Settings.ChunkErrorColor = win.clrChunkError.SelectedColorText;
                Settings.ChunkExportColor = win.clrChunkExport.SelectedColorText;
                Settings.ChunkSTTColor = win.clrChunkSTT.SelectedColorText;
                Settings.ChunkTextColor = win.clrChunkText.SelectedColorText;
                Settings.ChunkSelectionColor = win.clrChunkSelection.SelectedColorText;
                Settings.ChunkTextSelectionColor = win.clrChunkTextSelection.SelectedColorText;
                Settings.AppBackgroundColor = win.clrAppBackground.SelectedColorText;
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

        private void btnChangeColor_Click(object sender, RoutedEventArgs e)
        {
            //new ColorPicker().
        }
    }

    internal class FalseToCollapsed : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    internal class TrueToCollapsed : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
