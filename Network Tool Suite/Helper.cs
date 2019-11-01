using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace Network_Tool_Suite
{
    public static class Helper
    {
        public static unsafe void IntToByte(int num, byte[] bytes)
        {
            fixed (byte* pBytes = &bytes[0])
            {
                *(int*)pBytes = num;
            }
        }
        public static unsafe int ByteToInt(byte[] bytes)
        {
            fixed (byte* pBytes = &bytes[0])
            {
                return *(int*)pBytes;
            }
        }

        
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(ref Win32Point pt);
        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public int X;
            public int Y;
        };
        public static Point GetMousePosition()
        {
            var w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);
            return new Point(w32Mouse.X, w32Mouse.Y);
        }

    }
}
