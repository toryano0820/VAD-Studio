using NAudio.Wave;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VADEdit
{
    /// <summary>
    /// Interaction logic for WaveFormView.xaml
    /// </summary>
    public class WaveFormView : Canvas
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
            DependencyProperty.Register("ScrollOffset", typeof(double), typeof(WaveFormView), new PropertyMetadata(0.0, (o, e) =>
            {
                (o as WaveFormView).InvalidateVisual();
            }, (o, e) =>
            {
                var @this = o as WaveFormView;
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
            DependencyProperty.Register("Zoom", typeof(double), typeof(WaveFormView), new PropertyMetadata(1.0, (o, e) =>
            {
                (o as WaveFormView).InvalidateVisual();
            }, (o, e) =>
            {
                var @this = o as WaveFormView;
                var value = (double)e;
                return (value < @this.MinZoom) ? @this.MinZoom : (value > @this.MaxZoom) ? @this.MaxZoom : value;
            }));


        public double SelectionStart
        {
            get { return (double)GetValue(SelectionStartProperty); }
            set { SetValue(SelectionStartProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SelectionStart.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectionStartProperty =
            DependencyProperty.Register("SelectionStart", typeof(double), typeof(WaveFormView), new PropertyMetadata(0.0, null, (o, e) =>
            {
                var @this = o as WaveFormView;
                var value = (double)e;

                if (value > @this.SelectionEnd)
                    @this.SelectionEnd = value;

                return value;
            }));


        public double SelectionEnd
        {
            get { return (double)GetValue(SelectionEndProperty); }
            set { SetValue(SelectionEndProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SelectionEnd.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectionEndProperty =
            DependencyProperty.Register("SelectionEnd", typeof(double), typeof(WaveFormView), new PropertyMetadata(0.0, null, (o, e) =>
            {
                var @this = o as WaveFormView;
                var value = (double)e;

                if (value < @this.SelectionStart)
                    @this.SelectionStart = value;

                return value;
            }));

        public bool HaveSelection
        {
            get { return (bool)GetValue(HaveSelectionProperty); }
            set { SetValue(HaveSelectionProperty, value); }
        }

        // Using a DependencyProperty as the backing store for HaveSelection.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HaveSelectionProperty =
            DependencyProperty.Register("HaveSelection", typeof(bool), typeof(WaveFormView), new PropertyMetadata(false));



        public double WaveFormWidth
        {
            get
            {
                return (ActualWidth) * Zoom;
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
        public float[] WaveFormData { get; private set; }
        public TimeSpan PlayRangeEnd { get; set; } = TimeSpan.Zero;

        public WaveOut Player { get; } = new WaveOut()
        {
            NumberOfBuffers = 112,
            DesiredLatency = 10
        };

        private TextBlock timeView { get; } = new TextBlock()
        {
            TextAlignment = TextAlignment.Center,
            Padding = new Thickness(4)
        };

        private Line linePos { get; } = new Line()
        {
            StrokeThickness = 1
        };
        private double maxSpan = 0.0;
        private double downX = 0.0;
        private double downScroll = 0.0;
        private bool modifierCtrlPressed = false;
        private bool modifierShiftPressed = false;

        public WaveFormView()
        {
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
            Background = Brushes.Transparent;
            Focusable = true;

            Loaded += async delegate
            {
                linePos.X1 = 0;
                linePos.X2 = 0;

                timeView.Text = "00:00:00.00";

                Children.Add(linePos);
                Children.Add(timeView);

                await Task.Delay(100);
                
                var timeBgColor = ((SolidColorBrush)(new BrushConverter()).ConvertFromString(Settings.AudioWaveBackgroundColor)).Color;
                timeBgColor.A = 200;
                timeView.Background = new SolidColorBrush(timeBgColor);
                timeView.Foreground = (SolidColorBrush)(new BrushConverter()).ConvertFromString(Settings.AudioWaveColor);
                SetLeft(timeView, 0);
                SetTop(timeView, ActualHeight - timeView.ActualHeight);

                linePos.Visibility = Visibility.Visible;
                timeView.Visibility = Visibility.Visible;
            };

            SizeChanged += delegate
            {
                linePos.Y1 = 0;
                linePos.Y2 = ActualHeight;
            };
        }

        public async Task SetWaveStream(WaveStream waveStream, Action<bool> callback = null)
        {
            WaveStream = null;
            WaveFormData = null;
            InvalidateVisual();

            var thread = new Thread(() =>
            {
                try
                {
                    if (waveStream.WaveFormat.Channels != 1 || waveStream.WaveFormat.SampleRate != 16000)
                    {
                        throw new FileFormatException("Input should be 16kHz Mono WAV file.");
                    }
                    var wave = new WaveChannel32(waveStream);

                    if (wave == null)
                        return;

                    waveStream.Position = 0L;

                    int sampleSize = 0;

                    sampleSize = (from i in Utils.Range(1, 512)
                                  let align = wave.WaveFormat.BlockAlign * (double)i
                                  let v = wave.Length / align
                                  where v == (int)v
                                  select (int)align).Max();

                    var bufferSize = (int)(wave.Length / (double)sampleSize);
                    int read = 0;

                    var waveSize = wave.Length;

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
                    WaveFormData = waveFormData;

                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    WaveFormData = null;
                    File.AppendAllText("error.log", $"{DateTime.Now.ToString("yyyyMMddHHmmss")} [ERROR]: {ex.Message}:\n{ex.StackTrace}\n");
                    MessageBox.Show(ex.Message, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                GC.Collect();

                Dispatcher.Invoke(() =>
                {
                    InvalidateVisual();
                    callback?.Invoke(WaveStream != null);
                });
            });
            thread.Start();
            while (thread.IsAlive)
                await Task.Delay(10);
        }

        private void StartRenderPositionLine()
        {
            new Thread(() =>
            {
                try
                {
                    while (Player.PlaybackState == PlaybackState.Playing)
                    {
                        if (WaveStream.CurrentTime >= PlayRangeEnd)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                PlayRangeEnded?.Invoke(this, EventArgs.Empty);
                            });
                            Player.Stop();
                        }
                        Dispatcher.Invoke(() =>
                        {
                            RenderPositionLine();
                        });
                    }

                    //WaveStream.CurrentTime = PlayRangeEnd;
                    //Dispatcher.Invoke(() =>
                    //{
                    //    RenderPositionLine();
                    //});
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    File.AppendAllText("error.log", $"{DateTime.Now.ToString("yyyyMMddHHmmss")} [ERROR]: {ex.Message}:\n{ex.StackTrace}\n");
                    MessageBox.Show(ex.Message, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }).Start();
        }

        internal bool _renderingLine = false;
        public void RenderPositionLine(bool scrollToPosition = false)
        {
            if (!_renderingLine)
            {
                _renderingLine = true;
                var curPosX = (((double)WaveStream.Position / WaveStream.Length) * WaveFormWidth) - ScrollOffset;
                linePos.X1 = curPosX;
                linePos.X2 = curPosX;
                _renderingLine = false;

                timeView.Text = $"{WaveStream.CurrentTime.ToString(@"hh\:mm\:ss\.fff")} / {WaveStream.TotalTime.ToString(@"hh\:mm\:ss\.fff")}";

                if (scrollToPosition || (!Keyboard.IsKeyDown(Key.LeftCtrl) && Player.PlaybackState == PlaybackState.Playing))
                {
                    if (curPosX < 0 || curPosX > ActualWidth)
                        ScrollOffset += curPosX;
                }
            }
        }

        private double oldWidth = double.NaN;
        protected override void OnRender(DrawingContext drawingContext)
        {
            var bgColor = (SolidColorBrush)(new BrushConverter()).ConvertFromString(Settings.AudioWaveBackgroundColor);
            drawingContext.DrawRectangle(bgColor, null, new Rect(0, 0, ActualWidth, ActualHeight)); // Draw Background

            if (WaveFormData != null)
            {
                var waveSize = WaveFormData.Length;
                var sampleSize = waveSize / (WaveFormWidth + 1);

                if (!oldWidth.Equals(double.NaN))
                    ScrollOffset = ScrollOffset * (ActualWidth / oldWidth);
                else
                    ScrollOffset = ScrollOffset;

                var multiplier = (ActualHeight * 0.9) / maxSpan;

                var drawCenter = ((ActualHeight - ((WaveFormData.Average() * 2) * multiplier)) / 2);

                var visibleSample = WaveFormData.ToList().GetRange((int)(sampleSize * ScrollOffset), Math.Min((int)(sampleSize * (ActualWidth + 1)), waveSize));

                var pen = new Pen((SolidColorBrush)(new BrushConverter()).ConvertFromString(Settings.AudioWaveColor), 1);

                for (int i = 0; i < ActualWidth && (sampleSize * i) + sampleSize < visibleSample.Count(); i++)
                {
                    var sample = visibleSample.GetRange((int)(sampleSize * i), (int)(sampleSize)).ToArray();

                    if (sample.Length > 0)
                    {
                        drawingContext.DrawLine(
                            pen,
                            new Point(i + 1, drawCenter + (-sample.Max() * multiplier)),
                            new Point(i + 1, drawCenter + (-sample.Min() * multiplier)));
                    }

                    sample = null;
                }

                visibleSample.Clear();
                visibleSample = null;

                var selectionStart = Math.Max(-1, ((SelectionStart / WaveStream.Length) * WaveFormWidth) - ScrollOffset);
                var selectionEnd = Math.Min(ActualWidth, ((SelectionEnd / WaveStream.Length) * WaveFormWidth) - ScrollOffset);
                var selColor = (SolidColorBrush)new BrushConverter().ConvertFromString(Settings.AudioWaveSelectionColor);
                if (selectionEnd - selectionStart >= 0)
                    drawingContext.DrawRectangle(selColor, null, new Rect(selectionStart, 0, selectionEnd - selectionStart, ActualHeight)); // Draw Selection
                var lnColor = selColor.Color;
                lnColor.A = 255;
                linePos.Stroke = new SolidColorBrush(lnColor);

                GC.Collect();

                oldWidth = ActualWidth;
                RenderPositionLine();

                var timeBgColor = bgColor.Color;
                timeBgColor.A = 200;
                timeView.Background = new SolidColorBrush(timeBgColor);
                timeView.Foreground = (SolidColorBrush)(new BrushConverter()).ConvertFromString(Settings.AudioWaveColor);
                SetLeft(timeView, 0);
                SetTop(timeView, ActualHeight - timeView.ActualHeight);
            }

            base.OnRender(drawingContext);
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

                var curPosX = (((double)WaveStream.Position / WaveStream.Length) * WaveFormWidth) - ScrollOffset;
                if (curPosX < 0 || curPosX > ActualWidth)
                    ScrollOffset = (range.Start.TotalSeconds / WaveStream.TotalTime.TotalSeconds) * MaxScroll;

                Player.Play();

                StartRenderPositionLine();
            }
        }

        public void Pause()
        {
            if (WaveStream != null)
                Player.Pause();
        }

        public void Stop()
        {
            if (WaveStream != null)
            {
                WaveStream = null;
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
                    var newPosX = (long)(WaveStream.Length * curPosX);

                    SelectionStart = (WaveStream.Position < newPosX) ? WaveStream.Position : newPosX;
                    SelectionEnd = (WaveStream.Position > newPosX) ? WaveStream.Position : newPosX;

                    if (SelectionStart != SelectionEnd)
                    {
                        NewSelection?.Invoke(this, EventArgs.Empty);
                    }

                    InvalidateVisual();
                }
                else
                {
                    var curPosX = (downX + ScrollOffset) / WaveFormWidth;
                    var newPosX = (long)(WaveStream.Length * curPosX);
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
                        SelectionStart = Math.Max(WaveStream.Length * curPosX, 0);
                        InvalidateVisual();
                    }
                    else if (adjustingEnd)
                    {
                        var curPosX = ((downSelection + diffX) + ScrollOffset) / WaveFormWidth;
                        SelectionEnd = Math.Min(WaveStream.Length * curPosX, WaveStream.Length);
                        InvalidateVisual();
                    }
                    else
                        ScrollOffset = downScroll - diffX;
                }
                else if (!modifierShiftPressed)
                {
                    var curPosX = (moveX + ScrollOffset) / WaveFormWidth;
                    var newPosX = (long)(WaveStream.Length * curPosX);
                    newPosX = newPosX < 0 ? 0 : newPosX >= WaveStream.Length ? WaveStream.Length - 1 : newPosX;
                    WaveStream.Position = newPosX;
                    RenderPositionLine();
                }
            }
            else if (WaveStream != null)
            {
                var selectionStartPosX = ((SelectionStart / WaveStream.Length) * WaveFormWidth) - ScrollOffset;
                var selectionEndPosX = ((SelectionEnd / WaveStream.Length) * WaveFormWidth) - ScrollOffset;

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
                var totLen = WaveStream.Length;
                var totTime = WaveStream.TotalTime.TotalSeconds;
                SelectionChanged?.Invoke(this,
                    new TimeRange(
                        (SelectionStart / totLen) * totTime,
                        (SelectionEnd / totLen) * totTime
                    ));
            }

            adjustingStart = false;
            adjustingEnd = false;

            Mouse.Capture(null);
            base.OnMouseLeftButtonUp(e);
        }

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
                    }
                    else
                        Cursor = Cursors.Arrow;
                }
            }

            modifierCtrlPressed = false;
            modifierShiftPressed = false;

            base.OnMouseEnter(e);
        }
    }

    public struct TimeRange
    {
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }

        public TimeRange(TimeSpan from, TimeSpan to)
        {
            Start = from;
            End = to;
        }

        public TimeRange(double secFrom, double secTo) :
            this(TimeSpan.FromSeconds(secFrom), TimeSpan.FromSeconds(secTo))
        { }

        public override string ToString()
        {
            return $"{Start.ToString(@"hh\:mm\:ss\.fff")} - {End.ToString(@"hh\:mm\:ss\.fff")}";
        }
    }
}

