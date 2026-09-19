using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Win8StartScreen
{
    [ComImport, Guid("618736E0-3C3D-11CF-810C-00AA00389B71"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IAccessible
    {
        [DispId(-5000)] int accParent { [return: MarshalAs(UnmanagedType.IDispatch)] get; }
        [DispId(-5001)] int accChildCount { get; }
        [DispId(-5002)] int accChild { [return: MarshalAs(UnmanagedType.IDispatch)] get; }
        [DispId(-5003)] string get_accName([In, MarshalAs(UnmanagedType.Struct)] object varChild);
        [DispId(-5004)] string get_accValue([In, MarshalAs(UnmanagedType.Struct)] object varChild);
        [DispId(-5005)] string get_accDescription([In, MarshalAs(UnmanagedType.Struct)] object varChild);
        [DispId(-5006)] object get_accRole([In, MarshalAs(UnmanagedType.Struct)] object varChild);
        [DispId(-5007)] object get_accState([In, MarshalAs(UnmanagedType.Struct)] object varChild);
        [DispId(-5008)] string get_accHelp([In, MarshalAs(UnmanagedType.Struct)] object varChild);
        [DispId(-5009)] int get_accHelpTopic(out string pszHelpFile, [In, MarshalAs(UnmanagedType.Struct)] object varChild);
        [DispId(-5010)] string get_accKeyboardShortcut([In, MarshalAs(UnmanagedType.Struct)] object varChild);
        [DispId(-5011)] object accFocus { get; }
        [DispId(-5012)] object accSelection { get; }
        [DispId(-5013)] string get_accDefaultAction([In, MarshalAs(UnmanagedType.Struct)] object varChild);
        [DispId(-5014)] void accSelect(int flagsSelect, [In, MarshalAs(UnmanagedType.Struct)] object varChild);
        [DispId(-5015)] void accLocation(out int pxLeft, out int pyTop, out int pcxWidth, out int pcyHeight, [In, MarshalAs(UnmanagedType.Struct)] object varChild);
        [DispId(-5016)] object accNavigate(int navDir, [In, MarshalAs(UnmanagedType.Struct)] object varStart);
        [DispId(-5017)] object accHitTest(int xLeft, int yTop);
        [DispId(-5018)] void accDoDefaultAction([In, MarshalAs(UnmanagedType.Struct)] object varChild);
    }

    public class TaskbarHook : IDisposable
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;

        public event Action<Screen?>? StartButtonClicked;

        private LowLevelMouseProc _proc;
        private IntPtr _hookId = IntPtr.Zero;
        private bool _isMouseDownOnButton = false;
        private Screen? _lastDownScreen = null;

        public TaskbarHook()
        {
            _proc = HookCallback;
            _hookId = SetHook(_proc);
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule?.ModuleName), 0);
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();

                if (msg == WM_LBUTTONDOWN)
                {
                    var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    if (IsPointInStartButton(hookStruct.pt.X, hookStruct.pt.Y, out Screen? clickedScreen))
                    {
                        _isMouseDownOnButton = true;
                        _lastDownScreen = clickedScreen;
                        Log($"[TaskbarHook] Intercepted Start button click on screen '{(clickedScreen?.DeviceName ?? "Unknown")}' at ({hookStruct.pt.X}, {hookStruct.pt.Y})");
                        StartButtonClicked?.Invoke(clickedScreen);
                        // Подавляем нажатие мыши в Windows, чтобы предотвратить появление стандартного меню Пуск Win 11
                        return (IntPtr)1;
                    }
                }
                else if (msg == WM_LBUTTONUP)
                {
                    var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    bool inStart = IsPointInStartButton(hookStruct.pt.X, hookStruct.pt.Y, out _);

                    if (_isMouseDownOnButton || inStart)
                    {
                        _isMouseDownOnButton = false;
                        _lastDownScreen = null;
                        // Подавляем отпускание мыши в Windows
                        return (IntPtr)1;
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public static bool IsPointInStartButton(int x, int y, out Screen? matchedScreen)
        {
            matchedScreen = null;
            try
            {
                var screens = Screen.AllScreens;
                foreach (var screen in screens)
                {
                    if (IsPointInScreenStartButton(screen, x, y))
                    {
                        matchedScreen = screen;
                        return true;
                    }
                }

                // Дополнительная проверка по окнам панели задач Win32 (Shell_TrayWnd и Shell_SecondaryTrayWnd)
                if (IsPointInWin32TaskbarStartButton(x, y, out Screen? wScreen))
                {
                    matchedScreen = wScreen ?? Screen.PrimaryScreen;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log($"[TaskbarHook] Error in IsPointInStartButton: {ex.Message}");
            }

            return false;
        }

        private static bool IsPointInScreenStartButton(Screen screen, int x, int y)
        {
            // Проверяем, находится ли точка на данном мониторе
            if (!screen.Bounds.Contains(x, y)) return false;

            var bounds = screen.Bounds;
            var work = screen.WorkingArea;

            // Вычисляем геометрию панели задач на этом мониторе
            int tbLeft = bounds.Left;
            int tbTop = bounds.Top;
            int tbRight = bounds.Right;
            int tbBottom = bounds.Bottom;
            int tbHeight = 48;

            bool isBottom = work.Bottom < bounds.Bottom;
            bool isTop = work.Top > bounds.Top;
            bool isLeft = work.Left > bounds.Left;
            bool isRight = work.Right < bounds.Right;

            if (isBottom)
            {
                tbTop = work.Bottom;
                tbBottom = bounds.Bottom;
                tbHeight = tbBottom - tbTop;
            }
            else if (isTop)
            {
                tbTop = bounds.Top;
                tbBottom = work.Top;
                tbHeight = tbBottom - tbTop;
            }
            else if (isLeft)
            {
                tbLeft = bounds.Left;
                tbRight = work.Left;
            }
            else if (isRight)
            {
                tbLeft = work.Right;
                tbRight = bounds.Right;
            }
            else
            {
                // Если панель задач скрыта (auto-hide), резервируем полосу внизу монитора высотой 48px
                tbTop = bounds.Bottom - 48;
                tbBottom = bounds.Bottom;
                tbHeight = 48;
            }

            // Проверяем, попал ли курсор в панель задач
            if (x < tbLeft || x > tbRight || y < tbTop || y > tbBottom)
            {
                return false;
            }

            // 1. Проверка для левосторонней кнопки Пуск (Windows 10, Windows 8.1, и Windows 11 при выравнивании "Слева"):
            int startBtnWidth = Math.Max(52, tbHeight + 8);
            if (x >= tbLeft && x <= tbLeft + startBtnWidth && y >= tbTop && y <= tbBottom)
            {
                return true;
            }

            // 2. Проверка для центрированной кнопки Пуск (Windows 11 по умолчанию):
            if (TryCheckAccessibleStartButton(x, y))
            {
                return true;
            }

            return false;
        }

        private static bool IsPointInWin32TaskbarStartButton(int x, int y, out Screen? foundScreen)
        {
            foundScreen = null;
            var taskbarRects = new List<RECT>();

            IntPtr primary = FindWindow("Shell_TrayWnd", null);
            if (primary != IntPtr.Zero && GetWindowRect(primary, out RECT pr))
            {
                taskbarRects.Add(pr);
            }

            EnumWindows((hWnd, lParam) =>
            {
                var sb = new StringBuilder(64);
                GetClassName(hWnd, sb, 64);
                if (sb.ToString() == "Shell_SecondaryTrayWnd")
                {
                    if (GetWindowRect(hWnd, out RECT sr))
                    {
                        taskbarRects.Add(sr);
                    }
                }
                return true;
            }, IntPtr.Zero);

            foreach (var r in taskbarRects)
            {
                if (x >= r.Left && x <= r.Right && y >= r.Top && y <= r.Bottom)
                {
                    int h = r.Bottom - r.Top;
                    int startW = Math.Max(52, h + 8);
                    if (x >= r.Left && x <= r.Left + startW)
                    {
                        var screens = Screen.AllScreens;
                        foundScreen = screens.FirstOrDefault(s => s.Bounds.Contains(x, y));
                        return true;
                    }

                    if (TryCheckAccessibleStartButton(x, y))
                    {
                        var screens = Screen.AllScreens;
                        foundScreen = screens.FirstOrDefault(s => s.Bounds.Contains(x, y));
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryCheckAccessibleStartButton(int x, int y)
        {
            try
            {
                var pt = new POINT { X = x, Y = y };
                if (AccessibleObjectFromPoint(pt, out object? accObj, out object? child) == 0 && accObj is IAccessible acc)
                {
                    string name = acc.get_accName(child ?? 0);
                    if (!string.IsNullOrEmpty(name))
                    {
                        if (name.Equals("Пуск", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("Start", StringComparison.OrdinalIgnoreCase) ||
                            name.IndexOf("Пуск", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }
                    }

                    string desc = acc.get_accDescription(child ?? 0);
                    if (!string.IsNullOrEmpty(desc))
                    {
                        if (desc.IndexOf("Пуск", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            desc.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        private static void Log(string msg)
        {
            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen");
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] {msg}\n");
            }
            catch { }
        }

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("oleacc.dll")]
        private static extern int AccessibleObjectFromPoint(POINT pt, [Out, MarshalAs(UnmanagedType.Interface)] out object? ppacc, [Out] out object? pvarChild);
    }
}
