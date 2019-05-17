using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VADEdit
{
    /// <summary>
    /// Interaction logic for WaveFormView.xaml
    /// </summary>
    public class WaveFormView : UserControl
    {
        public event EventHandler SelectionAdjusted;
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


        public Brush SelectionBrush
        {
            get { return (Brush)GetValue(SelectionBrushProperty); }
            set { SetValue(SelectionBrushProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SelectionBrush.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectionBrushProperty =
            DependencyProperty.Register("SelectionBrush", typeof(Brush), typeof(WaveFormView), new PropertyMetadata((new BrushConverter()).ConvertFromString("#550000FF")));


        public bool AllowSelectionChange
        {
            get { return (bool)GetValue(AllowSelectionChangeProperty); }
            set { SetValue(AllowSelectionChangeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for AllowSelectionChange.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty AllowSelectionChangeProperty =
            DependencyProperty.Register("AllowSelectionChange", typeof(bool), typeof(WaveFormView), new PropertyMetadata(false));




        public double MaxScroll
        {
            get
            {
                return (ActualWidth * Zoom) - ActualWidth;
            }
        }

        public WaveStream WaveStream { get; private set; }

        public double MaxZoom { get; private set; } = 1.0;
        public double MinZoom { get; private set; } = 1.0;

        public float[] WaveFormData { get; private set; }

        public WaveOut Player { get; } = new WaveOut()
        {
            NumberOfBuffers = 112,
            DesiredLatency = 10
        };
        Line linePos = new Line()
        {
            StrokeThickness = 1,
            Stroke = Brushes.Blue
        };

        double maxSpan = 0.0;
        double downX = 0.0;
        double downScroll = 0.0;

        TimeSpan playRangeEnd = TimeSpan.Zero;

        public WaveFormView()
        {
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
            Background = Brushes.Transparent;
            Focusable = true;

            Loaded += delegate
            {
                linePos.X1 = 0;
                linePos.X2 = 0;
                linePos.Visibility = Visibility.Visible;

                Cursor = Cursors.IBeam;

                Content = linePos;
            };

            SizeChanged += delegate
            {
                linePos.Y1 = 0;
                linePos.Y2 = ActualHeight;
            };
        }

        public void SetWaveStream(WaveStream waveStream, Action callback = null)
        {
            new Thread(() =>
            {
                try
                {
                    WaveStream = waveStream;

                    var wave = new WaveChannel32(waveStream);

                    if (wave.Equals(null))
                        return;

                    waveStream.Position = 0L;

                    int sampleSize = wave.WaveFormat.BlockAlign;
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
                        AllowSelectionChange = false;
                    });

                    var maxWidth = Math.Min(WpfScreen.AllScreens().OrderByDescending(s => s.WorkingArea.Width).First().WorkingArea.Width * MaxZoom, waveSize);

                    var iter = 0;
                    WaveFormData = new float[(int)(maxWidth * 2)];
                    var c = 0;
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
                        rawWaveArray = null;

                        var samplesPerPixel = (rwaCount / (maxWidth / sampleSize));

                        var writeOffset = (int)((maxWidth / sampleSize) * iter);
                        for (int i = 0; i < (int)(maxWidth / sampleSize); i++)
                        {
                            var offset = (int)(samplesPerPixel * i);
                            var drawableSample = wl.GetRange(offset, Math.Min((int)samplesPerPixel, read)).ToArray();
                            WaveFormData[(i + writeOffset) * 2] = drawableSample.Max();
                            WaveFormData[((i + writeOffset) * 2) + 1] = drawableSample.Min();
                            drawableSample = null;
                            c += 2;
                        }

                        wl = null;
                        iter++;
                    }

                    maxSpan = WaveFormData.Max() - WaveFormData.Min();
                    Player.Init(waveStream);
                    waveStream.Position = 0L;
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    File.AppendAllText("error.log", $"{DateTime.Now.ToString("yyyyMMddHHmmss")} [ERROR]: {ex.Message}:\n{ex.StackTrace}\n");
                    MessageBox.Show("error occured, check error.log");
                }

                Dispatcher.Invoke(() =>
                {
                    InvalidateVisual();
                    callback?.Invoke();
                });
            }).Start();
        }

        private void StartRenderPositionLine()
        {
            new Thread(() =>
            {
                try
                {
                    while (Player.PlaybackState == PlaybackState.Playing)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            RenderPositionLine();
                        });
                        if (WaveStream.CurrentTime > playRangeEnd)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                PlayRangeEnded?.Invoke(this, EventArgs.Empty);
                            });
                            Player.Stop();
                        }
                        Thread.Sleep(1);
                    }
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    File.AppendAllText("error.log", $"{DateTime.Now.ToString("yyyyMMddHHmmss")} [ERROR]: {ex.Message}:\n{ex.StackTrace}\n");
                    MessageBox.Show("error occured, check error.log");
                }
            }).Start();
        }

        internal bool _renderingLine = false;
        public void RenderPositionLine(bool scrollToPosition = false)
        {
            if (!_renderingLine)
            {
                _renderingLine = true;
                var curPosX = (((double)WaveStream.Position / WaveStream.Length) * (ActualWidth * Zoom)) - ScrollOffset;
                linePos.X1 = curPosX;
                linePos.X2 = curPosX;
                _renderingLine = false;

                if (scrollToPosition || (!Keyboard.IsKeyDown(Key.LeftCtrl) && Player.PlaybackState == PlaybackState.Playing))
                {
                    if (curPosX < 0 || curPosX > ActualWidth)
                        ScrollOffset += curPosX;
                }
            }
        }

        double oldWidth = double.NaN;
        protected override void OnRender(DrawingContext drawingContext)
        {
            drawingContext.DrawRectangle(Brushes.Black, null, new Rect(0, 0, ActualWidth, ActualHeight)); // Draw Background

            if (WaveFormData != null)
            {
                var waveSize = WaveFormData.Length;
                var sampleSize = (waveSize / (ActualWidth * Zoom));

                if (!oldWidth.Equals(double.NaN))
                    ScrollOffset = ScrollOffset * (ActualWidth / oldWidth);
                else
                    ScrollOffset = ScrollOffset;

                var multiplier = (ActualHeight * 0.8) / maxSpan;

                var drawCenter = ((ActualHeight - ((WaveFormData.Average() * 2) * multiplier)) / 2);

                var visibleSample = WaveFormData.ToList().GetRange((int)(sampleSize * ScrollOffset), Math.Min((int)(sampleSize * ActualWidth), waveSize));

                for (int i = 0; i < ActualWidth && (sampleSize * i) + sampleSize < visibleSample.Count(); i++)
                {
                    var sample = visibleSample.GetRange((int)(sampleSize * i), (int)(sampleSize)).ToArray();

                    if (sample.Length > 0)
                    {
                        drawingContext.DrawLine(
                            new Pen(Brushes.Yellow, 1),
                            new Point(i, drawCenter + (-sample.Max() * multiplier)),
                            new Point(i, drawCenter + (-sample.Min() * multiplier)));
                    }
                    sample = null;
                }

                var selectionStart = Math.Max(0, ((SelectionStart / WaveStream.Length) * (ActualWidth * Zoom)) - ScrollOffset);
                var selectionEnd = Math.Min(ActualWidth, ((SelectionEnd / WaveStream.Length) * (ActualWidth * Zoom)) - ScrollOffset);
                if (selectionEnd - selectionStart >= 0)
                    drawingContext.DrawRectangle(SelectionBrush, null, new Rect(selectionStart, 0, selectionEnd - selectionStart, ActualHeight)); // Draw Selection

                oldWidth = ActualWidth;
                RenderPositionLine();
            }

            base.OnRender(drawingContext);
        }

        public void Play()
        {
            if (!WaveStream.Equals(null))
            {
                if (WaveStream.CurrentTime == WaveStream.TotalTime)
                    Play(TimeSpan.Zero);
                else
                    Play(WaveStream.CurrentTime);
            }
        }

        public void Play(TimeSpan start)
        {
            if (!WaveStream.Equals(null))
                Play(new TimeRange(start, WaveStream.TotalTime));
        }

        public void Play(TimeRange range)
        {
            if (!WaveStream.Equals(null))
            {
                playRangeEnd = range.To;

                WaveStream.CurrentTime = range.From;

                var curPosX = (((double)WaveStream.Position / WaveStream.Length) * (ActualWidth * Zoom)) - ScrollOffset;
                if (curPosX < 0 || curPosX > ActualWidth)
                    ScrollOffset = (range.From.TotalSeconds / WaveStream.TotalTime.TotalSeconds) * MaxScroll;

                Player.Play();

                StartRenderPositionLine();
            }
        }

        public void Pause()
        {
            if (!WaveStream.Equals(null))
                Player.Pause();
        }

        public void Stop()
        {
            if (!WaveStream.Equals(null))
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
                var scrollPercent = (ScrollOffset + mouseX) / (ActualWidth * Zoom);
                if (e.Delta < 0)
                {
                    Zoom /= 1.5;
                }
                else
                {
                    Zoom *= 1.5;
                }
                ScrollOffset = ((ActualWidth * Zoom) * scrollPercent) - mouseX;
            }
            base.OnMouseWheel(e);
        }

        bool adjustingStart = false;
        bool adjustingEnd = false;
        double downSelection = 0.0;

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            downX = e.GetPosition(this).X;
            if (modifierPressed)
            {
                if (adjustStart)
                {
                    adjustingStart = true;
                    downSelection = downX;
                    Mouse.Capture(this);
                }
                else if (adjustEnd)
                {
                    adjustingEnd = true;
                    downSelection = downX;
                    Mouse.Capture(this);
                }
                else
                    downScroll = ScrollOffset;

                Mouse.Capture(this);
            }
            else if (WaveFormData.Length > 0)
            {
                var curPosX = (downX + ScrollOffset) / (int)(ActualWidth * Zoom);
                WaveStream.Position = (long)(WaveStream.Length * curPosX);
                RenderPositionLine();
            }
            base.OnMouseLeftButtonDown(e);
        }

        bool adjustStart = false;
        bool adjustEnd = false;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var moveX = e.GetPosition(this).X;
            var diffX = moveX - downX;
            modifierPressed = Keyboard.IsKeyDown(Key.LeftCtrl);

            if (Mouse.Captured == this)
            {
                if (modifierPressed)
                {
                    if (adjustingStart)
                    {
                        var curPosX = ((downSelection + diffX) + ScrollOffset) / (int)(ActualWidth * Zoom);
                        SelectionStart = Math.Max(WaveStream.Length * curPosX, 0);
                        InvalidateVisual();
                    }
                    else if (adjustingEnd)
                    {
                        var curPosX = ((downSelection + diffX) + ScrollOffset) / (int)(ActualWidth * Zoom);
                        SelectionEnd = Math.Min(WaveStream.Length * curPosX, WaveStream.Length);
                        InvalidateVisual();
                    }
                    else
                        ScrollOffset = downScroll - diffX;
                }
                else
                    Mouse.Capture(null);
            }
            else if (WaveStream != null)
            {
                if (AllowSelectionChange)
                {
                    var selectionStartPosX = ((SelectionStart / WaveStream.Length) * (ActualWidth * Zoom)) - ScrollOffset;
                    var selectionEndPosX = ((SelectionEnd / WaveStream.Length) * (ActualWidth * Zoom)) - ScrollOffset;

                    if (moveX > selectionStartPosX - 3 && moveX < selectionStartPosX + 3)
                    {
                        adjustStart = true;
                        if (modifierPressed)
                            Cursor = Cursors.SizeWE;
                        return;
                    }
                    else if (moveX > selectionEndPosX - 3 && moveX < selectionEndPosX + 3)
                    {
                        adjustEnd = true;
                        if (modifierPressed)
                            Cursor = Cursors.SizeWE;
                        return;
                    }
                }

                adjustStart = false;
                adjustEnd = false;
                if (modifierPressed)
                    Cursor = Cursors.SizeAll;
                else
                    Cursor = Cursors.IBeam;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (adjustingStart || adjustingEnd)
            {
                SelectionAdjusted?.Invoke(this, EventArgs.Empty);
            }

            adjustingStart = false;
            adjustingEnd = false;
            adjustStart = false;
            adjustEnd = false;

            Mouse.Capture(null);
            base.OnMouseLeftButtonUp(e);
        }

        IInputElement lastKeyboardFocus = null;
        protected override void OnMouseEnter(MouseEventArgs e)
        {
            lastKeyboardFocus = Keyboard.FocusedElement;
            Keyboard.Focus(this);
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            Keyboard.Focus(lastKeyboardFocus);
            base.OnMouseLeave(e);
        }

        bool modifierPressed = false;
        protected override void OnKeyDown(KeyEventArgs e)
        {
            modifierPressed = Keyboard.IsKeyDown(Key.LeftCtrl);
            if (modifierPressed && WaveStream != null)
            {
                if (adjustStart || adjustEnd)
                    Cursor = Cursors.SizeWE;
                else
                    Cursor = Cursors.SizeAll;
            }
            else
                Cursor = Cursors.IBeam;
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            modifierPressed = false;
            Cursor = Cursors.IBeam;
            base.OnKeyUp(e);
        }
    }

    public struct TimeRange
    {
        public TimeSpan From { get; set; }
        public TimeSpan To { get; set; }

        public TimeRange(TimeSpan from, TimeSpan to)
        {
            From = from;
            To = to;
        }

        public override string ToString()
        {
            return $"{From.ToString(@"hh\:mm\:ss\.fff")} - {To.ToString(@"hh\:mm\:ss\.fff")}";
        }
    }
}

