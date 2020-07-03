using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VADEdit.Types;

namespace VADEdit
{
    /// <summary>
    /// Interaction logic for AudioChunkView.xaml
    /// </summary>
    public partial class AudioChunkView : UserControl
    {
        public static event EventHandler StaticFocused;
        public event EventHandler PlayButtonClicked;
        public event EventHandler StopButtonClicked;
        public event EventHandler SttButtonClicked;
        public event EventHandler DuplicateButtonClicked;
        public event EventHandler ExportButtonClicked;
        public event EventHandler ResetButtonClicked;
        public event EventHandler DeleteButtonClicked;
        public event EventHandler GotSelectionFocus;
        public event EventHandler TextChanged;
        public event EventHandler IndexChanged;

        public enum State : short
        {
            Idle,
            STTSuccess,
            ExportSuccess,
            Error
        }


        public Visibility PlayButtonVisibility
        {
            get { return (Visibility)GetValue(PlayButtonVisibilityProperty); }
            set { SetValue(PlayButtonVisibilityProperty, value); }
        }

        // Using a DependencyProperty as the backing store for PlayButtonVisibility.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PlayButtonVisibilityProperty =
            DependencyProperty.Register("PlayButtonVisibility", typeof(Visibility), typeof(AudioChunkView), new PropertyMetadata(Visibility.Visible));


        public TimeRange TimeRange
        {
            get { return (TimeRange)GetValue(TimeRangeProperty); }
            set { SetValue(TimeRangeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for TimeRange.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TimeRangeProperty =
            DependencyProperty.Register("TimeRange", typeof(TimeRange), typeof(AudioChunkView), new PropertyMetadata(null));


        public string SpeechText
        {
            get { return (string)GetValue(SpeechTextProperty); }
            set { SetValue(SpeechTextProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SpeechText.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SpeechTextProperty =
            DependencyProperty.Register("SpeechText", typeof(string), typeof(AudioChunkView), new PropertyMetadata(""));


        public string SttText
        {
            get { return (string)GetValue(GSttTextProperty); }
            set { SetValue(GSttTextProperty, value); }
        }

        // Using a DependencyProperty as the backing store for GSttText.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty GSttTextProperty =
            DependencyProperty.Register("GSttText", typeof(string), typeof(AudioChunkView), new PropertyMetadata(""));

        public State VisualState
        {
            get { return (State)GetValue(VisualStateProperty); }
            set { SetValue(VisualStateProperty, value); }
        }

        // Using a DependencyProperty as the backing store for VisualState.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty VisualStateProperty =
            DependencyProperty.Register("VisualState", typeof(State), typeof(AudioChunkView), new PropertyMetadata(State.Idle, (o, e) =>
            {
                (o as AudioChunkView).UpdateVisuals();
            }));

        private bool mouseDownTriggered = false;

        public AudioChunkView()
        {
            InitializeComponent();

            DataContext = this;

            StaticFocused += (o, e) =>
            {
                if ((o as AudioChunkView).Parent == Parent && grdSelect.IsVisible)
                    grdSelect.Visibility = Visibility.Hidden;
            };

            btnDrag.DragCompleted += (o, e) =>
            {
                BorderBrush = Brushes.Transparent;
                Panel.SetZIndex(this, 0);
                transform.Y = 0;
            };

            btnDrag.DragDelta += (o, e) =>
            {
                transform.Y += e.VerticalChange;
                if (transform.Y - 2 > Height / 2)
                {
                    var parent = Parent as StackPanel;
                    var newY = -(transform.Y - 5);
                    var newIndex = parent.Children.IndexOf(this) + 1;

                    if (newIndex < parent.Children.Count)
                    {
                        parent.Children.Remove(this);
                        parent.Children.Insert(newIndex, this);
                        transform.Y = newY;
                        IndexChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
                else if (transform.Y + 2 < -Height / 2)
                {
                    var parent = Parent as StackPanel;
                    var newY = -(transform.Y + 5);
                    var newIndex = parent.Children.IndexOf(this) - 1;

                    if (newIndex >= 0)
                    {
                        parent.Children.Remove(this);
                        parent.Children.Insert(newIndex, this);
                        transform.Y = newY;
                        IndexChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            };

            btnDrag.DragStarted += delegate
            {
                BorderBrush = (SolidColorBrush)Utils.BrushConverter.ConvertFromString("#808080");
                Panel.SetZIndex(this, 1);
            };
        }

        private void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            PlayButtonClicked?.Invoke(this, e);
        }

        private void btnStt_Click(object sender, RoutedEventArgs e)
        {
            SttButtonClicked?.Invoke(this, e);
        }

        private void btnDuplicate_Click(object sender, RoutedEventArgs e)
        {
            DuplicateButtonClicked?.Invoke(this, e);
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            ExportButtonClicked?.Invoke(this, e);
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            DeleteButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            StopButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void TextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (mouseDownTriggered)
            {
                mouseDownTriggered = false;
                return;
            }
            StaticFocused?.Invoke(this, EventArgs.Empty);
            grdSelect.Visibility = Visibility.Visible;
            GotSelectionFocus?.Invoke(this, EventArgs.Empty);
        }

        private void txtSpeech_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtSpeech.Text))
                txtSpeech.ToolTip = null;

            if (grdSelect.IsVisible)
            {
                txtSpeech.ToolTip = txtSpeech.Text;
                VisualState = State.Idle;
                TextChanged?.Invoke(this, EventArgs.Empty);
            }
            // IsFocused
        }

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            StaticFocused?.Invoke(this, EventArgs.Empty);
            grdSelect.Visibility = Visibility.Visible;
            GotSelectionFocus?.Invoke(this, EventArgs.Empty);
            mouseDownTriggered = true;
            Keyboard.Focus(txtSpeech);
            base.OnPreviewMouseLeftButtonDown(e);
        }

        private void txtTime_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        public void Select()
        {
            if (Keyboard.FocusedElement != txtSpeech)
                Keyboard.Focus(txtSpeech);
        }

        public void Unselect()
        {
            StaticFocused?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateVisuals()
        {
            switch (VisualState)
            {
                case State.Idle:
                    grdBackground.Background = Application.Current.MainWindow.Background;
                    break;
                case State.STTSuccess:
                    grdBackground.Background = (SolidColorBrush)Utils.BrushConverter.ConvertFromString(Settings.ChunkSTTColor);
                    break;
                case State.Error:
                    grdBackground.Background = (SolidColorBrush)Utils.BrushConverter.ConvertFromString(Settings.ChunkErrorColor);
                    break;
                case State.ExportSuccess:
                    grdBackground.Background = (SolidColorBrush)Utils.BrushConverter.ConvertFromString(Settings.ChunkExportColor);
                    break;
            }
            var textColor = (SolidColorBrush)Utils.BrushConverter.ConvertFromString(Settings.ChunkTextColor);
            txtSpeech.CaretBrush = textColor;
            txtSpeech.Foreground = textColor;
            txtTime.Foreground = textColor;
            txtIndex.Foreground = textColor;
            txtSpeech.SelectionBrush = (SolidColorBrush)Utils.BrushConverter.ConvertFromString(Settings.ChunkTextSelectionColor);
            grdSelect.Background = (SolidColorBrush)Utils.BrushConverter.ConvertFromString(Settings.ChunkSelectionColor);
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            VisualState = State.Idle;
            ResetButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            SpeechText = "";
            SttText = "";
            VisualState = State.Idle;
        }
    }
}
