using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace VADEdit
{
    public static class Utils
    {
        #region BrushConverter
        public static BrushConverter BrushConverter { get; } = new BrushConverter();
        #endregion

        #region Network

        public static bool IsNetworkAvailable()
        {
            try
            {
                using (var client = new WebClient())
                using (client.OpenRead("http://clients3.google.com/generate_204"))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region Generator
        public static IEnumerable<int> Range(int start, int end, int step = 1)
        {
            for (int i = start; (step > 0 && i < end) || (step < 0 && i > end); i += step)
                yield return i;
        }

        public static IEnumerable<int> Range(int end)
        {
            return Range(0, end);
        }
        #endregion

        #region Assert
        public static void Assert(bool condition, Exception exception = null)
        {
            if (condition)
            {
                if (exception == null)
                    exception = new Exception($"Assertion Error");
                else
                {
                    typeof(Exception)
                        .GetField("_innerException", BindingFlags.NonPublic | BindingFlags.Instance)
                        .SetValue(exception, new Exception($"Assertion Error"));
                }
                throw exception;
            }
        }

        public static void AssertEqual(object obj1, object obj2, Exception exception = null)
        {
            if (obj1 != obj2)
            {
                if (exception == null)
                    exception = new Exception($"Assertion Error: {obj1} != {obj2}");
                else
                {
                    typeof(Exception)
                        .GetField("_innerException", BindingFlags.NonPublic | BindingFlags.Instance)
                        .SetValue(exception, new Exception($"Assertion Error: {obj1} != {obj2}"));
                }
                throw exception;
            }
        }
        #endregion

        #region Path
        public static string GetRelativePath(string fromPath, string toPath)
        {
            int fromAttr = GetPathAttribute(fromPath);
            int toAttr = GetPathAttribute(toPath);

            StringBuilder path = new StringBuilder(260); // MAX_PATH
            if (PathRelativePathTo(
                path,
                fromPath,
                fromAttr,
                toPath,
                toAttr) == 0)
            {
                throw new ArgumentException("Paths must have a common prefix");
            }
            return path.ToString();
        }

        private static int GetPathAttribute(string path)
        {
            DirectoryInfo di = new DirectoryInfo(path);
            if (di.Exists)
            {
                return FILE_ATTRIBUTE_DIRECTORY;
            }

            FileInfo fi = new FileInfo(path);
            if (fi.Exists)
            {
                return FILE_ATTRIBUTE_NORMAL;
            }

            throw new FileNotFoundException();
        }

        private const int FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const int FILE_ATTRIBUTE_NORMAL = 0x80;

        [DllImport("shlwapi.dll", SetLastError = true)]
        private static extern int PathRelativePathTo(StringBuilder pszPath,
            string pszFrom, int dwAttrFrom, string pszTo, int dwAttrTo);
        #endregion

        #region Logger
        public static class Logger
        {
            private static bool loggerRunning = false;
            private static Queue<string> logLines = new Queue<string>();
            private static bool appRunning = true;

            public static void Initialize()
            {
                if (loggerRunning)
                    return;

                loggerRunning = true;

                App.Current.Exit += delegate
                {
                    appRunning = false;
                };
                new Thread(LoggerThread).Start();
            }

            public enum Type
            {
                Info,
                Warn,
                Error
            }

            private static void LoggerThread()
            {
                while (appRunning)
                {
                    while (logLines.Count == 0 && appRunning)
                        Thread.Sleep(1000);

                    if (appRunning)
                        File.AppendAllText("app.log", logLines.Dequeue());
                }
            }

            public static void Log(string message, Type type = Type.Info)
            {
                logLines.Enqueue($"{DateTime.Now.ToString("yyyyMMddHHmmss")} [{type.ToString().ToUpper()}]: {message}\n");
            }
        }
        #endregion

        #region Aero Blur
        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        //internal static void Show(MultipleAnswerPage multipleAnswerPage)
        //{
        //    throw new NotImplementedException();
        //}

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        internal enum WindowCompositionAttribute
        {
            // ...
            WCA_ACCENT_POLICY = 19
            // ...
        }

        internal enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_INVALID_STATE = 4
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        internal static void EnableBlur(this Window @this)
        {
            var windowHelper = new WindowInteropHelper(@this);

            var accent = new AccentPolicy();
            var accentStructSize = Marshal.SizeOf(accent);
            accent.AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND;

            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData();
            data.Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY;
            data.SizeOfData = accentStructSize;
            data.Data = accentPtr;

            SetWindowCompositionAttribute(windowHelper.Handle, ref data);

            Marshal.FreeHGlobal(accentPtr);
        }
        #endregion

        #region WPFScreen
        public class WpfScreen
        {
            public static IEnumerable<WpfScreen> AllScreens()
            {
                foreach (Screen screen in Screen.AllScreens)
                {
                    yield return new WpfScreen(screen);
                }
            }

            public static WpfScreen GetScreenFrom(Window window)
            {
                WindowInteropHelper windowInteropHelper = new WindowInteropHelper(window);
                Screen screen = Screen.FromHandle(windowInteropHelper.Handle);
                WpfScreen wpfScreen = new WpfScreen(screen);
                return wpfScreen;
            }

            public static WpfScreen GetScreenFrom(System.Windows.Point point)
            {
                int x = (int)Math.Round(point.X);
                int y = (int)Math.Round(point.Y);

                // are x,y device-independent-pixels ??
                System.Drawing.Point drawingPoint = new System.Drawing.Point(x, y);
                Screen screen = Screen.FromPoint(drawingPoint);
                WpfScreen wpfScreen = new WpfScreen(screen);

                return wpfScreen;
            }

            public static WpfScreen Primary
            {
                get { return new WpfScreen(Screen.PrimaryScreen); }
            }

            private readonly Screen screen;

            internal WpfScreen(Screen screen)
            {
                this.screen = screen;
            }

            public Rect DeviceBounds
            {
                get { return GetRect(screen.Bounds); }
            }

            public Rect WorkingArea
            {
                get { return GetRect(screen.WorkingArea); }
            }

            private Rect GetRect(Rectangle value)
            {
                // should x, y, width, height be device-independent-pixels ??
                return new Rect
                {
                    X = value.X,
                    Y = value.Y,
                    Width = value.Width,
                    Height = value.Height
                };
            }

            public bool IsPrimary
            {
                get { return this.screen.Primary; }
            }

            public string DeviceName
            {
                get { return this.screen.DeviceName; }
            }
        }
        #endregion
    }
}

#region Types
namespace VADEdit.Types
{
    public struct WaveSelectionChangedEventArgs
    {
        public enum SelectionEvent
        {
            New,
            Resize,
            Hide
        }

        public TimeRange TimeRange { get; set; }
        public SelectionEvent Event { get; set; }
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

        public override bool Equals(object obj)
        {
            return this == (TimeRange)obj;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(TimeRange r1, TimeRange r2)
        {
            return (r1.Start == r2.Start) && (r1.End == r2.End);
        }

        public static bool operator !=(TimeRange r1, TimeRange r2)
        {
            return (r1.Start != r2.Start) || (r1.End != r2.End);
        }
    }
}
#endregion

#region IValueConverters
namespace VADEdit.Converters
{
    public class MinusValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return (double)values[0] - (double)values[1];
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class Times2ValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (double)value * 2;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NegateVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((Visibility)value == Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FlippedIconColumnValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((bool)value) ? 1 : 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FlippedContentColumnValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((bool)value) ? 0 : 1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FlippedColumn0WidthValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var flipped = (bool)values[0];
            var icon = (ImageSource)values[1];
            var content = values[2];

            if (flipped)
                return new GridLength(1, (content == null) ? GridUnitType.Auto : GridUnitType.Star);
            else
                return new GridLength(1, (content != null) ? GridUnitType.Auto : GridUnitType.Star);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FlippedColumn1WidthValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var flipped = (bool)values[0];
            var icon = (ImageSource)values[1];
            var content = values[2];

            if (flipped)
                return new GridLength(1, (content != null) ? GridUnitType.Auto : GridUnitType.Star);
            else
                return new GridLength(1, (content == null) ? GridUnitType.Auto : GridUnitType.Star);
        }

        public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ImageButtonContentNullToMarginValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new Thickness((value == null) ? 0 : 5);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BorderClipConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 3 && values[0] is double && values[1] is double && values[2] is CornerRadius)
            {
                var width = (double)values[0];
                var height = (double)values[1];

                if (width < Double.Epsilon || height < Double.Epsilon)
                {
                    return Geometry.Empty;
                }

                var radius = (CornerRadius)values[2];

                var clip = new RectangleGeometry(new Rect(0, 0, width, height), radius.TopLeft, radius.TopLeft);
                clip.Freeze();

                return clip;
            }

            return DependencyProperty.UnsetValue;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class PropertiesToEffectValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var pressed = (bool)values[0];
            var checked_ = (bool)values[1];
            var enabled = (bool)values[2];

            var idleEffect = (Effect)values[3];
            var pressedEffect = (Effect)values[4];
            var checkedEffect = (Effect)values[5];
            var disabledEffect = (Effect)values[6];

            if (!enabled)
                return disabledEffect;
            else if (pressed)
                return pressedEffect;
            else if (checked_)
                return checkedEffect;
            else
                return idleEffect;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
#endregion