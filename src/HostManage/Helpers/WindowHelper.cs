using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace HostManage.Helpers;

public static class WindowHelper
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    private const uint FLASHW_STOP = 0;
    private const uint FLASHW_CAPTION = 0x00000001;
    private const uint FLASHW_TRAY = 0x00000002;
    private const uint FLASHW_ALL = 0x00000003;
    private const uint FLASHW_TIMERNOFG = 0x0000000C;

    public static bool? ShowDialog<TViewModel>(Window window, TViewModel vm)
    {
        try
        {
            if (window == null)
                return null;

            if (vm != null)
            {
                window.DataContext = vm;
            }

            CenterWindow(window);
            return window.ShowDialog();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void CenterWindow(Window window)
    {
        try
        {
            if (window == null)
                return;

            if (Application.Current?.MainWindow != null && Application.Current.MainWindow != window)
            {
                window.Owner = Application.Current.MainWindow;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }
        catch (Exception)
        {
        }
    }

    public static bool FlashWindow(Window window)
    {
        try
        {
            if (window == null)
                return false;

            var helper = new WindowInteropHelper(window);
            IntPtr hWnd = helper.Handle;

            if (hWnd == IntPtr.Zero)
                return false;

            var fInfo = new FLASHWINFO();
            fInfo.cbSize = (uint)Marshal.SizeOf(fInfo);
            fInfo.hwnd = hWnd;
            fInfo.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG;
            fInfo.uCount = 3;
            fInfo.dwTimeout = 0;

            return FlashWindowEx(ref fInfo);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
