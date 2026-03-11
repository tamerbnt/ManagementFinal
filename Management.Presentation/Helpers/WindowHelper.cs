using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Management.Presentation.Helpers
{
    public static class WindowHelper
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const int DWMSBT_MAINWINDOW = 2; // Mica
        private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic

        public static void EnableMica(Window window)
        {
            var interopHelper = new WindowInteropHelper(window);
            IntPtr hwnd = interopHelper.Handle;

            int trueValue = 1;
            int backdropType = DWMSBT_MAINWINDOW;

            // Set system backdrop to Mica
            DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
        }
    }
}
