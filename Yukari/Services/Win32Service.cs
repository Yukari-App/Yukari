using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;

namespace Yukari.Services
{
    internal class Win32WindowService
    {
        private static WinProc newWndProc = null;
        private static nint oldWndProc = nint.Zero;

        private POINT? minWindowSize = null;
        private POINT? maxWindowSize = null;

        private readonly Window window;

        public Win32WindowService(Window window)
        {
            this.window = window;
        }

        public int GetSystemDPI()
        {
            return GetDpiForWindow(GetWindowHandleForCurrentWindow(window));
        }

        public void SetWindowMinMaxSize(POINT? minWindowSize = null, POINT? maxWindowSize = null)
        {
            this.minWindowSize = minWindowSize;
            this.maxWindowSize = maxWindowSize;

            var hwnd = GetWindowHandleForCurrentWindow(window);

            newWndProc = new WinProc(WndProc);
            oldWndProc = SetWindowLongPtr(hwnd, WindowLongIndexFlags.GWL_WNDPROC, newWndProc);
        }

        private static nint GetWindowHandleForCurrentWindow(object target) =>
            WinRT.Interop.WindowNative.GetWindowHandle(target);

        private nint WndProc(nint hWnd, WindowMessage Msg, nint wParam, nint lParam)
        {
            switch (Msg)
            {
                case WindowMessage.WM_GETMINMAXINFO:
                    var dpi = GetDpiForWindow(hWnd);
                    var scalingFactor = (float)dpi / 96;

                    var minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                    if (minWindowSize != null)
                    {
                        minMaxInfo.ptMinTrackSize.x = (int)(minWindowSize.Value.x * scalingFactor);
                        minMaxInfo.ptMinTrackSize.y = (int)(minWindowSize.Value.y * scalingFactor);
                    }
                    if (maxWindowSize != null)
                    {
                        minMaxInfo.ptMaxTrackSize.x = (int)(maxWindowSize.Value.x * scalingFactor);
                        minMaxInfo.ptMaxTrackSize.y = (int)(maxWindowSize.Value.y * scalingFactor);
                    }

                    Marshal.StructureToPtr(minMaxInfo, lParam, true);
                    break;
            }
            return CallWindowProc(oldWndProc, hWnd, Msg, wParam, lParam);
        }

        private nint SetWindowLongPtr(nint hWnd, WindowLongIndexFlags nIndex, WinProc newProc)
        {
            if (nint.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, newProc);
            else
                return new nint(SetWindowLong32(hWnd, nIndex, newProc));
        }

        internal struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [DllImport("User32.dll")]
        internal static extern int GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        internal static extern int SetWindowLong32(IntPtr hWnd, WindowLongIndexFlags nIndex, WinProc newProc);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        internal static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, WindowLongIndexFlags nIndex, WinProc newProc);

        [DllImport("user32.dll")]
        internal static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, WindowMessage Msg, IntPtr wParam, IntPtr lParam);

        internal delegate IntPtr WinProc(IntPtr hWnd, WindowMessage Msg, IntPtr wParam, IntPtr lParam);

        [Flags]
        internal enum WindowLongIndexFlags : int
        {
            GWL_WNDPROC = -4,
        }

        internal enum WindowMessage : int
        {
            WM_GETMINMAXINFO = 0x0024,
        }
    }
}