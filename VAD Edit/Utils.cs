﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace VADEdit
{
    public static class Utils
    {
        #region network

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

        public static IEnumerable<int> Range(int start, int end, int step = 1)
        {
            for (int i = start; (step > 0 && i < end) || (step < 0 && i > end); i += step)
                yield return i;
        }

        public static IEnumerable<int> Range(int end)
        {
            return Range(0, end);
        }

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
    }
}

#region IValueConverters
namespace VADEdit.Converters
{
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