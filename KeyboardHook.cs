using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Win8StartScreen
{
    public class GlobalKeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYUP = 0x0105;

        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_ESCAPE = 0x1B;

        public event Action? WinKeyPressed;
        public event Action? EscKeyPressed;

        private LowLevelKeyboardProc _proc;
        private IntPtr _hookId = IntPtr.Zero;

        private bool _isWinDown = false;
        private bool _hasOtherKeyPressed = false;

        public GlobalKeyboardHook()
        {
            _proc = HookCallback;
            _hookId = SetHook(_proc);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule?.ModuleName), 0);
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
                bool isKeyUp = wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP;

                if (vkCode == VK_LWIN || vkCode == VK_RWIN)
                {
                    if (isKeyDown)
                    {
                        if (!_isWinDown)
                        {
                            _isWinDown = true;
                            _hasOtherKeyPressed = false;
                        }
                        // Подавляем передачу нажатия Win в систему для полного предотвращения вызова меню Win 11
                        return (IntPtr)1;
                    }
                    else if (isKeyUp)
                    {
                        _isWinDown = false;
                        if (!_hasOtherKeyPressed)
                        {
                            WinKeyPressed?.Invoke();
                        }
                        return (IntPtr)1;
                    }
                }
                else
                {
                    if (isKeyDown && _isWinDown)
                    {
                        _hasOtherKeyPressed = true;
                    }

                    if (isKeyDown && vkCode == VK_ESCAPE)
                    {
                        bool isCtrlDown = (GetKeyState(0x11) & 0x8000) != 0; // VK_CONTROL
                        if (isCtrlDown)
                        {
                            WinKeyPressed?.Invoke();
                            return (IntPtr)1;
                        }
                        else
                        {
                            EscKeyPressed?.Invoke();
                        }
                    }
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);
    }
}
