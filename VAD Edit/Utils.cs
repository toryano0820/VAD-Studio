using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace VADEdit
{
    public static class Utils
    {
        public static bool IsNetworkAvailable(long minimumSpeed = 0)
        {
            if (NetworkInterface.GetIsNetworkAvailable())
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if ((ni.OperationalStatus != OperationalStatus.Up) ||
                        (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) ||
                        (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel))
                        continue;
                    if (ni.Speed < minimumSpeed)
                        continue;
                    if ((ni.Description.IndexOf("virtual", StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (ni.Name.IndexOf("virtual", StringComparison.OrdinalIgnoreCase) >= 0))
                        continue;
                    if (ni.Description.Equals("Microsoft Loopback Adapter", StringComparison.OrdinalIgnoreCase))
                        continue;
                    return true;
                }
            }
            return false;
        }

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
