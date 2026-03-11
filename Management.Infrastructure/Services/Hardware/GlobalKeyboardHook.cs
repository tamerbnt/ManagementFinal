using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
// using System.Windows.Forms; // Removed dependency, using manual mapping or int casting

namespace Management.Infrastructure.Services.Hardware
{
    public class GlobalKeyboardHook : IDisposable
    {
        public event Action<string>? BarcodeScanned;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

        private readonly LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private readonly StringBuilder _buffer = new StringBuilder();
        private DateTime _lastKeystroke = DateTime.MinValue;

        public GlobalKeyboardHook()
        {
            _proc = HookCallback;
            _hookID = SetHook(_proc);
        }

        public void Dispose()
        {
            UnhookWindowsHookEx(_hookID);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule!)
            {
                // Injecting hook into the module instance. 
                // Robustness Fix: Use GetModuleHandle(null) to get current process handle safely.
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(null), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                
                // Timestamp check for debounce/timeout (optional but good)
                if ((DateTime.Now - _lastKeystroke).TotalMilliseconds > 200) // 200ms Scanner inter-character timeout
                {
                   // If too slow, it's manually typed or a new scan -> Clear buffer?
                   // No, actually scanners are fast. If huge gap, clear buffer.
                   if (_buffer.Length > 0 && (DateTime.Now - _lastKeystroke).TotalMilliseconds > 2000)
                       _buffer.Clear();
                }
                _lastKeystroke = DateTime.Now;

                // Handle Keys
                // Basic Mapping (User32 ToUnicode is better but complex for this scope).
                // Assuming standard US Layout or simple Numeric/Alpha barcode
                
                // ENTER Key
                if (vkCode == 0x0D) // VK_RETURN
                {
                    if (_buffer.Length > 0)
                    {
                        BarcodeScanned?.Invoke(_buffer.ToString());
                        _buffer.Clear();
                    }
                }
                else
                {
                    // Convert VK to Char
                    // Simple heuristic for AlphaNumeric
                    char? ch = GetCharFromKey(vkCode);
                    if (ch.HasValue)
                    {
                        _buffer.Append(ch.Value);
                    }
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private char? GetCharFromKey(int vkCode)
        {
             // 0-9
             if (vkCode >= 0x30 && vkCode <= 0x39) return (char)vkCode;
             // A-Z
             if (vkCode >= 0x41 && vkCode <= 0x5A) return (char)vkCode;
             // Numpad 0-9
             if (vkCode >= 0x60 && vkCode <= 0x69) return (char)(vkCode - 0x30);
             
             // Ignore others for now (Function keys, Ctrl, etc)
             return null;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
