using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Xceed.Wpf.Toolkit;

namespace VAD
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public enum Button
        {
            OK,
            Cancel,
            Apply
        }

        public static event EventHandler<Button> ButtonClicked;

        public SettingsWindow()
        {
            InitializeComponent();

            Owner = App.Current.MainWindow;

            chkSplitOnSilence.IsChecked = Settings.SplitOnSilence;
            txtSplitLength.Text = Settings.SplitLength.ToString();
            txtMaxSilence.Text = Settings.MaxSilence.ToString();
            txtMinLength.Text = Settings.MinLength.ToString();
            txtBatchSize.Text = Settings.BatchSize.ToString();
            txtMinVolume.Text = Settings.MinVolume.ToString();
            cmbSttLanguage.Text = Settings.SttLanguage;
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
            txtProjectBaseLocation.Text = Settings.ProjectBaseLocation;

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

        public static void Apply(SettingsWindow win)
        {
            Settings.SplitOnSilence = win.chkSplitOnSilence.IsChecked.Value;
            Settings.SplitLength = int.Parse(win.txtSplitLength.Text);
            Settings.MaxSilence = int.Parse(win.txtMaxSilence.Text);
            Settings.MinLength = int.Parse(win.txtMinLength.Text);
            Settings.MinVolume = float.Parse(win.txtMinVolume.Text);
            Settings.BatchSize = int.Parse(win.txtBatchSize.Text);
            Settings.SttLanguage = win.cmbSttLanguage.Text;
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
            Settings.ProjectBaseLocation = win.txtProjectBaseLocation.Text;
            Settings.Save();
        }

        private void ShowNormal()
        {
            base.Show();
        }

        public static new void Show()
        {
            new SettingsWindow().ShowNormal();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            Apply(this);
            ButtonClicked?.Invoke(this, Button.Apply);
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Apply(this);
            ButtonClicked?.Invoke(this, Button.OK);
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            ButtonClicked?.Invoke(this, Button.Cancel);
            Close();
        }

        private void btnProjectBaseLocation_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog()
            {
                Title = "Project Base Location",
                InitialDirectory = Settings.ProjectBaseLocation,
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
                txtProjectBaseLocation.Text = dlg.FileName;
            }
            dlg.Dispose();
        }
    }
}
