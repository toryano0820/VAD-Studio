using System.ComponentModel;
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

            txtMaxSilence.Text = Settings.MaxSilence.ToString();
            txtMinLength.Text = Settings.MinLength.ToString();
            txtMinVolume.Text = Settings.MinVolume.ToString();

            cmbLanguage.Text = Settings.LanguageCode;

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
            win.ShowDialog();

            Settings.MaxSilence = int.Parse(win.txtMaxSilence.Text);
            Settings.MinLength = int.Parse(win.txtMinLength.Text);
            Settings.MinVolume = float.Parse(win.txtMinVolume.Text);

            Settings.LanguageCode = win.cmbLanguage.Text;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
