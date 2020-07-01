using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VADEdit.Types;
using static VADEdit.Utils;

namespace VADEdit
{
    /// <summary>
    /// Interaction logic for WaveFormViewer.xaml
    /// </summary>
    public partial class WaveFormViewer : UserControl
    {
        public event EventHandler NewSelection;
        public event EventHandler<TimeRange> SelectionChanged;
        public event EventHandler PlayRangeEnded;

        public double ScrollOffset
        {
            get { return (double)GetValue(ScrollOffsetProperty); }
            set { SetValue(ScrollOffsetProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ScrollOffset.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ScrollOffsetProperty =
            DependencyProperty.Register("ScrollOffset", typeof(double), typeof(WaveFormViewer), new PropertyMetadata(0.0, (o, e) =>
            {
                var @this = o as WaveFormViewer;
                @this.drawWave = true;
                @this.Render();
            }, (o, e) =>
            {
                var @this = o as WaveFormViewer;
                var value = (double)e;
                var maxScroll = @this.MaxScroll;
                return (value < 0.0) ? 0.0 : (value > maxScroll) ? maxScroll : value;
            }));


        public double Zoom
        {
            get { return (double)GetValue(ZoomProperty); }
            set { SetValue(ZoomProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Zoom.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register("Zoom", typeof(double), typeof(WaveFormViewer), new PropertyMetadata(1.0, (o, e) =>
            {
                var @this = o as WaveFormViewer;
                @this.drawWave = true;
                @this.Render();
            }, (o, e) =>
            {
                var @this = o as WaveFormViewer;
                var value = (double)e;
                return (value < @this.MinZoom) ? @this.MinZoom : (value > @this.MaxZoom) ? @this.MaxZoom : value;
            }));


        public long SelectionStart
        {
            get { return (long)GetValue(SelectionStartProperty); }
            set { SetValue(SelectionStartProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SelectionStart.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectionStartProperty =
            DependencyProperty.Register("SelectionStart", typeof(long), typeof(WaveFormViewer), new PropertyMetadata(0L, (o, e) =>
            {
                var @this = o as WaveFormViewer;
                var value = (long)e.NewValue;

                if (value > @this.SelectionEnd)
                    @this.SelectionEnd = value;

                @this.UpdateSelection();
            }));


        public long SelectionEnd
        {
            get { return (long)GetValue(SelectionEndProperty); }
            set { SetValue(SelectionEndProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SelectionEnd.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectionEndProperty =
            DependencyProperty.Register("SelectionEnd", typeof(long), typeof(WaveFormViewer), new PropertyMetadata(0L, (o, e) =>
            {
                var @this = o as WaveFormViewer;
                var value = (long)e.NewValue;

                if (value < @this.SelectionStart)
                    @this.SelectionStart = value;

                @this.UpdateSelection();
            }));

        public bool HaveSelection
        {
            get { return (bool)GetValue(HaveSelectionProperty); }
            set { SetValue(HaveSelectionProperty, value); }
        }

        // Using a DependencyProperty as the backing store for HaveSelection.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HaveSelectionProperty =
            DependencyProperty.Register("HaveSelection", typeof(bool), typeof(WaveFormViewer), new PropertyMetadata(false));



        public double WaveFormWidth
        {
            get
            {
                return ActualWidth * Zoom;
            }
        }

        public double MaxScroll
        {
            get
            {
                return WaveFormWidth - ActualWidth;
            }
        }

        public WaveStream WaveStream { get; private set; }

        public double MaxZoom { get; private set; } = 1.0;
        public double MinZoom { get; private set; } = 1.0;
        public List<float> WaveFormData { get; private set; }
        public TimeSpan PlayRangeEnd { get; set; } = TimeSpan.Zero;

        public WaveOut Player { get; } = new WaveOut()
        {
            NumberOfBuffers = 112,
            DesiredLatency = 10
        };

        private long waveSize = 0L;
        private int waveFormSize = 0;
        private TimeSpan waveDuration = TimeSpan.Zero;
        private double maxSpan = 0.0;
        private double downX = 0.0;
        private double downScroll = 0.0;
        private bool modifierCtrlPressed = false;
        private bool modifierShiftPressed = false;
        private bool drawWave = false;

        public WaveFormViewer()
        {
            InitializeComponent();

            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
            Background = Brushes.Transparent;
            Focusable = true;

            Loaded += delegate
            {
                UpdateVisuals();
            };

            SizeChanged += delegate
            {
                drawWave = true;
                Render();
            };
        }

        public async Task SetWaveStream(string fileName, Action<bool> callback = null)
        {
            WaveStream = null;
            WaveFormData = null;
            waveSize = 0L;
            waveFormSize = 0;
            waveDuration = TimeSpan.Zero;
            UpdateVisuals();

            var thread = new Thread(() =>
            {
                try
                {
                    var waveStream = new WaveFileReader(fileName);
                    if (waveStream.WaveFormat.Channels != 1 || waveStream.WaveFormat.SampleRate != 16000)
                    {
                        throw new FileFormatException("Input should be 16kHz Mono WAV file.");
                    }
                    var wave = new WaveChannel32(waveStream);

                    if (wave == null)
                        return;

                    waveStream.Position = 0L;
                    waveDuration = waveStream.TotalTime;

                    int sampleSize = 0;

                    sampleSize = (from i in Utils.Range(1, 512)
                                  let align = wave.WaveFormat.BlockAlign * (double)i
                                  let v = wave.Length / align
                                  where v == (int)v
                                  select (int)align).Max();

                    var bufferSize = (int)(wave.Length / (double)sampleSize);
                    int read = 0;

                    //this.waveSize = waveStream.Length;
                    waveSize = waveStream.Length;

                    Dispatcher.Invoke(() =>
                    {
                        ScrollOffset = 0.0;
                        MaxZoom = Math.Max(wave.TotalTime.TotalSeconds / 4, MinZoom);
                        Zoom = MinZoom;
                        SelectionStart = 0;
                        SelectionEnd = 0;
                    });

                    var maxWidth = Math.Min(WpfScreen.AllScreens().OrderByDescending(s => s.WorkingArea.Width).First().WorkingArea.Width * MaxZoom, waveSize);

                    var iter = 0;
                    var waveFormData = new float[(int)(maxWidth * 2)];
                    while (wave.Position < wave.Length)
                    {
                        var rwaIndex = 0;
                        var rawWaveArray = new float[bufferSize / 4];

                        var buffer = new byte[bufferSize];
                        read = wave.Read(buffer, 0, bufferSize);

                        for (int i = 0; i < read / 4; i++)
                        {
                            var point = BitConverter.ToSingle(buffer, i * 4);
                            rawWaveArray[rwaIndex++] = point;
                        }
                        buffer = null;

                        var wl = rawWaveArray.ToList();
                        var rwaCount = rawWaveArray.Length;
                        Array.Resize(ref rawWaveArray, 0);
                        rawWaveArray = null;

                        var samplesPerPixel = (rwaCount / (maxWidth / sampleSize));

                        var writeOffset = (int)((maxWidth / sampleSize) * iter);
                        for (int i = 0; i < (int)(maxWidth / sampleSize); i++)
                        {
                            var offset = (int)(samplesPerPixel * i);
                            var drawableSample = wl.GetRange(offset, Math.Min((int)samplesPerPixel, read)).ToArray();
                            waveFormData[(i + writeOffset) * 2] = drawableSample.Max();
                            waveFormData[((i + writeOffset) * 2) + 1] = drawableSample.Min();
                            drawableSample = null;
                        }

                        wl.Clear();
                        wl = null;
                        iter++;
                    }

                    maxSpan = waveFormData.Max() - waveFormData.Min();
                    Player.Init(waveStream);
                    waveStream.Position = 0L;
                    WaveStream = waveStream;
                    WaveFormData = waveFormData.ToList();
                    waveFormSize = waveFormData.Length;
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    WaveStream = null;
                    WaveFormData = null;
                    waveSize = 0L;
                    waveFormSize = 0;
                    waveDuration = TimeSpan.Zero;
                    File.AppendAllText("error.log", $"{DateTime.Now.ToString("yyyyMMddHHmmss")} [ERROR]: {ex.Message}:\n{ex.StackTrace}\n");
                    MessageBox.Show(ex.Message, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                GC.Collect();

                Dispatcher.Invoke(() =>
                {
                    drawWave = true;
                    UpdateVisuals();
                    btnPlayPause.IsEnabled = (WaveStream != null);
                    callback?.Invoke(WaveStream != null);
                });
            });
            thread.Start();
            while (thread.IsAlive)
                await Task.Delay(10);
        }

        private async void StartRenderPositionLine()
        {
            while (Player.PlaybackState == PlaybackState.Playing)
            {
                if (WaveStream.CurrentTime >= PlayRangeEnd)
                {
                    btnPlayPause.Content = "4";
                    btnPlayPause.Foreground = Brushes.Green;
                    PlayRangeEnded?.Invoke(this, EventArgs.Empty);
                    Player.Stop();
                }

                RenderPositionLine();
                await Task.Delay(5);
            }

            btnPlayPause.Content = "4";
            btnPlayPause.Foreground = Brushes.Green;
            PlayRangeEnded?.Invoke(this, EventArgs.Empty);
            Player.Stop();
        }

        private bool _renderingLine = false;
        public async void RenderPositionLine(bool scrollToPosition = false)
        {
            if (!_renderingLine)
            {
                _renderingLine = true;
                var curPosX = (((double)WaveStream.Position / waveSize) * WaveFormWidth) - ScrollOffset;
                linePos.X1 = curPosX;
                linePos.X2 = curPosX;
                _renderingLine = false;

                txtTime.Content = $"{WaveStream.CurrentTime.ToString(@"hh\:mm\:ss\.fff")} / {WaveStream.TotalTime.ToString(@"hh\:mm\:ss\.fff")}";

                if (scrollToPosition || (!Keyboard.IsKeyDown(Key.LeftCtrl) && Player.PlaybackState == PlaybackState.Playing))
                {
                    if (curPosX < 0 || curPosX > ActualWidth)
                        ScrollOffset += curPosX;
                }
            }
            await Task.Yield();
        }

        private DrawingGroup drawingGroup { get; } = new DrawingGroup();
        protected override void OnRender(DrawingContext drawingContext)
        {
            Render();
            drawingContext.DrawDrawing(drawingGroup);
            base.OnRender(drawingContext);
        }

        private bool taskQueueRunning = false;
        private int renderCounter = 0;
        private async void Render()
        {
            renderCounter++;
            if (!taskQueueRunning)
            {
                taskQueueRunning = true;
                var dc = drawingGroup.Open();
                while (renderCounter > 0)
                {
                    RenderTask(dc);
                    renderCounter--;
                }
                dc.Close();
                taskQueueRunning = false;

                if (WaveStream != null)
                {
                    RenderPositionLine();
                    UpdateSelection();
                }
            }
            await Task.Yield();
        }

        private double oldWidth = double.NaN;
        private void RenderTask(DrawingContext drawingContext)
        {
            if (!drawWave)
                return;

            drawWave = false;

            var bgColor = (SolidColorBrush)Utils.BrushConverter.ConvertFromString(Settings.AudioWaveBackgroundColor);
            bgColor.Freeze();
            drawingContext.DrawRectangle(bgColor, null, new Rect(0, 0, ActualWidth, ActualHeight)); // Draw Background

            if (WaveFormData == null)
                return;

            var sampleSize = waveFormSize / (WaveFormWidth + 1);

            if (!oldWidth.Equals(double.NaN))
                ScrollOffset = ScrollOffset * (ActualWidth / oldWidth);
            else
                ScrollOffset = ScrollOffset;

            var multiplier = (ActualHeight * 0.9) / maxSpan;

            var drawCenter = ((ActualHeight - ((WaveFormData.Average() * 2) * multiplier)) / 2);

            var visibleSample = WaveFormData.GetRange((int)(sampleSize * ScrollOffset), Math.Min((int)(sampleSize * (ActualWidth + 1)), waveFormSize));

            var pen = new Pen((SolidColorBrush)Utils.BrushConverter.ConvertFromString(Settings.AudioWaveColor), 1);
            pen.Freeze();

            var a = new Point(0, drawCenter);
            var b = a;

            for (int i = 0; i < ActualWidth && (sampleSize * i) + sampleSize < visibleSample.Count(); i++)
            {
                var sample = visibleSample.GetRange((int)(sampleSize * i), (int)(sampleSize)).ToArray();
                if (sample.Length > 0)
                {
                    a = new Point(b.X, drawCenter + (-sample.Max() * multiplier));
                    b = new Point(i + 1, drawCenter + (-sample.Min() * multiplier));
                }
                else
                {
                    a = new Point(b.X, drawCenter);
                    b = new Point(i + 1, drawCenter);
                }
                drawingContext.DrawLine(pen, a, b);
            }

            oldWidth = ActualWidth;
        }

        public void UpdateVisuals()
        {
            var selColor = (SolidColorBrush)Utils.BrushConverter.ConvertFromString(Settings.AudioWaveSelectionColor);
            grdSelect.Background = selColor;

            var lnColor = selColor.Color;
            lnColor.A = 255;
            linePos.Stroke = new SolidColorBrush(lnColor);

            var timeBgColor = (Color)ColorConverter.ConvertFromString(Settings.AudioWaveBackgroundColor);
            timeBgColor.A = 220;
            txtTimeBG.Background = new SolidColorBrush(timeBgColor);
            txtTime.Foreground = (SolidColorBrush)Utils.BrushConverter.ConvertFromString(Settings.AudioWaveColor);

            drawWave = true;
            Render();
        }

        private void UpdateSelection()
        {
            var selectionStart = Math.Max(-1, (((double)SelectionStart / waveSize) * WaveFormWidth) - ScrollOffset);
            var selectionEnd = Math.Min(ActualWidth, (((double)SelectionEnd / waveSize) * WaveFormWidth) - ScrollOffset);
            if (selectionEnd - selectionStart > 0)
            {
                grdSelect.Margin = new Thickness(selectionStart, 0, 0, 0);
                grdSelect.Width = selectionEnd - selectionStart;
            }
            else
            {
                grdSelect.Margin = new Thickness(0);
                grdSelect.Width = 0;
            }
        }

        public void Play()
        {
            if (WaveStream != null)
            {
                if (WaveStream.CurrentTime == WaveStream.TotalTime)
                    Play(TimeSpan.Zero);
                else
                    Play(WaveStream.CurrentTime);
            }
        }

        public void Play(TimeSpan start)
        {
            if (WaveStream != null)
                Play(new TimeRange(start, WaveStream.TotalTime));
        }

        public void Play(TimeRange range)
        {
            if (WaveStream != null)
            {
                PlayRangeEnd = range.End;

                WaveStream.CurrentTime = range.Start;

                var curPosX = (((double)WaveStream.Position / waveSize) * WaveFormWidth) - ScrollOffset;
                if (curPosX < 0 || curPosX > ActualWidth)
                    ScrollOffset = (range.Start.TotalSeconds / WaveStream.TotalTime.TotalSeconds) * MaxScroll;

                Player.Play();

                btnPlayPause.Content = ";";
                btnPlayPause.Foreground = Brushes.Blue;

                StartRenderPositionLine();
            }
        }

        public void Pause()
        {
            if (WaveStream != null)
            {
                Player.Pause();
                btnPlayPause.Content = "4";
                btnPlayPause.Foreground = Brushes.Green;
            }
        }

        public void Stop()
        {
            if (WaveStream != null)
            {
                WaveStream = null;
                Render();
                Player.Stop();
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                var mouseX = e.GetPosition(this).X;
                var scrollPercent = (ScrollOffset + mouseX) / WaveFormWidth;
                if (e.Delta < 0)
                {
                    Zoom /= 1.5;
                }
                else
                {
                    Zoom *= 1.5;
                }
                ScrollOffset = (WaveFormWidth * scrollPercent) - mouseX;
            }
            base.OnMouseWheel(e);
        }


        private bool adjustStart = false;
        private bool adjustEnd = false;
        private bool adjustingStart = false;
        private bool adjustingEnd = false;
        private double downSelection = 0.0;

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            downX = e.GetPosition(this).X;
            if (WaveStream != null)
            {
                if (modifierCtrlPressed)
                {
                    if (adjustStart)
                    {
                        adjustingStart = true;
                        downSelection = downX;
                    }
                    else if (adjustEnd)
                    {
                        adjustingEnd = true;
                        downSelection = downX;
                    }
                    else
                        downScroll = ScrollOffset;
                }
                else if (modifierShiftPressed)
                {
                    var curPosX = (3 + downX + ScrollOffset) / WaveFormWidth;
                    var newPosX = (long)(waveSize * curPosX);

                    SelectionStart = (WaveStream.Position < newPosX) ? WaveStream.Position : newPosX;
                    SelectionEnd = (WaveStream.Position > newPosX) ? WaveStream.Position : newPosX;

                    if (SelectionStart != SelectionEnd)
                    {
                        NewSelection?.Invoke(this, EventArgs.Empty);
                    }
                }
                else
                {
                    var curPosX = (downX + ScrollOffset) / WaveFormWidth;
                    var newPosX = (long)(waveSize * curPosX);
                    WaveStream.Position = newPosX;
                    RenderPositionLine();
                }

                Mouse.Capture(this);
            }
            base.OnMouseLeftButtonDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var moveX = e.GetPosition(this).X;
            var diffX = moveX - downX;

            if (Mouse.Captured == this)
            {
                if (modifierCtrlPressed)
                {
                    if (adjustingStart)
                    {
                        var curPosX = ((downSelection + diffX) + ScrollOffset) / WaveFormWidth;
                        var newValue = (long)Math.Max(waveSize * curPosX, 0);
                        if (newValue > SelectionEnd)
                        {
                            adjustingEnd = true;
                            adjustingStart = false;
                            SelectionStart = SelectionEnd;
                        }
                        else
                            SelectionStart = Math.Max(newValue, 0);
                    }
                    else if (adjustingEnd)
                    {
                        var curPosX = ((downSelection + diffX) + ScrollOffset) / WaveFormWidth;
                        var newValue = (long)Math.Max(waveSize * curPosX, 0);
                        if (newValue < SelectionStart)
                        {
                            adjustingStart = true;
                            adjustingEnd = false;
                            SelectionEnd = SelectionStart;
                        }
                        else
                            SelectionEnd = Math.Min(newValue, waveSize);
                    }
                    else
                        ScrollOffset = downScroll - diffX;
                }
                else if (!modifierShiftPressed)
                {
                    var curPosX = (moveX + ScrollOffset) / WaveFormWidth;
                    var newPosX = (long)(waveSize * curPosX);
                    newPosX = newPosX < 0 ? 0 : newPosX >= waveSize ? waveSize - 1 : newPosX;
                    WaveStream.Position = newPosX;
                    RenderPositionLine();
                }
            }
            else if (WaveStream != null)
            {
                var selectionStartPosX = (((double)SelectionStart / waveSize) * WaveFormWidth) - ScrollOffset;
                var selectionEndPosX = (((double)SelectionEnd / waveSize) * WaveFormWidth) - ScrollOffset;

                if (moveX > selectionStartPosX - 3 && moveX < selectionStartPosX + 3)
                {
                    adjustStart = true;
                    return;
                }
                else if (moveX > selectionEndPosX - 3 && moveX < selectionEndPosX + 3)
                {
                    adjustEnd = true;
                    return;
                }

                adjustStart = false;
                adjustEnd = false;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (adjustingStart || adjustingEnd)
            {
                var totLen = waveSize;
                var totTime = WaveStream.TotalTime.TotalSeconds;
                SelectionChanged?.Invoke(this,
                    new TimeRange(
                        ((double)SelectionStart / totLen) * totTime,
                        ((double)SelectionEnd / totLen) * totTime
                    ));
            }

            adjustingStart = false;
            adjustingEnd = false;

            Mouse.Capture(null);
            base.OnMouseLeftButtonUp(e);
        }

        //bool _playToggled = false;
        protected override async void OnMouseEnter(MouseEventArgs e)
        {
            while (IsMouseOver)
            {
                await Task.Delay(10);

                if (Mouse.Captured != this)
                {
                    modifierCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl);
                    modifierShiftPressed = Keyboard.IsKeyDown(Key.LeftShift);
                    if (WaveStream != null)
                    {
                        if (modifierCtrlPressed)
                        {
                            if (adjustStart || adjustEnd)
                                Cursor = Cursors.SizeWE;
                            else
                                Cursor = Cursors.SizeAll;
                        }
                        else if (modifierShiftPressed)
                            Cursor = Cursors.IBeam;
                        else
                            Cursor = Cursors.Arrow;

                        if (Keyboard.IsKeyDown(Key.Escape))
                        {
                            SelectionStart = 0;
                            SelectionEnd = 0;

                            SelectionChanged?.Invoke(this, new TimeRange());
                        }

                        //var spacePressed = Keyboard.IsKeyDown(Key.Space);
                        //if (spacePressed && !_playToggled)
                        //{
                        //    TogglePlay();
                        //}

                        //_playToggled = spacePressed;
                    }
                    else
                        Cursor = Cursors.Arrow;
                }
            }

            modifierCtrlPressed = false;
            modifierShiftPressed = false;

            base.OnMouseEnter(e);
        }

        private TimeSpan TimeFromPosition(long position)
        {
            if (WaveStream != null)
            {
                return TimeSpan.FromMilliseconds((position / (double)waveSize) * waveDuration.TotalMilliseconds);
            }
            return TimeSpan.Zero;
        }

        private void TogglePlay()
        {
            if (btnPlayPause.Content.ToString() == "4")
            {
                if (SelectionEnd > SelectionStart)
                    Play(new TimeRange(TimeFromPosition(SelectionStart), TimeFromPosition(SelectionEnd)));
                else
                    Play(WaveStream.CurrentTime);
            }
            else
                Pause();
        }

        private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            TogglePlay();
        }
    }
}

