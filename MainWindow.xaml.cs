using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Win8StartScreen.Models;

namespace Win8StartScreen
{
    public enum ScreenState
    {
        Closed,
        Opening,
        Open,
        Closing
    }

    public enum ActiveView
    {
        Start,
        Apps
    }

    public partial class MainWindow : Window
    {
        public ObservableCollection<TileModel> StartTiles { get; set; } = new();
        public ObservableCollection<AppAlphabetGroup> AlphabetGroups { get; set; } = new();
        public ObservableCollection<AppsViewItem> AllAppsViewItems { get; set; } = new();
        public ObservableCollection<AppsViewItem> DisplayedAppsViewItems { get; set; } = new();

        // Отдельные типизированные плитки для разметки экрана Пуск
        public TileModel MailTile { get; set; } = new();
        public TileModel CalendarTile { get; set; } = new();
        public TileModel IETile { get; set; } = new();
        public TileModel VideoTile { get; set; } = new();
        public TileModel MusicTile { get; set; } = new();
        public TileModel GamesTile { get; set; } = new();
        public TileModel CameraTile { get; set; } = new();
        public TileModel StoreTile { get; set; } = new();
        public TileModel SportsTile { get; set; } = new();
        public TileModel MoneyTile { get; set; } = new();
        public TileModel PeopleTile { get; set; } = new();
        public TileModel DesktopTile { get; set; } = new();
        public TileModel WeatherTile { get; set; } = new();
        public TileModel PhotosTile { get; set; } = new();
        public TileModel OneNoteTile { get; set; } = new();
        public TileModel NewsTile { get; set; } = new();
        public TileModel HelpTile { get; set; } = new();
        public TileModel OneDriveTile { get; set; } = new();
        public TileModel HealthTile { get; set; } = new();
        public TileModel FoodTile { get; set; } = new();
        public TileModel MapsTile { get; set; } = new();
        public TileModel ReadingListTile { get; set; } = new();
        public TileModel PCSettingsTile { get; set; } = new();

        private ScreenState _screenState = ScreenState.Closed;
        public ScreenState CurrentState => _screenState;

        private ActiveView _activeView = ActiveView.Start;
        private bool _isNavigating = false;

        public static MainWindow? Instance { get; private set; }
        private bool _isSearchCharmOpen = false;
        private bool _isSettingsCharmOpen = false;
        private bool _isPersonalizeOpen => _isSettingsCharmOpen;
        private TileSize _addTileSelectedSize = TileSize.Medium;
        private string _addTileSelectedColor = "#FF0078D7";
        private string _addTileCustomIconPath = string.Empty;
        private double _addTileIconSize = 64.0;
        private string _addTileIconStretch = "Uniform";
        private bool _addTileInitialized = false;
        private bool _isUpdatingColorHex = false;
        private bool _isEyedropperActive = false;
        private DispatcherTimer? _eyedropperTimer;
        private Color _eyedropperSampledColor;
        private bool _isSemanticZoomedOut = false;
        private FileSystemWatcher? _configWatcher;
        private System.Windows.Threading.DispatcherTimer? _configDebounceTimer;
        private FileSystemWatcher? _userStartMenuWatcher;
        private FileSystemWatcher? _commonStartMenuWatcher;
        private System.Windows.Threading.DispatcherTimer? _appsRefreshDebounceTimer;
        private volatile bool _isAppInventoryDirty = false;
        private volatile bool _isAppInventoryScanning = false;
        private FileSystemWatcher? _themesWatcher;
        private System.Windows.Threading.DispatcherTimer? _wallpaperDebounceTimer;
        private static string _lastLoadedWallpaperPath = "";
        private static DateTime _lastLoadedWallpaperTime = DateTime.MinValue;
        private static long _lastLoadedWallpaperLength = -1;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int DWMWA_CLOAK = 13;
        private IntPtr _hwnd = IntPtr.Zero;
        private bool _isCloaked = false;

        public static void SafeLog(string message)
        {
            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] {message}\n");
            }
            catch { }
        }

        public MainWindow()
        {
            SafeLog("ctor 1: Instance");
            Instance = this;
            SafeLog("ctor 2: InitializeComponent");
            InitializeComponent();
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
                if (File.Exists(iconPath))
                {
                    Icon = BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
                }
            }
            catch { }
            SafeLog("ctor 3: LoadWindowsUserInfo");
            LoadWindowsUserInfo();
            SafeLog("ctor 4: ThemeManager");
            ThemeManager.ApplyTheme(ThemeManager.CurrentTheme, persist: false);
            SafeLog("ctor 5: Tiles");
            InitializeStartScreenTiles();
            SafeLog("ctor 6: Apps");
            InitializeAppInventory();
            SafeLog("ctor 7: Palette");
            InitializePersonalizationPalette();
            SafeLog("ctor 8: Layout");
            ApplyLayoutConfig();
            SafeLog("ctor 9: Watcher");
            SetupConfigWatcher();
            SetupStartMenuWatchers();
            SetupThemesWatcher();
            DataContext = this;

            SafeLog("ctor 10: EnsureHandle");
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            _hwnd = helper.EnsureHandle();
            SafeLog($"ctor 11: HWND {_hwnd}");

            LocalizationManager.LanguageChanged += UpdateUiLanguage;
            UpdateUiLanguage();
            ApplyMonitorConfiguration();

            Loaded += (s, e) =>
            {
                ApplyMonitorConfiguration();
                UpdateAdaptiveGridDimensions();
                UpdateResolutionScaling();
            };
            SizeChanged += (s, e) =>
            {
                if (e.PreviousSize.Width > 0 && e.PreviousSize.Height > 0 &&
                    (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > 1 || Math.Abs(e.NewSize.Height - e.PreviousSize.Height) > 1))
                {
                    HandleResolutionOrScreenChange();
                }
            };
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (s, e) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    HandleResolutionOrScreenChange();
                }));
            };
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category == Microsoft.Win32.UserPreferenceCategory.Desktop ||
                    e.Category == Microsoft.Win32.UserPreferenceCategory.General ||
                    e.Category == Microsoft.Win32.UserPreferenceCategory.Color)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_wallpaperDebounceTimer != null)
                        {
                            _wallpaperDebounceTimer.Stop();
                            _wallpaperDebounceTimer.Start();
                        }
                        else
                        {
                            UpdateDesktopTileWallpaper(true);
                        }
                    }));
                }
            };

            int cloak = 1;
            int hr = DwmSetWindowAttribute(_hwnd, DWMWA_CLOAK, ref cloak, sizeof(int));
            if (hr == 0)
            {
                _isCloaked = true;
                Visibility = Visibility.Visible;
            }
            SafeLog("ctor finished");
        }

        private void CloakWindow()
        {
            if (_hwnd != IntPtr.Zero)
            {
                int cloak = 1;
                int hr = DwmSetWindowAttribute(_hwnd, DWMWA_CLOAK, ref cloak, sizeof(int));
                if (hr == 0)
                {
                    _isCloaked = true;
                    Topmost = false;
                    return;
                }
            }
            Visibility = Visibility.Collapsed;
            Hide();
        }

        public string TargetMonitor { get; set; } = "Primary";

        public void HandleResolutionOrScreenChange()
        {
            ApplyMonitorConfiguration();
        }

        public void ApplyMonitorConfiguration(System.Windows.Forms.Screen? overrideScreen = null)
        {
            try
            {
                var screens = System.Windows.Forms.Screen.AllScreens;
                System.Windows.Forms.Screen targetScreen;

                if (overrideScreen != null)
                {
                    targetScreen = overrideScreen;
                }
                else
                {
                    targetScreen = System.Windows.Forms.Screen.PrimaryScreen ?? screens[0];
                    if (TargetMonitor.Equals("Secondary", StringComparison.OrdinalIgnoreCase) && screens.Length > 1)
                    {
                        targetScreen = screens.FirstOrDefault(s => !s.Primary) ?? targetScreen;
                    }
                }

                WindowState = WindowState.Normal;
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = targetScreen.Bounds.Left;
                Top = targetScreen.Bounds.Top;
                Width = targetScreen.Bounds.Width;
                Height = targetScreen.Bounds.Height;

                UpdateAdaptiveGridDimensions();
                UpdateResolutionScaling();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApplyMonitorConfiguration] Error: {ex.Message}");
            }
        }

        public bool IsCurrentScreen(System.Windows.Forms.Screen? screen)
        {
            if (screen == null) return true;
            return Math.Abs(Left - screen.Bounds.Left) < 30 && Math.Abs(Top - screen.Bounds.Top) < 30;
        }

        public System.Windows.Forms.Screen GetActiveOrConfiguredScreen()
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            if (screens.Length <= 1) return System.Windows.Forms.Screen.PrimaryScreen ?? screens[0];

            if (TargetMonitor.Equals("Secondary", StringComparison.OrdinalIgnoreCase))
            {
                return screens.FirstOrDefault(s => !s.Primary) ?? screens[0];
            }

            return System.Windows.Forms.Screen.PrimaryScreen ?? screens[0];
        }

        public void UpdateResolutionScaling()
        {
            double curW = ActualWidth > 0 ? ActualWidth : Width;
            double curH = ActualHeight > 0 ? ActualHeight : Height;
            if (curW <= 0) curW = 1920.0;
            if (curH <= 0) curH = 1080.0;

            // Эталонное разрешение сетки плиток: строго 1920х1080
            // При 1920х1080 масштаб строго 1.0 (нативный пиксель-в-пиксель, сетка и координаты не меняются)
            // При других разрешениях экрана контейнер пропорционально масштабируется, сохраняя исходную 1920х1080 сетку
            double scale = Math.Min(curW / 1920.0, curH / 1080.0);
            if (Math.Abs(curW - 1920.0) < 2 && Math.Abs(curH - 1080.0) < 2)
            {
                scale = 1.0;
            }

            if (StartScreenScale != null)
            {
                StartScreenScale.ScaleX = scale;
                StartScreenScale.ScaleY = scale;
            }
            if (AppsScreenScale != null)
            {
                AppsScreenScale.ScaleX = scale;
                AppsScreenScale.ScaleY = scale;
            }
        }

        private void UncloakWindow()
        {
            ApplyMonitorConfiguration();

            if (_hwnd != IntPtr.Zero && _isCloaked)
            {
                int cloak = 0;
                int hr = DwmSetWindowAttribute(_hwnd, DWMWA_CLOAK, ref cloak, sizeof(int));
                if (hr == 0)
                {
                    _isCloaked = false;
                }
            }

            if (Visibility != Visibility.Visible)
            {
                Visibility = Visibility.Visible;
                Show();
            }

            Topmost = true;
            if (_hwnd != IntPtr.Zero) SetForegroundWindow(_hwnd);
            try { Activate(); } catch { }
            try { Focus(); } catch { }
        }

        private void SetupConfigWatcher()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                _configDebounceTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                _configDebounceTimer.Tick += (s, e) =>
                {
                    _configDebounceTimer.Stop();
                    ApplyLayoutConfig();
                };

                _configWatcher = new FileSystemWatcher(dir, "layout_config.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = true
                };
                _configWatcher.Changed += (s, e) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _configDebounceTimer.Stop();
                        _configDebounceTimer.Start();
                    }));
                };
            }
            catch { }
        }

        private void SetupStartMenuWatchers()
        {
            try
            {
                _appsRefreshDebounceTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                _appsRefreshDebounceTimer.Tick += (s, e) =>
                {
                    _appsRefreshDebounceTimer.Stop();
                    _isAppInventoryDirty = true;
                    // Если экран приложений прямо сейчас открыт, обновляем его
                    if (_activeView == ActiveView.Apps && !_isNavigating)
                    {
                        _isAppInventoryDirty = false;
                        InitializeAppInventory();
                    }
                };

                void OnShortcutChanged(object s, FileSystemEventArgs e)
                {
                    _isAppInventoryDirty = true;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _appsRefreshDebounceTimer.Stop();
                        _appsRefreshDebounceTimer.Start();
                    }));
                }

                void OnShortcutRenamed(object s, RenamedEventArgs e)
                {
                    _isAppInventoryDirty = true;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _appsRefreshDebounceTimer.Stop();
                        _appsRefreshDebounceTimer.Start();
                    }));
                }

                string userStart = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs");
                if (Directory.Exists(userStart))
                {
                    _userStartMenuWatcher = new FileSystemWatcher(userStart)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                        EnableRaisingEvents = true
                    };
                    _userStartMenuWatcher.Created += OnShortcutChanged;
                    _userStartMenuWatcher.Deleted += OnShortcutChanged;
                    _userStartMenuWatcher.Changed += OnShortcutChanged;
                    _userStartMenuWatcher.Renamed += OnShortcutRenamed;
                }

                string commonStart = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs");
                if (Directory.Exists(commonStart))
                {
                    _commonStartMenuWatcher = new FileSystemWatcher(commonStart)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                        EnableRaisingEvents = true
                    };
                    _commonStartMenuWatcher.Created += OnShortcutChanged;
                    _commonStartMenuWatcher.Deleted += OnShortcutChanged;
                    _commonStartMenuWatcher.Changed += OnShortcutChanged;
                    _commonStartMenuWatcher.Renamed += OnShortcutRenamed;
                }
            }
            catch { }
        }

        private void SetupThemesWatcher()
        {
            try
            {
                string themesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Themes");
                if (Directory.Exists(themesDir))
                {
                    _wallpaperDebounceTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                    _wallpaperDebounceTimer.Tick += (s, e) =>
                    {
                        _wallpaperDebounceTimer.Stop();
                        UpdateDesktopTileWallpaper();
                    };

                    _themesWatcher = new FileSystemWatcher(themesDir)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                        EnableRaisingEvents = true
                    };

                    void OnThemeFileChanged(object s, FileSystemEventArgs e)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            _wallpaperDebounceTimer.Stop();
                            _wallpaperDebounceTimer.Start();
                        }));
                    }

                    _themesWatcher.Changed += OnThemeFileChanged;
                    _themesWatcher.Created += OnThemeFileChanged;
                    _themesWatcher.Renamed += (s, e) => OnThemeFileChanged(s, e);
                }
            }
            catch { }
        }

        public static string ResolveCurrentDesktopWallpaper()
        {
            try
            {
                // 1. Проверяем папку активных обоев Windows: %APPDATA%\Microsoft\Windows\Themes
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string themesDir = Path.Combine(appData, "Microsoft", "Windows", "Themes");
                if (Directory.Exists(themesDir))
                {
                    string transcoded0 = Path.Combine(themesDir, "Transcoded_000");
                    string transcodedGlobal = Path.Combine(themesDir, "TranscodedWallpaper");

                    bool has0 = File.Exists(transcoded0);
                    bool hasGlobal = File.Exists(transcodedGlobal);

                    if (has0 && hasGlobal)
                    {
                        DateTime t0 = File.GetLastWriteTimeUtc(transcoded0);
                        DateTime tg = File.GetLastWriteTimeUtc(transcodedGlobal);
                        // Если Transcoded_000 новее или равен TranscodedWallpaper, берем Transcoded_000 (монитор 0)
                        return t0 >= tg ? transcoded0 : transcodedGlobal;
                    }
                    if (has0) return transcoded0;
                    if (hasGlobal) return transcodedGlobal;

                    var files = Directory.GetFiles(themesDir, "Transcoded*");
                    if (files.Length > 0)
                    {
                        Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                        return files[0];
                    }
                }

                // 2. HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers\CurrentWallpaperPath
                try
                {
                    using var expKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers");
                    if (expKey?.GetValue("CurrentWallpaperPath") is string curWp && File.Exists(curWp))
                    {
                        return curWp;
                    }
                }
                catch { }

                // 3. HKCU\Control Panel\Desktop\Wallpaper
                try
                {
                    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
                    if (key?.GetValue("Wallpaper") is string regWallpaper && File.Exists(regWallpaper))
                    {
                        return regWallpaper;
                    }
                }
                catch { }

                // 4. Custom theme wallpaper
                if (!string.IsNullOrEmpty(ThemeManager.CurrentTheme.CustomWallpaperPath) && File.Exists(ThemeManager.CurrentTheme.CustomWallpaperPath))
                {
                    return ThemeManager.CurrentTheme.CustomWallpaperPath;
                }
            }
            catch { }

            return "";
        }

        public static BitmapImage? LoadDesktopWallpaperBitmap()
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    string realWp = ResolveCurrentDesktopWallpaper();
                    if (!string.IsNullOrEmpty(realWp) && File.Exists(realWp))
                    {
                        byte[] bytes;
                        using (var fs = new FileStream(realWp, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var ms = new MemoryStream())
                        {
                            fs.CopyTo(ms);
                            bytes = ms.ToArray();
                        }

                        if (bytes.Length > 1000)
                        {
                            var bi = new BitmapImage();
                            bi.BeginInit();
                            bi.StreamSource = new MemoryStream(bytes);
                            bi.CacheOption = BitmapCacheOption.OnLoad;
                            bi.DecodePixelWidth = 620;
                            bi.EndInit();
                            bi.Freeze();
                            return bi;
                        }
                    }
                }
                catch
                {
                    if (attempt < 2)
                    {
                        System.Threading.Thread.Sleep(80);
                    }
                }
            }
            return null;
        }

        public void UpdateDesktopTileWallpaper(bool force = false)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => UpdateDesktopTileWallpaper(force)));
                return;
            }

            try
            {
                string realWp = ResolveCurrentDesktopWallpaper();
                if (string.IsNullOrEmpty(realWp) || !File.Exists(realWp)) return;

                var fi = new FileInfo(realWp);
                DateTime writeTime = fi.LastWriteTimeUtc;
                long length = fi.Length;

                if (!force && realWp == _lastLoadedWallpaperPath && writeTime == _lastLoadedWallpaperTime && length == _lastLoadedWallpaperLength)
                {
                    return;
                }

                var bmp = LoadDesktopWallpaperBitmap();
                if (bmp == null) return;

                _lastLoadedWallpaperPath = realWp;
                _lastLoadedWallpaperTime = writeTime;
                _lastLoadedWallpaperLength = length;

                foreach (var tile in StartTiles)
                {
                    if (tile.IsDesktopTile)
                    {
                        tile.DesktopWallpaperSource = bmp;
                        tile.IconImagePath = realWp;
                    }
                }
            }
            catch { }
        }

        public static void EnsureDefaultLayoutConfig(string configPath)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(configPath) ?? "";
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = new[]
                {
                    System.IO.Path.Combine(baseDir, "default_layout.json"),
                    System.IO.Path.Combine(baseDir, "Assets", "default_layout.json"),
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WinMosaic", "default_layout.json"),
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WinMosaic", "Assets", "default_layout.json")
                };

                foreach (var c in candidates)
                {
                    if (System.IO.File.Exists(c) && new System.IO.FileInfo(c).Length > 100)
                    {
                        System.IO.File.Copy(c, configPath, true);
                        SafeLog($"EnsureDefaultLayoutConfig: Copied from {c} to {configPath}");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                SafeLog($"EnsureDefaultLayoutConfig error: {ex.Message}");
            }
        }

        public static string ResolveTileIconPath(string title, string configuredIcon, string iconVector)
        {
            try
            {
                if (string.IsNullOrEmpty(configuredIcon) && string.IsNullOrEmpty(iconVector))
                {
                    return "";
                }

                string t = title.Trim();

                // Authentic Windows 8.1 system tiles with vectors render crisp SVG vector paths
                bool prefersVector = !string.IsNullOrEmpty(iconVector) &&
                                     !t.Equals("Games", StringComparison.OrdinalIgnoreCase) &&
                                     !t.Equals("Игры", StringComparison.OrdinalIgnoreCase) &&
                                     !t.Equals("Desktop", StringComparison.OrdinalIgnoreCase) &&
                                     !t.Equals("Рабочий стол", StringComparison.OrdinalIgnoreCase);

                if (prefersVector)
                {
                    return "";
                }

                if (!string.IsNullOrEmpty(configuredIcon) && System.IO.File.Exists(configuredIcon))
                {
                    return System.IO.Path.GetFullPath(configuredIcon);
                }

                if (!string.IsNullOrEmpty(configuredIcon))
                {
                    string cleaned = configuredIcon.Replace('/', '\\');
                    if (cleaned.StartsWith("Assets\\", StringComparison.OrdinalIgnoreCase))
                    {
                        cleaned = cleaned.Substring(7);
                    }
                    string assetP = GetAssetPath(cleaned);
                    if (System.IO.File.Exists(assetP)) return System.IO.Path.GetFullPath(assetP);

                    string fn = System.IO.Path.GetFileName(configuredIcon);
                    if (!string.IsNullOrEmpty(fn))
                    {
                        string fnAsset = GetAssetPath(fn);
                        if (System.IO.File.Exists(fnAsset)) return System.IO.Path.GetFullPath(fnAsset);
                        string fnLive = GetAssetPath("LiveTiles\\" + fn);
                        if (System.IO.File.Exists(fnLive)) return System.IO.Path.GetFullPath(fnLive);
                    }
                }

                if (t.Equals("Games", StringComparison.OrdinalIgnoreCase) || t.Equals("Игры", StringComparison.OrdinalIgnoreCase))
                {
                    string p = GetAssetPath("games_icon.png"); if (System.IO.File.Exists(p)) return System.IO.Path.GetFullPath(p);
                }
                if (t.Equals("Money", StringComparison.OrdinalIgnoreCase) || t.Equals("Финансы", StringComparison.OrdinalIgnoreCase))
                {
                    string p = GetAssetPath("money_icon.png"); if (System.IO.File.Exists(p)) return System.IO.Path.GetFullPath(p);
                }
                if (t.Equals("Sports", StringComparison.OrdinalIgnoreCase) || t.Equals("Спорт", StringComparison.OrdinalIgnoreCase))
                {
                    string p = GetAssetPath("sports_icon.png"); if (System.IO.File.Exists(p)) return System.IO.Path.GetFullPath(p);
                }
                if (t.Equals("Store", StringComparison.OrdinalIgnoreCase) || t.Equals("Магазин", StringComparison.OrdinalIgnoreCase))
                {
                    string p = GetAssetPath("store_icon.png"); if (System.IO.File.Exists(p)) return System.IO.Path.GetFullPath(p);
                    p = GetAssetPath("MetroIcons\\Applications\\Windows 8 Store.png"); if (System.IO.File.Exists(p)) return System.IO.Path.GetFullPath(p);
                }
                if (t.Equals("Desktop", StringComparison.OrdinalIgnoreCase) || t.Equals("Рабочий стол", StringComparison.OrdinalIgnoreCase))
                {
                    return "";
                }
                if (t.Equals("Weather", StringComparison.OrdinalIgnoreCase) || t.Equals("Погода", StringComparison.OrdinalIgnoreCase))
                {
                    string p = GetAssetPath("weather_sun.png"); if (System.IO.File.Exists(p)) return System.IO.Path.GetFullPath(p);
                }
                if (t.Equals("Internet Explorer", StringComparison.OrdinalIgnoreCase) || t.Equals("IE", StringComparison.OrdinalIgnoreCase))
                {
                    string p = GetAssetPath("ie_icon.png"); if (System.IO.File.Exists(p)) return System.IO.Path.GetFullPath(p);
                    p = GetAssetPath("MetroIcons\\Web Browsers\\Internet Explorer 10.png"); if (System.IO.File.Exists(p)) return System.IO.Path.GetFullPath(p);
                }
                if (t.Equals("Skype", StringComparison.OrdinalIgnoreCase))
                {
                    string p = GetAssetPath("skype_icon.png"); if (System.IO.File.Exists(p)) return System.IO.Path.GetFullPath(p);
                }
                if (t.Equals("Photos", StringComparison.OrdinalIgnoreCase) || t.Equals("Фотографии", StringComparison.OrdinalIgnoreCase))
                {
                    string p = GetAssetPath("photos_icon.png"); if (System.IO.File.Exists(p)) return System.IO.Path.GetFullPath(p);
                }
                if (t.Equals("Maps", StringComparison.OrdinalIgnoreCase) || t.Equals("Карты", StringComparison.OrdinalIgnoreCase))
                {
                    string p = GetAssetPath("maps_icon.png"); if (System.IO.File.Exists(p)) return System.IO.Path.GetFullPath(p);
                }
                if (t.Equals("News", StringComparison.OrdinalIgnoreCase) || t.Equals("Новости", StringComparison.OrdinalIgnoreCase))
                {
                    string p = GetAssetPath("news_icon.png"); if (System.IO.File.Exists(p)) return System.IO.Path.GetFullPath(p);
                }
                if (t.Equals("OneDrive", StringComparison.OrdinalIgnoreCase) || t.Equals("SkyDrive", StringComparison.OrdinalIgnoreCase))
                {
                    string p = GetAssetPath("onedrive_icon.png"); if (System.IO.File.Exists(p)) return System.IO.Path.GetFullPath(p);
                }
                if (t.Equals("OneNote", StringComparison.OrdinalIgnoreCase))
                {
                    string p = GetAssetPath("onenote_icon.png"); if (System.IO.File.Exists(p)) return System.IO.Path.GetFullPath(p);
                }
                if (t.Equals("Discord", StringComparison.OrdinalIgnoreCase))
                {
                    string p = GetAssetPath("discord_icon.jpg"); if (System.IO.File.Exists(p)) return System.IO.Path.GetFullPath(p);
                }
                if (t.Equals("Health & Fitness", StringComparison.OrdinalIgnoreCase) || t.Equals("Здоровье и фитнес", StringComparison.OrdinalIgnoreCase))
                {
                    string p = GetAssetPath("health_icon.png"); if (System.IO.File.Exists(p)) return System.IO.Path.GetFullPath(p);
                }
                if (t.Equals("Food & Drink", StringComparison.OrdinalIgnoreCase) || t.Equals("Кулинария", StringComparison.OrdinalIgnoreCase))
                {
                    string p = GetAssetPath("food_icon.png"); if (System.IO.File.Exists(p)) return System.IO.Path.GetFullPath(p);
                }
                if (t.Equals("Reading List", StringComparison.OrdinalIgnoreCase) || t.Equals("Список для чтения", StringComparison.OrdinalIgnoreCase))
                {
                    string p = GetAssetPath("readinglist_icon.png"); if (System.IO.File.Exists(p)) return System.IO.Path.GetFullPath(p);
                }

                string metro = MetroIconResolver.ResolveIconPath(t);
                if (!string.IsNullOrEmpty(metro) && System.IO.File.Exists(metro)) return System.IO.Path.GetFullPath(metro);

                if (!string.IsNullOrEmpty(configuredIcon))
                {
                    string fnWithoutExt = System.IO.Path.GetFileNameWithoutExtension(configuredIcon);
                    string metroFn = MetroIconResolver.ResolveIconPath(fnWithoutExt);
                    if (!string.IsNullOrEmpty(metroFn) && System.IO.File.Exists(metroFn)) return System.IO.Path.GetFullPath(metroFn);
                }
            }
            catch { }

            return !string.IsNullOrEmpty(iconVector) ? "" : configuredIcon;
        }

        public static string ResolveLiveImagePath(string liveImg)
        {
            if (string.IsNullOrEmpty(liveImg)) return "";
            if (System.IO.File.Exists(liveImg)) return liveImg;
            string fn = System.IO.Path.GetFileName(liveImg);
            string candidate = GetAssetPath("LiveTiles\\" + fn);
            if (System.IO.File.Exists(candidate)) return candidate;
            candidate = GetAssetPath(fn);
            if (System.IO.File.Exists(candidate)) return candidate;
            return liveImg;
        }

        public void ResetDefaultTiles()
        {
            try
            {
                string configPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen", "layout_config.json");
                EnsureDefaultLayoutConfig(configPath);
                ApplyLayoutConfig();
            }
            catch (Exception ex)
            {
                SafeLog($"ResetDefaultTiles error: {ex.Message}");
            }
        }

        private void ResetDefaultTiles_Click(object sender, RoutedEventArgs e)
        {
            ResetDefaultTiles();
            CloseAllFlyouts();
        }

        private void ApplyLayoutConfig()
        {
            try
            {
                MetroIconResolver.Initialize();
                string configPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen", "layout_config.json");
                bool needsDefault = !System.IO.File.Exists(configPath) || new System.IO.FileInfo(configPath).Length < 50;
                if (!needsDefault)
                {
                    try
                    {
                        using var testDoc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(configPath));
                        if (!testDoc.RootElement.TryGetProperty("Tiles", out var testTiles) || testTiles.GetArrayLength() == 0)
                        {
                            needsDefault = true;
                        }
                    }
                    catch { needsDefault = true; }
                }

                if (needsDefault)
                {
                    EnsureDefaultLayoutConfig(configPath);
                }

                if (!System.IO.File.Exists(configPath)) return;

                string json = System.IO.File.ReadAllText(configPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("Language", out var langProp))
                {
                    string lang = langProp.GetString() ?? "ru";
                    LocalizationManager.SetLanguage(lang);
                    UpdateUiLanguage();
                }

                if (doc.RootElement.TryGetProperty("TargetMonitor", out var monProp))
                {
                    string mon = monProp.GetString() ?? "Primary";
                    TargetMonitor = mon;
                    ApplyMonitorConfiguration();
                }

                if (doc.RootElement.TryGetProperty("Tiles", out var tilesElem) && tilesElem.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    if (tilesElem.GetArrayLength() == 0)
                    {
                        EnsureDefaultLayoutConfig(configPath);
                        ApplyLayoutConfig();
                        return;
                    }

                    UpdateAdaptiveGridDimensions();
                    StartTilesCanvas.Children.Clear();
                    StartTiles.Clear();

                    double maxRight = 1400;

                    foreach (var item in tilesElem.EnumerateArray())
                    {
                        string title = item.TryGetProperty("Title", out var t) ? t.GetString() ?? "" : "";
                        string color = item.TryGetProperty("BackgroundColor", out var c) ? c.GetString() ?? "#FF0078D7" : "#FF0078D7";
                        string icon = item.TryGetProperty("IconImagePath", out var ic) ? ic.GetString() ?? "" : "";
                        string liveText = item.TryGetProperty("LiveText", out var lt) ? lt.GetString() ?? "" : "";
                        string groupName = item.TryGetProperty("GroupName", out var gn) ? gn.GetString() ?? "" : "";
                        if (groupName.Equals("Main", StringComparison.OrdinalIgnoreCase)) groupName = "";
                        string exe = item.TryGetProperty("ExecutablePath", out var ep) ? ep.GetString() ?? "" : "";
                        int sizeInt = item.TryGetProperty("Size", out var sz) ? sz.GetInt32() : 1;
                        double x = item.TryGetProperty("X", out var xp) ? xp.GetDouble() : 0;
                        double y = item.TryGetProperty("Y", out var yp) ? yp.GetDouble() : 0;
                        double customW = item.TryGetProperty("CustomWidth", out var cwp) ? cwp.GetDouble() : 0;
                        double customH = item.TryGetProperty("CustomHeight", out var chp) ? chp.GetDouble() : 0;

                        x = Math.Max(0, x);
                        y = Math.Max(0, y);
                        double iconScale = item.TryGetProperty("IconScale", out var isp) ? isp.GetDouble() : 1.0;
                        double iconSize = item.TryGetProperty("IconSize", out var iszp) ? iszp.GetDouble() : 64.0;
                        double iconOffX = item.TryGetProperty("IconOffsetX", out var ioxp) ? ioxp.GetDouble() : 0;
                        double iconOffY = item.TryGetProperty("IconOffsetY", out var ioyp) ? ioyp.GetDouble() : 0;
                        string iconStretch = item.TryGetProperty("IconStretch", out var istp) ? istp.GetString() ?? "Uniform" : "Uniform";
                        string liveTemplate = item.TryGetProperty("LiveTemplate", out var ltp) ? ltp.GetString() ?? "Auto" : "Auto";
                        string liveImg = item.TryGetProperty("LiveImagePath", out var lip) ? lip.GetString() ?? "" : "";
                        string liveHeadline = item.TryGetProperty("LiveHeadline", out var lhp) ? lhp.GetString() ?? "" : "";
                        string liveSubheadline = item.TryGetProperty("LiveSubheadline", out var lshp) ? lshp.GetString() ?? "" : "";
                        string liveBannerColor = item.TryGetProperty("LiveBannerColor", out var lbcp) ? lbcp.GetString() ?? "" : "";
                        string liveBannerIcon = item.TryGetProperty("LiveBannerIcon", out var lbip) ? lbip.GetString() ?? "" : "";
                        string liveCity = item.TryGetProperty("LiveCity", out var lctp) ? lctp.GetString() ?? "Москва" : "Москва";
                        string liveTemp = item.TryGetProperty("LiveTemperature", out var ltempp) ? ltempp.GetString() ?? "+21°" : "+21°";
                        string liveCond = item.TryGetProperty("LiveCondition", out var lcondp) ? lcondp.GetString() ?? "Ясно" : "Ясно";
                        string st1Icon = item.TryGetProperty("StoreTopApp1Icon", out var st1ip) ? st1ip.GetString() ?? "" : "";
                        string st1Name = item.TryGetProperty("StoreTopApp1Name", out var st1np) ? st1np.GetString() ?? "" : "";
                        string st1Sub = item.TryGetProperty("StoreTopApp1Sub", out var st1sp) ? st1sp.GetString() ?? "" : "";
                        string st2Icon = item.TryGetProperty("StoreTopApp2Icon", out var st2ip) ? st2ip.GetString() ?? "" : "";
                        string st2Name = item.TryGetProperty("StoreTopApp2Name", out var st2np) ? st2np.GetString() ?? "" : "";
                        string st2Sub = item.TryGetProperty("StoreTopApp2Sub", out var st2sp) ? st2sp.GetString() ?? "" : "";
                        string st3Icon = item.TryGetProperty("StoreTopApp3Icon", out var st3ip) ? st3ip.GetString() ?? "" : "";
                        string st3Name = item.TryGetProperty("StoreTopApp3Name", out var st3np) ? st3np.GetString() ?? "" : "";
                        string st3Sub = item.TryGetProperty("StoreTopApp3Sub", out var st3sp) ? st3sp.GetString() ?? "" : "";

                        string iconVector = item.TryGetProperty("IconVectorPath", out var ivp) ? ivp.GetString() ?? "" : "";
                        string iconGlyph = item.TryGetProperty("IconGlyph", out var igp) ? igp.GetString() ?? "" : "";
                        if (string.IsNullOrEmpty(iconGlyph))
                        {
                            if (title.Equals("Photoshop", StringComparison.OrdinalIgnoreCase)) iconGlyph = "Ps";
                            else if (title.Equals("Adobe Premiere Pro", StringComparison.OrdinalIgnoreCase)) iconGlyph = "Pr";
                            else if (title.Equals("Adobe After Effects", StringComparison.OrdinalIgnoreCase)) iconGlyph = "Ae";
                            else if (title.Equals("OneNote", StringComparison.OrdinalIgnoreCase)) iconGlyph = "N";
                        }
                        bool isLiveTileEnabledConfig = item.TryGetProperty("IsLiveTileEnabled", out var iltep) ? iltep.GetBoolean() : true;

                        icon = ResolveTileIconPath(title, icon, iconVector);
                        liveImg = ResolveLiveImagePath(liveImg);
                        st1Icon = ResolveLiveImagePath(st1Icon);
                        st2Icon = ResolveLiveImagePath(st2Icon);
                        st3Icon = ResolveLiveImagePath(st3Icon);

                        var tileModel = new TileModel
                        {
                            Title = title,
                            GroupName = groupName,
                            Size = (TileSize)sizeInt,
                            X = x,
                            Y = y,
                            CustomWidth = customW,
                            CustomHeight = customH,
                            IconScale = iconScale,
                            IconSize = iconSize,
                            IconOffsetX = iconOffX,
                            IconOffsetY = iconOffY,
                            IconStretch = iconStretch,
                            IconImagePath = icon,
                            IconVectorPath = iconVector,
                            IconGlyph = iconGlyph,
                            IsLiveTileEnabled = isLiveTileEnabledConfig,
                            LiveText = liveText,
                            LiveTemplate = liveTemplate,
                            LiveImagePath = liveImg,
                            LiveHeadline = liveHeadline,
                            LiveSubheadline = liveSubheadline,
                            LiveBannerColor = liveBannerColor,
                            LiveBannerIcon = liveBannerIcon,
                            LiveCity = liveCity,
                            LiveTemperature = liveTemp,
                            LiveCondition = liveCond,
                            StoreTopApp1Icon = st1Icon,
                            StoreTopApp1Name = !string.IsNullOrEmpty(st1Name) ? st1Name : "Minecraft: Pocket Edition",
                            StoreTopApp1Sub = !string.IsNullOrEmpty(st1Sub) ? st1Sub : "Бесплатно ★★★★★ 18 420",
                            StoreTopApp2Icon = st2Icon,
                            StoreTopApp2Name = !string.IsNullOrEmpty(st2Name) ? st2Name : "Asphalt 8: На взлёт",
                            StoreTopApp2Sub = !string.IsNullOrEmpty(st2Sub) ? st2Sub : "Бесплатно ★★★★★ 95 430",
                            StoreTopApp3Icon = st3Icon,
                            StoreTopApp3Name = !string.IsNullOrEmpty(st3Name) ? st3Name : "Jetpack Joyride",
                            StoreTopApp3Sub = !string.IsNullOrEmpty(st3Sub) ? st3Sub : "Бесплатно ★★★★★ 27 890",
                            ExecutablePath = exe
                        };

                        try
                        {
                            tileModel.BackgroundBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                        }
                        catch
                        {
                            tileModel.BackgroundBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215));
                        }

                        if (!isLiveTileEnabledConfig)
                        {
                            tileModel.LiveText = string.Empty;
                            tileModel.IsLiveTileEnabled = false;
                            tileModel.LiveTemplate = string.Empty;
                        }
                        // Mail ВСЕГДА строго статична: никакого живого текста и никакого переворота
                        else if (title.Equals("Mail", StringComparison.OrdinalIgnoreCase) || title.Equals("Почта", StringComparison.OrdinalIgnoreCase))
                        {
                            tileModel.LiveText = string.Empty;
                            tileModel.IsLiveTileEnabled = false;
                            tileModel.LiveTemplate = string.Empty;
                        }
                        // Аутентичные живые плитки Windows 8.1
                        else if (title.Equals("Sports", StringComparison.OrdinalIgnoreCase) || title.Equals("Спорт", StringComparison.OrdinalIgnoreCase))
                        {
                            tileModel.LiveTemplate = "PhotoBanner";
                            tileModel.LiveImagePath = GetAssetPath("LiveTiles\\sports_shirokov.png");
                            tileModel.LiveHeadline = LocalizationManager.Get("LiveSportsHeadline", "Роман Широков: Быть");
                            tileModel.LiveSubheadline = LocalizationManager.Get("LiveSportsSubheadline", "капитаном — это...");
                            tileModel.LiveBannerColor = "#5C2D91";
                            tileModel.LiveBannerIcon = "M19 5h-2V3H7v2H5c-1.1 0-2 .9-2 2v1c0 2.55 1.92 4.63 4.39 4.94A5.01 5.01 0 0 0 11 15.9V19H7v2h10v-2h-4v-3.1a5.01 5.01 0 0 0 3.61-2.96C19.08 12.63 21 10.55 21 8V7c0-1.1-.9-2-2-2zM5 8V7h2v3.82C5.84 10.4 5 9.3 5 8zm14 0c0 1.3-.84 2.4-2 2.82V7h2v1z";
                            tileModel.IsLiveTileEnabled = true;
                        }
                        else if (title.Equals("News", StringComparison.OrdinalIgnoreCase) || title.Equals("Новости", StringComparison.OrdinalIgnoreCase))
                        {
                            tileModel.LiveTemplate = "PhotoBanner";
                            tileModel.LiveImagePath = GetAssetPath("LiveTiles\\news_pope.png");
                            tileModel.LiveHeadline = LocalizationManager.Get("LiveNewsHeadline", "Последняя молитва");
                            tileModel.LiveBannerColor = "#A20025";
                            tileModel.LiveBannerIcon = "M20,11H4V8H20M20,15H13V13H20M20,19H13V17H20M11,19H4V13H11M20,3H4C2.89,3 2,3.89 2,5V19A2,2 0 0,0 4,21H20A2,2 0 0,0 22,19V5C22,3.89 21.1,3 20,3Z";
                            tileModel.IsLiveTileEnabled = true;
                        }
                        else if (title.Equals("Money", StringComparison.OrdinalIgnoreCase) || title.Equals("Финансы", StringComparison.OrdinalIgnoreCase))
                        {
                            tileModel.LiveTemplate = "FinanceQuotes";
                            tileModel.LiveQuote1Name = "DOW"; tileModel.LiveQuote1Value = "16 556,82"; tileModel.LiveQuote1Change = "-16,18"; tileModel.LiveQuote1IsUp = false;
                            tileModel.LiveQuote2Name = "FTSE 100"; tileModel.LiveQuote2Value = "6 655,20"; tileModel.LiveQuote2Change = "-3,84"; tileModel.LiveQuote2IsUp = false;
                            tileModel.LiveQuote3Name = "NIKKEI 225"; tileModel.LiveQuote3Value = "15 071,88"; tileModel.LiveQuote3Change = "+125,56"; tileModel.LiveQuote3IsUp = true;
                            tileModel.IsLiveTileEnabled = true;
                            tileModel.BackgroundColor = "#FF008A00";
                            tileModel.BackgroundBrush = new SolidColorBrush(Color.FromRgb(0, 138, 0));
                        }
                        else if (title.Equals("Weather", StringComparison.OrdinalIgnoreCase) || title.Equals("Погода", StringComparison.OrdinalIgnoreCase))
                        {
                            tileModel.LiveTemplate = "Weather";
                            tileModel.LiveImagePath = GetAssetPath("LiveTiles\\weather_sky.png");
                            tileModel.LiveCity = LocalizationManager.Get("LiveWeatherCity", "Москва");
                            tileModel.LiveTemperature = "+21°";
                            tileModel.LiveCondition = LocalizationManager.Get("LiveWeatherCondition", "Преимущественно солнечно");
                            tileModel.LiveText = LocalizationManager.Get("LiveWeatherText", "Москва, Ясно\n+21°C | Влажность 45%");
                            tileModel.IsLiveTileEnabled = true;
                        }
                        else if (title.Equals("Calendar", StringComparison.OrdinalIgnoreCase) || title.Equals("Календарь", StringComparison.OrdinalIgnoreCase))
                        {
                            tileModel.LiveText = LocalizationManager.Get("LiveCalendarText", "14:00 - Встреча команды\n18:30 - Тренировка");
                            tileModel.IsLiveTileEnabled = true;
                        }
                        else if (title.Equals("Store", StringComparison.OrdinalIgnoreCase) || title.Equals("Магазин", StringComparison.OrdinalIgnoreCase))
                        {
                            tileModel.LiveTemplate = "Store";
                            tileModel.StoreAppIcon = GetAssetPath("LiveTiles\\game_minion.png");
                            tileModel.StoreHeadline = LocalizationManager.Get("LiveStoreHeadline", "Войдите в Лабораторию...");
                            tileModel.StoreAppName = LocalizationManager.Get("LiveStoreAppName", "Гадкий Я: Minion Rush");
                            tileModel.StoreRatingPrice = LocalizationManager.Get("LiveStoreRatingPrice", "Бесплатно ★★★★★ 12 369");
                            tileModel.StoreBadgeCount = "11";
                            tileModel.StoreTopApp1Sub = LocalizationManager.Get("LiveStoreTop1Sub", "Бесплатно ★★★★★ 18 420");
                            tileModel.StoreTopApp2Name = LocalizationManager.Get("LiveStoreTop2Name", "Asphalt 8: На взлёт");
                            tileModel.StoreTopApp2Sub = LocalizationManager.Get("LiveStoreTop2Sub", "Бесплатно ★★★★★ 95 430");
                            tileModel.StoreTopApp3Sub = LocalizationManager.Get("LiveStoreTop3Sub", "Бесплатно ★★★★★ 27 890");
                            tileModel.IsLiveTileEnabled = true;
                        }
                        else if (title.Equals("Games", StringComparison.OrdinalIgnoreCase) || title.Equals("Игры", StringComparison.OrdinalIgnoreCase) || liveTemplate == "Games")
                        {
                            tileModel.Title = "Games";
                            tileModel.LiveTemplate = "Games";
                            tileModel.GameIcon = GetAssetPath("LiveTiles\\game_angry_birds.png");
                            tileModel.GameTitle = "Angry Birds";
                            tileModel.GameSubtitle = "Xbox Live";
                            tileModel.IsLiveTileEnabled = true;
                        }
                        else if (title.Equals("Food & Drink", StringComparison.OrdinalIgnoreCase) || title.Equals("Кулинария", StringComparison.OrdinalIgnoreCase))
                        {
                            tileModel.LiveTemplate = "PhotoBanner";
                            tileModel.LiveImagePath = GetAssetPath("LiveTiles\\food_pasta.png");
                            tileModel.LiveHeadline = LocalizationManager.Get("LiveFoodHeadline", "Паста с брокколи");
                            tileModel.LiveBannerColor = "#008272";
                            tileModel.LiveBannerIcon = "M11,9H9V2H7V9H5V2H3V9C3,11.12 4.66,12.84 6.75,12.97V22H9.25V12.97C11.34,12.84 13,11.12 13,9V2H11V9M16,6V14H18.5V22H21V2C18.24,2 16,4.24 16,6Z";
                            tileModel.IsLiveTileEnabled = true;
                        }

                        if (tileModel.IsDesktopTile)
                        {
                            if (string.IsNullOrEmpty(tileModel.IconImagePath) || !System.IO.File.Exists(tileModel.IconImagePath))
                            {
                                string realWp = ResolveCurrentDesktopWallpaper();
                                if (!string.IsNullOrEmpty(realWp) && System.IO.File.Exists(realWp))
                                {
                                    tileModel.IconImagePath = realWp;
                                    var bmp = LoadDesktopWallpaperBitmap();
                                    if (bmp != null)
                                    {
                                        tileModel.DesktopWallpaperSource = bmp;
                                    }
                                }
                                else
                                {
                                    string defaultDesk = GetAssetPath("win8_desktop.png");
                                    if (System.IO.File.Exists(defaultDesk)) tileModel.IconImagePath = defaultDesk;
                                }
                            }
                        }

                        StartTiles.Add(tileModel);
                    }


                    foreach (var tileModel in StartTiles)
                    {
                        var control = new LiveTileControl
                        {
                            DataContext = tileModel
                        };

                        Canvas.SetLeft(control, tileModel.X);
                        Canvas.SetTop(control, tileModel.Y);
                        StartTilesCanvas.Children.Add(control);

                        if (tileModel.X + tileModel.PixelWidth > maxRight)
                        {
                            maxRight = tileModel.X + tileModel.PixelWidth + 100;
                        }
                    }

                    SyncCanvasWidths(Math.Max(2400, maxRight + 200));
                    UpdateGroupHeaders();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApplyLayoutConfig] Error: {ex.Message}");
            }
        }

        // =========================================================================
        // WINDOWS 8.1 GROUP NAMING & CUSTOMIZATION SYSTEM
        // =========================================================================

        public class TileGroupInfo
        {
            public int GroupIndex { get; set; }
            public string Name { get; set; } = "";
            public double Left { get; set; }
            public double Width { get; set; }
            public List<TileModel> Tiles { get; set; } = new();
        }

        private bool _isGroupNamingMode = false;
        private int? _currentlyEditingGroupIndex = null;

        public void SyncCanvasWidths(double width)
        {
            StartTilesCanvas.Width = width;
            if (StartScrollCanvas != null) StartScrollCanvas.Width = width;
            if (GroupHeadersCanvas != null) GroupHeadersCanvas.Width = width;
        }

        private List<TileGroupInfo> GetTileGroups()
        {
            var groups = new List<TileGroupInfo>();
            if (StartTiles == null || StartTiles.Count == 0) return groups;

            // Сортируем все видимые плитки по горизонтали слева направо
            var sortedTiles = StartTiles
                .Where(t => t.IsVisible)
                .OrderBy(t => t.X)
                .ThenBy(t => t.Y)
                .ToList();

            if (sortedTiles.Count == 0) return groups;

            var currentCluster = new List<TileModel>();
            double clusterMaxX = 0;
            int groupIndexCounter = 0;

            foreach (var tile in sortedTiles)
            {
                double tileRight = tile.X + tile.PixelWidth;

                // Стандартное расстояние между плитками внутри группы — 12px.
                // Расстояние между отдельными группами (gutter) — 42px.
                // Если плитка начинается более чем через 28px после правого края текущего кластера, это новая группа.
                if (currentCluster.Count > 0 && tile.X > (clusterMaxX + 28.0))
                {
                    double minX = currentCluster.Min(t => t.X);
                    double maxX = currentCluster.Max(t => t.X + t.PixelWidth);
                    string name = currentCluster.FirstOrDefault(t =>
                        !string.IsNullOrWhiteSpace(t.GroupName) &&
                        !t.GroupName.Equals("Main", StringComparison.OrdinalIgnoreCase))?.GroupName ?? "";

                    groups.Add(new TileGroupInfo
                    {
                        GroupIndex = groupIndexCounter++,
                        Name = name,
                        Left = minX,
                        Width = Math.Max(260.0, maxX - minX),
                        Tiles = new List<TileModel>(currentCluster)
                    });

                    currentCluster.Clear();
                    clusterMaxX = 0;
                }

                currentCluster.Add(tile);
                if (tileRight > clusterMaxX)
                {
                    clusterMaxX = tileRight;
                }
            }

            if (currentCluster.Count > 0)
            {
                double minX = currentCluster.Min(t => t.X);
                double maxX = currentCluster.Max(t => t.X + t.PixelWidth);
                string name = currentCluster.FirstOrDefault(t =>
                    !string.IsNullOrWhiteSpace(t.GroupName) &&
                    !t.GroupName.Equals("Main", StringComparison.OrdinalIgnoreCase))?.GroupName ?? "";

                groups.Add(new TileGroupInfo
                {
                    GroupIndex = groupIndexCounter++,
                    Name = name,
                    Left = minX,
                    Width = Math.Max(260.0, maxX - minX),
                    Tiles = new List<TileModel>(currentCluster)
                });
            }

            return groups;
        }

        public void ToggleGroupNamingMode()
        {
            _isGroupNamingMode = !_isGroupNamingMode;
            _currentlyEditingGroupIndex = null;
            UpdateGroupHeaders();
        }

        public void EnterGroupNamingMode(int? initialEditGroupIndex = null)
        {
            _isGroupNamingMode = true;
            _currentlyEditingGroupIndex = initialEditGroupIndex;
            UpdateGroupHeaders(initialEditGroupIndex);
        }

        public void ExitGroupNamingMode()
        {
            if (!_isGroupNamingMode && _currentlyEditingGroupIndex == null) return;
            _isGroupNamingMode = false;
            _currentlyEditingGroupIndex = null;
            UpdateGroupHeaders();
        }

        private void CommitGroupName(int groupIndex, string newName)
        {
            var groups = GetTileGroups();
            var group = groups.FirstOrDefault(g => g.GroupIndex == groupIndex);
            if (group != null)
            {
                foreach (var tile in group.Tiles)
                {
                    tile.GroupName = newName;
                }
                SaveLayoutConfig();
            }
            _currentlyEditingGroupIndex = null;
            UpdateGroupHeaders();
        }

        public void UpdateGroupHeaders(int? focusEditGroupIndex = null)
        {
            if (GroupHeadersCanvas == null) return;
            GroupHeadersCanvas.Children.Clear();

            if (StartTilesCanvas != null)
            {
                GroupHeadersCanvas.Width = StartTilesCanvas.Width;
                if (StartScrollCanvas != null) StartScrollCanvas.Width = StartTilesCanvas.Width;
            }

            if (focusEditGroupIndex.HasValue)
            {
                _currentlyEditingGroupIndex = focusEditGroupIndex.Value;
            }

            var groups = GetTileGroups();

            foreach (var group in groups)
            {
                bool isThisGroupEditing = _isGroupNamingMode && (_currentlyEditingGroupIndex == group.GroupIndex);

                // В обычном режиме, если у группы нет названия, заголовок не отображается (как в Windows 8.1)
                if (!_isGroupNamingMode && string.IsNullOrWhiteSpace(group.Name))
                {
                    continue;
                }

                var container = new Border
                {
                    Height = 32,
                    Width = Math.Max(180, group.Width),
                    CornerRadius = new CornerRadius(0)
                };
                Canvas.SetLeft(container, group.Left);
                Canvas.SetTop(container, 0);

                if (!_isGroupNamingMode)
                {
                    // === ОБЫЧНЫЙ РЕЖИМ (Аутентичный парящий заголовок Windows 8.1) ===
                    container.Background = Brushes.Transparent;
                    container.Cursor = Cursors.Hand;
                    container.ToolTip = LocalizationManager.CurrentLanguage == "en" ? "Click to rename group" : "Нажмите для переименования";

                    var tb = new TextBlock
                    {
                        Text = group.Name,
                        FontFamily = (FontFamily)FindResource("SegoeRegular"),
                        FontSize = 16,
                        FontWeight = FontWeights.Normal,
                        Foreground = new SolidColorBrush(Color.FromArgb(0xEE, 0xFF, 0xFF, 0xFF)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0, 4, 0)
                    };
                    container.Child = tb;

                    container.PreviewMouseLeftButtonDown += (s, e) =>
                    {
                        EnterGroupNamingMode(group.GroupIndex);
                        e.Handled = true;
                    };
                }
                else
                {
                    // === РЕЖИМ НАЗВАНИЯ ГРУПП (Аутентичная Metro плашка и ввод текста) ===
                    if (isThisGroupEditing)
                    {
                        container.Background = new SolidColorBrush(Color.FromArgb(0xF0, 0x1A, 0x1A, 0x1A));
                        container.BorderBrush = Brushes.White;
                        container.BorderThickness = new Thickness(1);

                        var grid = new Grid();
                        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        var textBox = new TextBox
                        {
                            Text = group.Name,
                            FontFamily = (FontFamily)FindResource("SegoeRegular"),
                            FontSize = 16,
                            Foreground = Brushes.White,
                            Background = Brushes.Transparent,
                            BorderThickness = new Thickness(0),
                            CaretBrush = Brushes.White,
                            SelectionBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                            Padding = new Thickness(8, 0, 4, 0),
                            VerticalAlignment = VerticalAlignment.Stretch,
                            VerticalContentAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Stretch
                        };
                        Grid.SetColumn(textBox, 0);
                        grid.Children.Add(textBox);

                        var clearBtn = new Button
                        {
                            Width = 26,
                            Height = 26,
                            Margin = new Thickness(0, 0, 3, 0),
                            Background = Brushes.Transparent,
                            BorderThickness = new Thickness(0),
                            Cursor = Cursors.Hand,
                            ToolTip = LocalizationManager.CurrentLanguage == "en" ? "Clear" : "Очистить",
                            Content = "✕",
                            FontSize = 11,
                            Foreground = new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF)),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        clearBtn.Click += (s, e) =>
                        {
                            textBox.Text = "";
                            textBox.Focus();
                            e.Handled = true;
                        };
                        Grid.SetColumn(clearBtn, 1);
                        grid.Children.Add(clearBtn);

                        container.Child = grid;

                        bool committed = false;
                        Action commitAction = () =>
                        {
                            if (committed) return;
                            committed = true;
                            _currentlyEditingGroupIndex = null;
                            string newName = textBox.Text.Trim();
                            CommitGroupName(group.GroupIndex, newName);
                        };

                        textBox.KeyDown += (s, e) =>
                        {
                            if (e.Key == Key.Enter)
                            {
                                commitAction();
                                e.Handled = true;
                            }
                            else if (e.Key == Key.Escape)
                            {
                                committed = true;
                                _currentlyEditingGroupIndex = null;
                                UpdateGroupHeaders();
                                e.Handled = true;
                            }
                            else if (e.Key == Key.Tab)
                            {
                                committed = true;
                                _currentlyEditingGroupIndex = null;
                                string newName = textBox.Text.Trim();
                                CommitGroupName(group.GroupIndex, newName);
                                var allGroups = GetTileGroups();
                                var nextGroup = allGroups.FirstOrDefault(g => g.GroupIndex > group.GroupIndex) ?? allGroups.FirstOrDefault();
                                if (nextGroup != null)
                                {
                                    EnterGroupNamingMode(nextGroup.GroupIndex);
                                }
                                e.Handled = true;
                            }
                        };

                        bool allowLostFocusCommit = false;
                        var debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
                        debounceTimer.Tick += (s, e) =>
                        {
                            debounceTimer.Stop();
                            allowLostFocusCommit = true;
                        };
                        debounceTimer.Start();

                        textBox.LostFocus += (s, e) =>
                        {
                            if (allowLostFocusCommit && !committed)
                            {
                                commitAction();
                            }
                        };

                        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                        {
                            textBox.Focus();
                            Keyboard.Focus(textBox);
                            textBox.SelectAll();
                        }));
                    }
                    else
                    {
                        container.Background = new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0));
                        container.BorderBrush = new SolidColorBrush(Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF));
                        container.BorderThickness = new Thickness(1);
                        container.Cursor = Cursors.Hand;

                        container.MouseEnter += (s, e) =>
                        {
                            container.Background = new SolidColorBrush(Color.FromArgb(0x66, 0x33, 0x33, 0x33));
                        };
                        container.MouseLeave += (s, e) =>
                        {
                            container.Background = new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0));
                        };

                        var tb = new TextBlock
                        {
                            FontFamily = (FontFamily)FindResource("SegoeRegular"),
                            FontSize = 16,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(8, 0, 8, 0)
                        };

                        if (string.IsNullOrWhiteSpace(group.Name))
                        {
                            tb.Text = LocalizationManager.Get("GroupNamePlaceholder", "Назвать группу");
                            tb.Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));
                        }
                        else
                        {
                            tb.Text = group.Name;
                            tb.Foreground = Brushes.White;
                        }

                        container.Child = tb;

                        container.PreviewMouseLeftButtonDown += (s, e) =>
                        {
                            _currentlyEditingGroupIndex = group.GroupIndex;
                            UpdateGroupHeaders(group.GroupIndex);
                            e.Handled = true;
                        };
                    }
                }

                GroupHeadersCanvas.Children.Add(container);
            }
        }

        public static double GetBlockX(int blockIndex)
        {
            // Windows 8.1 Standard Columns:
            // Group 1 (Left): Block 0 (X=0), Block 1 (X=312)
            // Gutter: 42px
            // Group 2 (Center): Block 2 (X=666), Block 3 (X=978)
            // Gutter: 42px
            // Group 3 (Right): Block 4 (X=1332), Block 5 (X=1644)...
            if (blockIndex <= 0) return 0.0;
            if (blockIndex == 1) return 312.0;
            if (blockIndex == 2) return 666.0;
            if (blockIndex == 3) return 978.0;
            if (blockIndex == 4) return 1332.0;
            if (blockIndex == 5) return 1644.0;
            int extra = blockIndex - 6;
            int groupPairs = extra / 2;
            int withinGroup = extra % 2;
            return 1998.0 + groupPairs * (312.0 * 2 + 42.0) + withinGroup * 312.0;
        }

        public static int GetNearestBlockIndex(double curX)
        {
            if (curX < 309.0) return 0;
            if (curX < 645.0) return 1;
            if (curX < 957.0) return 2;
            if (curX < 1311.0) return 3;
            if (curX < 1623.0) return 4;
            if (curX < 1977.0) return 5;
            for (int b = 6; b < 100; b++)
            {
                double bx = GetBlockX(b);
                if (curX < bx + 156.0) return b;
            }
            return 100;
        }

        public static double SnapXForTileSize(double curX, TileSize size)
        {
            if (curX <= 0) return 0.0;

            if (size == TileSize.Wide || size == TileSize.Large)
            {
                double bestX = 0;
                double minDiff = double.MaxValue;
                for (int b = 0; b < 60; b++)
                {
                    double bx = GetBlockX(b);
                    double diff = Math.Abs(curX - bx);
                    if (diff < minDiff)
                    {
                        minDiff = diff;
                        bestX = bx;
                    }
                    if (bx > curX + 350) break;
                }
                return bestX;
            }
            else if (size == TileSize.Medium)
            {
                double bestX = 0;
                double minDiff = double.MaxValue;
                for (int b = 0; b < 60; b++)
                {
                    double bx = GetBlockX(b);
                    for (int c = 0; c < 2; c++)
                    {
                        double sx = bx + c * 156.0;
                        double diff = Math.Abs(curX - sx);
                        if (diff < minDiff)
                        {
                            minDiff = diff;
                            bestX = sx;
                        }
                    }
                    if (bx > curX + 350) break;
                }
                return bestX;
            }
            else // Small
            {
                double bestX = 0;
                double minDiff = double.MaxValue;
                for (int b = 0; b < 60; b++)
                {
                    double bx = GetBlockX(b);
                    for (int c = 0; c < 4; c++)
                    {
                        double sx = bx + c * 78.0;
                        double diff = Math.Abs(curX - sx);
                        if (diff < minDiff)
                        {
                            minDiff = diff;
                            bestX = sx;
                        }
                    }
                    if (bx > curX + 350) break;
                }
                return bestX;
            }
        }

        public static double GetAvailableCanvasHeight()
        {
            // Фиксированная эталонная высота сетки под 1920х1080 (5 рядов плиток: 5 * 156 = 780px + 40px запас = 820px)
            // Это гарантирует, что сетка и ряды плиток никогда не сжимаются и не съезжают
            return 820.0;
        }

        public static int GetMaxStandardRows()
        {
            // Эталонная сетка Windows 8.1 под разрешение 1920х1080: всегда строго 5 рядов
            return 5;
        }

        public void UpdateAdaptiveGridDimensions()
        {
            double canvasH = GetAvailableCanvasHeight();
            if (StartTilesCanvas != null)
            {
                StartTilesCanvas.Height = canvasH;
            }
            if (StartScrollCanvas != null)
            {
                StartScrollCanvas.Height = canvasH + 50.0;
            }
        }

        public void SanitizeTileLayout(IEnumerable<TileModel> tiles)
        {
            if (tiles == null) return;
            var tileList = tiles.ToList();
            if (tileList.Count == 0) return;

            bool anyChanged = false;
            var occupied = new List<(TileModel tile, Rect rect)>();

            int maxRows = GetMaxStandardRows();
            double maxStandardY = Math.Max(0, (maxRows - 1) * 156.0);
            double maxLargeY = Math.Max(0, (maxRows - 2) * 156.0);
            double maxSmallY = Math.Max(0, (maxRows * 2 - 1) * 78.0);
            double maxCanvasH = GetAvailableCanvasHeight();

            bool FitsVertically(TileModel t, double testY)
            {
                double h = t.PixelHeight;
                if (testY < 0) return false;
                if (t.Size == TileSize.Large || h >= 250)
                {
                    return testY <= maxLargeY + 4 && (testY + h <= maxCanvasH + 8);
                }
                if (t.Size == TileSize.Small)
                {
                    return testY <= maxSmallY + 4 && (testY + h <= maxCanvasH + 8);
                }
                return testY <= maxStandardY + 4 && (testY + h <= maxCanvasH + 8);
            }

            void FindFreeSlot(TileModel t, int startBlock, out double outX, out double outY)
            {
                double w = t.PixelWidth;
                double h = t.PixelHeight;
                outX = t.X;
                outY = t.Y;

                for (int pass = 0; pass < 2; pass++)
                {
                    int minB = (pass == 0) ? Math.Max(0, startBlock) : 0;
                    int maxB = (pass == 0) ? Math.Max(0, startBlock) + 30 : Math.Max(0, startBlock);

                    for (int b = minB; b < maxB; b++)
                    {
                        double blockX = GetBlockX(b);

                        if (t.Size == TileSize.Wide)
                        {
                            for (int r = 0; r < maxRows; r++)
                            {
                                double testY = r * 156.0;
                                if (!FitsVertically(t, testY)) continue;
                                Rect testRect = new Rect(blockX, testY, w, h);
                                if (!occupied.Any(o => Rect.Intersect(testRect, o.rect).Width > 4 && Rect.Intersect(testRect, o.rect).Height > 4))
                                {
                                    outX = blockX;
                                    outY = testY;
                                    return;
                                }
                            }
                        }
                        else if (t.Size == TileSize.Large)
                        {
                            for (int r = 0; r <= Math.Max(0, maxRows - 2); r++)
                            {
                                double testY = r * 156.0;
                                if (!FitsVertically(t, testY)) continue;
                                Rect testRect = new Rect(blockX, testY, w, h);
                                if (!occupied.Any(o => Rect.Intersect(testRect, o.rect).Width > 4 && Rect.Intersect(testRect, o.rect).Height > 4))
                                {
                                    outX = blockX;
                                    outY = testY;
                                    return;
                                }
                            }
                        }
                        else if (t.Size == TileSize.Medium)
                        {
                            for (int c = 0; c < 2; c++)
                            {
                                double subColX = blockX + c * 156.0;
                                for (int r = 0; r < maxRows; r++)
                                {
                                    double testY = r * 156.0;
                                    if (!FitsVertically(t, testY)) continue;
                                    Rect testRect = new Rect(subColX, testY, w, h);
                                    if (!occupied.Any(o => Rect.Intersect(testRect, o.rect).Width > 4 && Rect.Intersect(testRect, o.rect).Height > 4))
                                    {
                                        outX = subColX;
                                        outY = testY;
                                        return;
                                    }
                                }
                            }
                        }
                        else // Small
                        {
                            for (int c = 0; c < 4; c++)
                            {
                                double subColX = blockX + c * 78.0;
                                for (int r = 0; r < maxRows * 2; r++)
                                {
                                    double testY = r * 78.0;
                                    if (!FitsVertically(t, testY)) continue;
                                    Rect testRect = new Rect(subColX, testY, w, h);
                                    if (!occupied.Any(o => Rect.Intersect(testRect, o.rect).Width > 4 && Rect.Intersect(testRect, o.rect).Height > 4))
                                    {
                                        outX = subColX;
                                        outY = testY;
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            var inBounds = tileList.Where(t => FitsVertically(t, t.Y)).ToList();
            var overflow = tileList.Where(t => !FitsVertically(t, t.Y)).ToList();

            inBounds.Sort((a, b) =>
            {
                int cmpX = a.X.CompareTo(b.X);
                if (cmpX != 0) return cmpX;
                return a.Y.CompareTo(b.Y);
            });

            foreach (var t in inBounds)
            {
                double w = t.PixelWidth;
                double h = t.PixelHeight;
                double curX = t.X;
                double curY = t.Y;

                double validX = SnapXForTileSize(curX, t.Size);
                double validY;
                if (t.Size == TileSize.Large || h >= 250)
                {
                    validY = Math.Clamp(Math.Round(curY / 156.0) * 156.0, 0, maxLargeY);
                }
                else if (t.Size == TileSize.Small)
                {
                    validY = Math.Clamp(Math.Round(curY / 78.0) * 78.0, 0, maxSmallY);
                }
                else
                {
                    validY = Math.Clamp(Math.Round(curY / 156.0) * 156.0, 0, maxStandardY);
                }

                if (Math.Abs(curX - validX) > 4) { curX = validX; anyChanged = true; }
                if (Math.Abs(curY - validY) > 4) { curY = validY; anyChanged = true; }

                Rect candidateRect = new Rect(curX, curY, w, h);
                bool hasConflict = occupied.Any(o =>
                {
                    Rect inter = Rect.Intersect(candidateRect, o.rect);
                    return !inter.IsEmpty && inter.Width > 4 && inter.Height > 4;
                });

                if (hasConflict)
                {
                    anyChanged = true;
                    int startBlock = GetNearestBlockIndex(curX);
                    FindFreeSlot(t, startBlock, out curX, out curY);
                }

                if (Math.Abs(t.X - curX) > 1 || Math.Abs(t.Y - curY) > 1) anyChanged = true;
                t.X = curX;
                t.Y = curY;
                occupied.Add((t, new Rect(curX, curY, w, h)));
            }

            overflow.Sort((a, b) =>
            {
                int cmpY = a.Y.CompareTo(b.Y);
                if (cmpY != 0) return cmpY;
                return a.X.CompareTo(b.X);
            });

            foreach (var t in overflow)
            {
                anyChanged = true;
                double w = t.PixelWidth;
                double h = t.PixelHeight;
                int startBlock = GetNearestBlockIndex(t.X);
                FindFreeSlot(t, startBlock, out double curX, out double curY);

                t.X = curX;
                t.Y = curY;
                occupied.Add((t, new Rect(curX, curY, w, h)));
            }

            if (anyChanged)
            {
                SaveLayoutConfig();
            }
        }


        public void RelayoutTileControls(bool animate = true)
        {
            if (StartTilesCanvas == null) return;
            double maxRight = 0;

            foreach (var control in StartTilesCanvas.Children.OfType<LiveTileControl>())
            {
                if (control.DataContext is TileModel tm)
                {
                    if (animate)
                    {
                        control.AnimateTo(tm.X, tm.Y, 220);
                    }
                    else
                    {
                        Canvas.SetLeft(control, tm.X);
                        Canvas.SetTop(control, tm.Y);
                    }

                    if (tm.X + tm.PixelWidth > maxRight)
                    {
                        maxRight = tm.X + tm.PixelWidth + 100;
                    }
                }
            }

            SyncCanvasWidths(Math.Max(2400, maxRight + 200));
            UpdateGroupHeaders();
        }

        public void ToggleScreen(System.Windows.Forms.Screen? targetScreen = null)
        {
            if (_screenState == ScreenState.Open || _screenState == ScreenState.Opening)
            {
                if (targetScreen == null || IsCurrentScreen(targetScreen))
                {
                    CloseScreenAnimated();
                }
                else
                {
                    ApplyMonitorConfiguration(targetScreen);
                }
            }
            else
            {
                OpenScreenAnimated(targetScreen);
            }
        }

        public void OpenScreenAnimated(System.Windows.Forms.Screen? targetScreen = null)
        {
            if (_screenState == ScreenState.Open && targetScreen != null && !IsCurrentScreen(targetScreen))
            {
                ApplyMonitorConfiguration(targetScreen);
                return;
            }

            if (_screenState == ScreenState.Open || _screenState == ScreenState.Opening) return;

            ApplyMonitorConfiguration(targetScreen);

            _screenState = ScreenState.Opening;
            _activeView = ActiveView.Start;
            _isNavigating = false;
            UpdateDesktopTileWallpaper();
            UpdateGroupHeaders();

            // Сбрасываем предыдущие анимации
            WallpaperCanvas.BeginAnimation(OpacityProperty, null);
            StartScreenContainer.BeginAnimation(OpacityProperty, null);
            StartScreenTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            StartScreenTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            AppsScreenContainer.BeginAnimation(OpacityProperty, null);
            AppsScreenTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            AppsScreenTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            this.BeginAnimation(OpacityProperty, null);
            this.Opacity = 1.0;

            // Начальное состояние:
            // 1. Декоративный фон с лентами и градиентом скрыт (0.0), видна базовая подложка темы
            WallpaperCanvas.Opacity = 0.0;
            bool hasCustomWp = !string.IsNullOrEmpty(ThemeManager.CurrentTheme.CustomWallpaperPath) && File.Exists(ThemeManager.CurrentTheme.CustomWallpaperPath);
            if (RibbonContainer != null)
            {
                RibbonContainer.BeginAnimation(OpacityProperty, null);
                RibbonTranslate?.BeginAnimation(TranslateTransform.YProperty, null);
                if (RibbonTranslate != null) RibbonTranslate.Y = 0;
                RibbonContainer.Opacity = 0.0;
                RibbonContainer.Visibility = hasCustomWp ? Visibility.Collapsed : Visibility.Visible;
            }
            if (RibbonCanvas != null)
            {
                RibbonCanvas.BeginAnimation(OpacityProperty, null);
                RibbonCanvas.Opacity = 1.0;
                RibbonCanvas.Visibility = hasCustomWp ? Visibility.Collapsed : Visibility.Visible;
            }

            // 2. Экран приложений скрыт за нижней границей
            double h = ActualHeight > 0 ? ActualHeight : SystemParameters.PrimaryScreenHeight;
            double offscreenY = h > 0 ? h : 1080.0;
            AppsScreenContainer.Opacity = 1.0;
            AppsScreenContainer.Visibility = Visibility.Hidden;
            AppsScreenTranslate.X = 0;
            AppsScreenTranslate.Y = offscreenY;

            // 3. Экран Пуск видим и подготавливается к появлению
            StartScreenContainer.Visibility = Visibility.Visible;
            StartScreenContainer.Opacity = 1.0;
            StartScreenTranslate.Y = 0;

            if (_isSemanticZoomedOut)
            {
                _isSemanticZoomedOut = false;
                ZoomedOutJumpGridLayer.Visibility = Visibility.Collapsed;
                ZoomedInAppsLayer.Opacity = 1.0;
            }

            foreach (var tile in StartTilesCanvas.Children.OfType<LiveTileControl>())
            {
                tile.ResetVisualState();
            }

            CloseAllFlyouts();
            CloseSearchCharm();
            ClosePersonalize();
            DeselectAllTiles();

            UncloakWindow();

            // 1. СНАЧАЛА ПОЯВЛЯЮТСЯ ПЛИТКИ: каскадная волна плиток
            AnimateTilesEntrance();

            // Мягкий сдвиг заголовка и навигации (35px -> 0px) за 240мс
            StartScreenTranslate.X = 35.0;
            var slideIn = new DoubleAnimation(35.0, 0.0, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            StartScreenTranslate.BeginAnimation(TranslateTransform.XProperty, slideIn);

            // 2. ЗАТЕМ ПЛАВНО ПРОЯВЛЯЕТСЯ САМ ВЕСЬ ФОН (0.0 -> 1.0)
            var bgFadeIn = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(260),
                BeginTime = TimeSpan.FromMilliseconds(90), // элегантная задержка: сначала плитки, затем заливается фон
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            bgFadeIn.Completed += (s, e) =>
            {
                if (_screenState == ScreenState.Opening)
                {
                    _screenState = ScreenState.Open;
                    LiveTileCoordinator.Start();
                    WallpaperCanvas.BeginAnimation(OpacityProperty, null);
                    WallpaperCanvas.Opacity = 1.0;
                    if (RibbonContainer != null)
                    {
                        RibbonContainer.BeginAnimation(OpacityProperty, null);
                        RibbonContainer.Opacity = 1.0;
                    }
                    StartScreenTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                    StartScreenTranslate.X = 0;
                }
            };
            WallpaperCanvas.BeginAnimation(OpacityProperty, bgFadeIn);
            if (RibbonContainer != null && !hasCustomWp) RibbonContainer.BeginAnimation(OpacityProperty, bgFadeIn);
        }

        public void CloseScreenAnimated()
        {
            if (_screenState == ScreenState.Closed || _screenState == ScreenState.Closing) return;

            _screenState = ScreenState.Closing;
            LiveTileCoordinator.Stop();

            CloseAllFlyouts();
            CloseSearchCharm();
            ClosePersonalize();
            DeselectAllTiles();

            // Сбрасываем активные анимации
            WallpaperCanvas.BeginAnimation(OpacityProperty, null);
            StartScreenContainer.BeginAnimation(OpacityProperty, null);
            StartScreenTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            AppsScreenContainer.BeginAnimation(OpacityProperty, null);
            AppsScreenTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            RibbonContainer?.BeginAnimation(OpacityProperty, null);
            RibbonTranslate?.BeginAnimation(TranslateTransform.YProperty, null);
            this.BeginAnimation(OpacityProperty, null);

            var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };

            // 1. СНАЧАЛА ПЛИТКИ (И СОДЕРЖИМОЕ ЭКРАНА): быстро и плавно угасают со сдвигом влево (0мс -> 130мс)
            var contentFadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(130),
                EasingFunction = easeIn
            };

            var contentSlideOut = new DoubleAnimation
            {
                From = 0.0,
                To = -35.0,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = easeIn
            };

            if (_activeView == ActiveView.Apps)
            {
                AppsScreenContainer.BeginAnimation(OpacityProperty, contentFadeOut);
                AppsScreenTranslate.BeginAnimation(TranslateTransform.XProperty, contentSlideOut);
            }
            else
            {
                StartScreenContainer.BeginAnimation(OpacityProperty, contentFadeOut);
                StartScreenTranslate.BeginAnimation(TranslateTransform.XProperty, contentSlideOut);
            }

            // 2. ЗАТЕМ ПЛАВНО ИСЧЕЗАЮТ ВЕСЬ ФОН, ЛИНИЯ И ОКНО К РАБОЧЕМУ СТОЛУ (70мс -> 220мс)
            // Задержка 70мс позволяет плиткам уже почти полностью исчезнуть до ухода фона,
            // что кардинально снижает нагрузку на GPU и исключает любые лаги (butter-smooth 60-144 FPS).
            var bgFadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                BeginTime = TimeSpan.FromMilliseconds(70),
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = easeIn
            };

            var winFadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                BeginTime = TimeSpan.FromMilliseconds(70),
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = easeIn
            };

            winFadeOut.Completed += (s, e) =>
            {
                if (_screenState == ScreenState.Closing)
                {
                    _screenState = ScreenState.Closed;
                    CloakWindow();

                    this.BeginAnimation(OpacityProperty, null);
                    this.Opacity = 1.0;

                    WallpaperCanvas.BeginAnimation(OpacityProperty, null);
                    WallpaperCanvas.Opacity = 1.0;

                    StartScreenTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                    StartScreenTranslate.X = 0;
                    StartScreenContainer.BeginAnimation(OpacityProperty, null);
                    StartScreenContainer.Opacity = 1.0;

                    AppsScreenTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                    AppsScreenTranslate.X = 0;
                    AppsScreenContainer.BeginAnimation(OpacityProperty, null);
                    AppsScreenContainer.Opacity = 1.0;

                    // Всегда сбрасываем вид на экран Пуск
                    _activeView = ActiveView.Start;
                    _isNavigating = false;
                    StartScreenContainer.Visibility = Visibility.Visible;
                    StartScreenTranslate.Y = 0;
                    AppsScreenContainer.Visibility = Visibility.Hidden;
                    double h = ActualHeight > 0 ? ActualHeight : SystemParameters.PrimaryScreenHeight;
                    AppsScreenTranslate.Y = h > 0 ? h : 1080.0;
                    bool hasCustomWpClose = !string.IsNullOrEmpty(ThemeManager.CurrentTheme.CustomWallpaperPath) && File.Exists(ThemeManager.CurrentTheme.CustomWallpaperPath);
                    if (RibbonContainer != null)
                    {
                        RibbonContainer.BeginAnimation(OpacityProperty, null);
                        RibbonTranslate?.BeginAnimation(TranslateTransform.YProperty, null);
                        if (RibbonTranslate != null) RibbonTranslate.Y = 0;
                        RibbonContainer.Opacity = hasCustomWpClose ? 0.0 : 1.0;
                        RibbonContainer.Visibility = hasCustomWpClose ? Visibility.Collapsed : Visibility.Visible;
                    }
                    if (RibbonCanvas != null)
                    {
                        RibbonCanvas.BeginAnimation(OpacityProperty, null);
                        RibbonCanvas.Opacity = hasCustomWpClose ? 0.0 : 1.0;
                        RibbonCanvas.Visibility = hasCustomWpClose ? Visibility.Collapsed : Visibility.Visible;
                    }

                    foreach (var tile in StartTilesCanvas.Children.OfType<LiveTileControl>())
                    {
                        tile.ResetVisualState();
                    }
                }
            };

            WallpaperCanvas.BeginAnimation(OpacityProperty, bgFadeOut);
            if (RibbonContainer != null && RibbonContainer.Visibility == Visibility.Visible)
            {
                RibbonContainer.BeginAnimation(OpacityProperty, bgFadeOut);
            }
            this.BeginAnimation(OpacityProperty, winFadeOut);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F12)
            {
                TakeDebugScreenshot();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                if (UserFlyout?.Visibility == Visibility.Visible || PowerFlyout?.Visibility == Visibility.Visible)
                {
                    CloseAllFlyouts();
                    e.Handled = true;
                    return;
                }

                if (_isEyedropperActive)
                {
                    StopEyedropper(applyColor: false);
                    e.Handled = true;
                    return;
                }

                var draggingTile = StartTilesCanvas.Children.OfType<LiveTileControl>().FirstOrDefault(t => t.IsDragging);
                if (draggingTile != null)
                {
                    draggingTile.CancelDrag();
                    e.Handled = true;
                    return;
                }

                if (_isSemanticZoomedOut)
                {
                    SemanticZoomIn();
                }
                else if (_isPersonalizeOpen)
                {
                    ClosePersonalize();
                }
                else if (_isSearchCharmOpen)
                {
                    CloseSearchCharm();
                }
                else if (StartTiles.Any(t => t.IsSelected))
                {
                    DeselectAllTiles();
                }
                else if (_isGroupNamingMode)
                {
                    ExitGroupNamingMode();
                }
                else if (_activeView == ActiveView.Apps)
                {
                    NavigateToStart_Click(this, new RoutedEventArgs());
                }
                else
                {
                    CloseScreenAnimated();
                }
                e.Handled = true;
            }
            else if ((e.Key == Key.S || e.Key == Key.Q) && (Keyboard.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
            {
                OpenSearchCharm();
                e.Handled = true;
            }
            else if (e.Key == Key.I && (Keyboard.Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
            {
                PersonalizeToggle_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (!_isSearchCharmOpen && !_isPersonalizeOpen && !_isSemanticZoomedOut && 
                     Keyboard.Modifiers == ModifierKeys.None &&
                     ((e.Key >= Key.A && e.Key <= Key.Z) || 
                      (e.Key >= Key.D0 && e.Key <= Key.D9) || 
                      (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) ||
                      e.Key == Key.Oem1 || e.Key == Key.Oem2 || e.Key == Key.Oem3 || 
                      e.Key == Key.Oem4 || e.Key == Key.Oem5 || e.Key == Key.Oem6 || 
                      e.Key == Key.OemOpenBrackets || e.Key == Key.OemCloseBrackets || 
                      e.Key == Key.OemQuotes || e.Key == Key.OemComma || e.Key == Key.OemPeriod))
            {
                OpenSearchCharm();
            }
        }

        public void SwitchToAppsForDebugScreenshot()
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen");
            File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] SwitchToAppsForDebugScreenshot called\n");

            Show();
            UncloakWindow();
            _activeView = ActiveView.Apps;
            StartScreenContainer.Visibility = Visibility.Hidden;
            AppsScreenContainer.Visibility = Visibility.Visible;
            AppsScreenTranslate.Y = 0;
            AppsScreenContainer.Opacity = 1.0;
            if (RibbonContainer != null)
            {
                RibbonContainer.BeginAnimation(OpacityProperty, null);
                if (RibbonTranslate != null)
                {
                    RibbonTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                    RibbonTranslate.Y = -2000;
                }
                RibbonContainer.Opacity = 0.0;
                RibbonContainer.Visibility = Visibility.Collapsed;
            }
            if (RibbonCanvas != null)
            {
                RibbonCanvas.BeginAnimation(OpacityProperty, null);
                RibbonCanvas.Opacity = 0.0;
                RibbonCanvas.Visibility = Visibility.Collapsed;
            }

            UpdateLayout();

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(800)
            };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Timer tick, taking screenshot\n");
                TakeDebugScreenshot("apps_screen_live.png");
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Screenshot taken, shutting down\n");
                System.Windows.Application.Current.Shutdown();
            };
            timer.Start();
        }

        public void SwitchToStartForHoverScreenshot()
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen");
            File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] SwitchToStartForHoverScreenshot called\n");

            Show();
            UncloakWindow();
            _activeView = ActiveView.Start;
            StartScreenContainer.Visibility = Visibility.Visible;
            AppsScreenContainer.Visibility = Visibility.Hidden;
            StartScreenTranslate.X = 0;
            StartScreenContainer.Opacity = 1.0;
            bool hasCustomWp = !string.IsNullOrEmpty(ThemeManager.CurrentTheme.CustomWallpaperPath) && File.Exists(ThemeManager.CurrentTheme.CustomWallpaperPath);
            if (RibbonContainer != null)
            {
                RibbonContainer.BeginAnimation(OpacityProperty, null);
                if (RibbonTranslate != null)
                {
                    RibbonTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                    RibbonTranslate.Y = 0;
                }
                RibbonContainer.Opacity = 1.0;
                RibbonContainer.Visibility = hasCustomWp ? Visibility.Collapsed : Visibility.Visible;
            }
            if (RibbonCanvas != null)
            {
                RibbonCanvas.BeginAnimation(OpacityProperty, null);
                RibbonCanvas.Opacity = 1.0;
                RibbonCanvas.Visibility = hasCustomWp ? Visibility.Collapsed : Visibility.Visible;
            }

            UpdateLayout();

            // Simulate hover over Store tile
            var storeTile = StartTilesCanvas.Children.OfType<LiveTileControl>().FirstOrDefault(t => (t.DataContext as Models.TileModel)?.Title == "Store");
            if (storeTile != null)
            {
                if (System.Windows.Application.Current?.Resources["ThemeTileHoverBorderBrush"] is Brush b)
                    storeTile.HoverBorder.BorderBrush = b;
                storeTile.HoverBorder.BorderThickness = new Thickness(2);
            }

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(800)
            };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Hover screenshot timer tick\n");
                TakeDebugScreenshot("store_hover_live.png");
                System.Windows.Application.Current?.Shutdown();
            };
            timer.Start();
        }

        public void SwitchToStartForLiveScreenshot()
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen");
            File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] SwitchToStartForLiveScreenshot called\n");

            Show();
            UncloakWindow();
            _activeView = ActiveView.Start;
            StartScreenContainer.Visibility = Visibility.Visible;
            AppsScreenContainer.Visibility = Visibility.Hidden;
            StartScreenTranslate.X = 0;
            StartScreenContainer.Opacity = 1.0;
            _screenState = ScreenState.Open;

            UpdateLayout();

            // Принудительно запускаем переворот на живых плитках для визуальной проверки
            foreach (var tile in StartTilesCanvas.Children.OfType<LiveTileControl>())
            {
                if (tile.DataContext is Models.TileModel m && m.IsLiveTileEnabled &&
                    (m.Title == "Sports" || m.Title == "Спорт" || 
                     m.Title == "News" || m.Title == "Новости" || 
                     m.Title == "Money" || m.Title == "Финансы" || 
                     m.Title == "Weather" || m.Title == "Погода" || 
                     m.Title == "Store" || m.Title == "Магазин" || m.IsStoreTemplate ||
                     m.Title == "Games" || m.Title == "Игры" || m.IsGamesTemplate ||
                     m.Title == "Food & Drink" || m.Title == "Кулинария" ||
                     m.Title == "Calendar" || m.Title == "Календарь"))
                {
                    tile.TriggerLiveRoll();
                }
            }

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1600)
            };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                TakeDebugScreenshot("live_tiles_preview.png");
                System.Windows.Application.Current?.Shutdown();
            };
            timer.Start();
        }

        public void SwitchToStartForUserFlyoutScreenshot()
        {
            Show();
            UncloakWindow();
            _activeView = ActiveView.Start;
            StartScreenContainer.Visibility = Visibility.Visible;
            AppsScreenContainer.Visibility = Visibility.Hidden;
            StartScreenTranslate.X = 0;
            StartScreenContainer.Opacity = 1.0;
            _screenState = ScreenState.Open;

            UpdateLayout();

            PositionUserFlyout();
            UserFlyout.Visibility = Visibility.Visible;

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                TakeDebugScreenshot("user_flyout_preview.png");
                System.Windows.Application.Current?.Shutdown();
            };
            timer.Start();
        }

        public void SwitchToStartForStaticScreenshot()
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen");
            File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] SwitchToStartForStaticScreenshot called\n");

            Show();
            UncloakWindow();
            _activeView = ActiveView.Start;
            StartScreenContainer.Visibility = Visibility.Visible;
            AppsScreenContainer.Visibility = Visibility.Hidden;
            StartScreenTranslate.X = 0;
            StartScreenTranslate.Y = 0;
            StartScreenContainer.Opacity = 1.0;
            _screenState = ScreenState.Open;

            UpdateLayout();

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                TakeDebugScreenshot("start_screen_current.png");
                System.Windows.Application.Current?.Shutdown();
            };
            timer.Start();
        }

        public void SwitchToSearchForDebugScreenshot()
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen");
            File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] SwitchToSearchForDebugScreenshot called\n");

            Show();
            UncloakWindow();
            _activeView = ActiveView.Start;
            StartScreenContainer.Visibility = Visibility.Visible;
            AppsScreenContainer.Visibility = Visibility.Hidden;
            StartScreenTranslate.X = 0;
            StartScreenContainer.Opacity = 1.0;
            bool hasCustomWpSearch = !string.IsNullOrEmpty(ThemeManager.CurrentTheme.CustomWallpaperPath) && File.Exists(ThemeManager.CurrentTheme.CustomWallpaperPath);
            if (RibbonContainer != null)
            {
                RibbonContainer.BeginAnimation(OpacityProperty, null);
                if (RibbonTranslate != null)
                {
                    RibbonTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                    RibbonTranslate.Y = 0;
                }
                RibbonContainer.Opacity = hasCustomWpSearch ? 0.0 : 1.0;
                RibbonContainer.Visibility = hasCustomWpSearch ? Visibility.Collapsed : Visibility.Visible;
            }
            if (RibbonCanvas != null)
            {
                RibbonCanvas.BeginAnimation(OpacityProperty, null);
                RibbonCanvas.Opacity = hasCustomWpSearch ? 0.0 : 1.0;
                RibbonCanvas.Visibility = hasCustomWpSearch ? Visibility.Collapsed : Visibility.Visible;
            }

            OpenSearchCharm();
            SearchCharmTranslate.X = -340;

            // Search query 'п' matching reference media_1789221550360.png
            SearchQueryInput.Text = "п";
            PerformSearch("п", triggerAutocomplete: true);

            UpdateLayout();

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(800)
            };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Search screenshot timer tick\n");
                TakeDebugScreenshot("search_charm_live.png");
                System.Windows.Application.Current?.Shutdown();
            };
            timer.Start();
        }

        public void SwitchToSettingsForDebugScreenshot(string mode = "main")
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen");
            File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] SwitchToSettingsForDebugScreenshot mode={mode} called\n");

            Show();
            UncloakWindow();
            _activeView = ActiveView.Start;
            StartScreenContainer.Visibility = Visibility.Visible;
            AppsScreenContainer.Visibility = Visibility.Hidden;
            StartScreenTranslate.X = 0;
            StartScreenContainer.Opacity = 1.0;
            bool hasCustomWp = !string.IsNullOrEmpty(ThemeManager.CurrentTheme.CustomWallpaperPath) && File.Exists(ThemeManager.CurrentTheme.CustomWallpaperPath);
            if (RibbonContainer != null)
            {
                RibbonContainer.BeginAnimation(OpacityProperty, null);
                if (RibbonTranslate != null)
                {
                    RibbonTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                    RibbonTranslate.Y = 0;
                }
                RibbonContainer.Opacity = 1.0;
                RibbonContainer.Visibility = hasCustomWp ? Visibility.Collapsed : Visibility.Visible;
            }
            if (RibbonCanvas != null)
            {
                RibbonCanvas.BeginAnimation(OpacityProperty, null);
                RibbonCanvas.Opacity = 1.0;
                RibbonCanvas.Visibility = hasCustomWp ? Visibility.Collapsed : Visibility.Visible;
            }

            OpenSettingsCharm();
            SettingsCharmTranslate.X = -360;

            string screenshotFile = "settings_charm_live.png";
            if (mode.Equals("addtile", StringComparison.OrdinalIgnoreCase))
            {
                NavigateToSettingsAddTile_Click(this, new RoutedEventArgs());
                AddTileTitleInput.Text = "Google Chrome";
                AddTilePathInput.Text = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
                _addTileSelectedColor = "#FF0078D7";
                if (AddTileColorHexInput != null) AddTileColorHexInput.Text = "#FF0078D7";
                string chromeIcon = MetroIconResolver.ResolveIconPath("Google Chrome");
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] chromeIcon resolved to '{chromeIcon}', exists={File.Exists(chromeIcon)}\n");
                if (!string.IsNullOrEmpty(chromeIcon)) _addTileCustomIconPath = chromeIcon;
                _addTileIconSize = 88;
                if (AddTileIconSizeSlider != null) AddTileIconSizeSlider.Value = 88;
                UpdateTilePreview();
                screenshotFile = "settings_addtile_live.png";
            }
            else if (mode.Equals("personalize", StringComparison.OrdinalIgnoreCase))
            {
                NavigateToSettingsPersonalize_Click(this, new RoutedEventArgs());
                screenshotFile = "settings_personalize_live.png";
            }
            else if (mode.Equals("addtile-create", StringComparison.OrdinalIgnoreCase))
            {
                NavigateToSettingsAddTile_Click(this, new RoutedEventArgs());
                AddTileTitleInput.Text = "Notepad++";
                AddTilePathInput.Text = @"C:\Windows\notepad.exe";
                _addTileSelectedColor = "#FF008A00";
                _addTileSelectedSize = TileSize.Medium;
                UpdateTilePreview();
                CreateCustomTile_Click(this, new RoutedEventArgs());
                CloseSettingsCharm();
                screenshotFile = "custom_tile_added_live.png";
            }
            else if (mode.Equals("studio", StringComparison.OrdinalIgnoreCase))
            {
                NavigateToSettingsStudio();
                StudioTileSelector.IsDropDownOpen = true;
                screenshotFile = "settings_studio_live.png";
            }
            else if (mode.Equals("monitors", StringComparison.OrdinalIgnoreCase))
            {
                NavigateToSettingsMonitors_Click(this, new RoutedEventArgs());
                screenshotFile = "settings_monitors_live.png";
            }
            else if (mode.Equals("language", StringComparison.OrdinalIgnoreCase))
            {
                NavigateToSettingsLanguage_Click(this, new RoutedEventArgs());
                screenshotFile = "settings_language_live.png";
            }
            else if (mode.Equals("founders", StringComparison.OrdinalIgnoreCase))
            {
                NavigateToSettingsFounders_Click(this, new RoutedEventArgs());
                screenshotFile = "settings_founders_live.png";
            }
            else if (mode.Equals("autostart", StringComparison.OrdinalIgnoreCase))
            {
                NavigateToSettingsAutostart_Click(this, new RoutedEventArgs());
                screenshotFile = "settings_autostart_live.png";
            }
            else
            {
                NavigateToSettingsMain();
                screenshotFile = "settings_charm_live.png";
            }

            UpdateLayout();

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(800)
            };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Settings screenshot timer tick\n");
                TakeDebugScreenshot(screenshotFile);
                System.Windows.Application.Current?.Shutdown();
            };
            timer.Start();
        }

        public void SwitchToStartForGroupNamingScreenshot()
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen");
            File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] SwitchToStartForGroupNamingScreenshot called\n");

            Show();
            UncloakWindow();
            _activeView = ActiveView.Start;
            StartScreenContainer.Visibility = Visibility.Visible;
            AppsScreenContainer.Visibility = Visibility.Hidden;
            StartScreenTranslate.X = 0;
            StartScreenTranslate.Y = 0;
            StartScreenContainer.Opacity = 1.0;
            UpdateLayout();

            EnterGroupNamingMode(0);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                TakeDebugScreenshot("group_naming_live.png");
                System.Windows.Application.Current?.Shutdown();
            };
            timer.Start();
        }

        public void TakeDebugScreenshot(string filename = "apps_screen_live.png")
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen");
            try
            {
                int w = (int)ActualWidth;
                int h = (int)ActualHeight;
                if (w <= 0 || h <= 0) { w = (int)SystemParameters.PrimaryScreenWidth; h = (int)SystemParameters.PrimaryScreenHeight; }
                if (w <= 0 || h <= 0) { w = 1920; h = 1080; }

                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] TakeDebugScreenshot w={w}, h={h}\n");

                var oldStartCache = StartScreenContainer.CacheMode;
                var oldAppsCache = AppsScreenContainer.CacheMode;

                StartScreenContainer.CacheMode = null;
                AppsScreenContainer.CacheMode = null;

                UpdateLayout();

                var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1E, 0x02, 0x3D)), null, new Rect(0, 0, w, h));
                }
                rtb.Render(dv);
                rtb.Render(this);

                StartScreenContainer.CacheMode = oldStartCache;
                AppsScreenContainer.CacheMode = oldAppsCache;

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                string outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen", "Screenshots");
                string brainDir = @"D:\Users\amir_\.gemini\antigravity\brain\261e44a7-c131-4bf3-bc4b-c8d690ed61a2\scratch";
                if (Directory.Exists(brainDir)) outDir = brainDir;
                Directory.CreateDirectory(outDir);
                string path = Path.Combine(outDir, filename);
                using var fs = File.Create(path);
                encoder.Save(fs);
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Saved screenshot to {path}, size: {new FileInfo(path).Length} bytes\n");
            }
            catch (Exception ex)
            {
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] TakeDebugScreenshot Exception: {ex}\n");
            }
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer sv)
            {
                if (e.Delta < 0)
                    sv.ScrollToHorizontalOffset(sv.HorizontalOffset + 140);
                else
                    sv.ScrollToHorizontalOffset(sv.HorizontalOffset - 140);
                e.Handled = true;
            }
        }

        // ======================== НАВИГАЦИЯ ПУСК <-> ПРИЛОЖЕНИЯ ========================

        private void NavigateToApps_Click(object sender, RoutedEventArgs e)
        {
            if (_activeView == ActiveView.Apps || _isNavigating) return;
            _activeView = ActiveView.Apps;
            _isNavigating = true;

            CloseAllFlyouts();
            DeselectAllTiles();
            LiveTileCoordinator.Stop();

            double h = ActualHeight > 0 ? ActualHeight : SystemParameters.PrimaryScreenHeight;
            double slideDistance = h > 0 ? h : 1080.0;

            // Включаем аппаратный BitmapCache на время слайд-анимации (абсолютно гладкий GPU-блит без перерисовки элементов)
            StartScreenContainer.CacheMode = new BitmapCache { EnableClearType = false, RenderAtScale = 1.0 };
            AppsScreenContainer.CacheMode = new BitmapCache { EnableClearType = false, RenderAtScale = 1.0 };

            StartScreenTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            AppsScreenTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            RibbonTranslate?.BeginAnimation(TranslateTransform.YProperty, null);

            StartScreenContainer.Visibility = Visibility.Visible;
            AppsScreenContainer.Visibility = Visibility.Visible;
            bool hasCustomWpApps = !string.IsNullOrEmpty(ThemeManager.CurrentTheme.CustomWallpaperPath) && File.Exists(ThemeManager.CurrentTheme.CustomWallpaperPath);
            if (RibbonContainer != null)
            {
                RibbonContainer.Visibility = hasCustomWpApps ? Visibility.Collapsed : Visibility.Visible;
                if (RibbonTranslate != null) RibbonTranslate.Y = 0;
            }
            StartScreenTranslate.Y = 0;
            AppsScreenTranslate.Y = slideDistance;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(260);

            // 1. Плитки уходят вверх за пределы экрана (0 -> -slideDistance) и быстро затухают
            var slideOutStart = new DoubleAnimation(0.0, -slideDistance, duration)
            {
                EasingFunction = ease
            };
            var fadeOutStart = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            // 2. Список приложений въезжает снизу (slideDistance -> 0) и плавно проявляется
            AppsScreenContainer.Opacity = 0.0;
            var slideInApps = new DoubleAnimation(slideDistance, 0.0, duration)
            {
                EasingFunction = ease
            };
            var fadeInApps = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // 3. Линии/узоры (RibbonContainer + RibbonCanvas) УХОДЯТ ВВЕРХ И ПОЛНОСТЬЮ РАСТВОРЯЮТСЯ!
            if (RibbonContainer != null)
            {
                RibbonContainer.BeginAnimation(OpacityProperty, null);
                var fadeOutRibbon = new DoubleAnimation(RibbonContainer.Opacity, 0.0, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                RibbonContainer.BeginAnimation(OpacityProperty, fadeOutRibbon);

                if (RibbonTranslate != null)
                {
                    var slideOutRibbon = new DoubleAnimation(0.0, -slideDistance, duration)
                    {
                        EasingFunction = ease
                    };
                    RibbonTranslate.BeginAnimation(TranslateTransform.YProperty, slideOutRibbon);
                }
            }
            if (RibbonCanvas != null)
            {
                RibbonCanvas.BeginAnimation(OpacityProperty, null);
                var fadeOutCanvas = new DoubleAnimation(RibbonCanvas.Opacity, 0.0, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                RibbonCanvas.BeginAnimation(OpacityProperty, fadeOutCanvas);
            }

            slideInApps.Completed += (s, ev) =>
            {
                _isNavigating = false;
                StartScreenContainer.Visibility = Visibility.Hidden;
                StartScreenTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                StartScreenTranslate.Y = -slideDistance;
                StartScreenContainer.BeginAnimation(OpacityProperty, null);
                StartScreenContainer.Opacity = 1.0;

                AppsScreenTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                AppsScreenTranslate.Y = 0;
                AppsScreenContainer.BeginAnimation(OpacityProperty, null);
                AppsScreenContainer.Opacity = 1.0;

                // Отключаем BitmapCache после завершения анимации для идеальной четкости текста
                StartScreenContainer.CacheMode = null;
                AppsScreenContainer.CacheMode = null;

                // ПОЛНОЕ ИСКЛЮЧЕНИЕ УЗОРОВ И ЛИНИЙ НА ЭКРАНЕ ПРИЛОЖЕНИЙ
                if (RibbonContainer != null)
                {
                    RibbonContainer.BeginAnimation(OpacityProperty, null);
                    if (RibbonTranslate != null)
                    {
                        RibbonTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                        RibbonTranslate.Y = -slideDistance;
                    }
                    RibbonContainer.Opacity = 0.0;
                    RibbonContainer.Visibility = Visibility.Collapsed;
                }
                if (RibbonCanvas != null)
                {
                    RibbonCanvas.BeginAnimation(OpacityProperty, null);
                    RibbonCanvas.Opacity = 0.0;
                    RibbonCanvas.Visibility = Visibility.Collapsed;
                }

                // Фоновое обновление если появились новые ярлыки
                if (_isAppInventoryDirty && !_isAppInventoryScanning)
                {
                    _isAppInventoryDirty = false;
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                    {
                        try { InitializeAppInventory(); } catch { }
                    }));
                }

                if (Environment.GetEnvironmentVariable("WIN8_DEBUG_SCREENSHOT") == "1")
                {
                    TakeDebugScreenshot("apps_screen_live.png");
                }
            };

            StartScreenTranslate.BeginAnimation(TranslateTransform.YProperty, slideOutStart);
            StartScreenContainer.BeginAnimation(OpacityProperty, fadeOutStart);

            AppsScreenTranslate.BeginAnimation(TranslateTransform.YProperty, slideInApps);
            AppsScreenContainer.BeginAnimation(OpacityProperty, fadeInApps);
        }

        private void NavigateToStart_Click(object sender, RoutedEventArgs e)
        {
            if (_activeView == ActiveView.Start || _isNavigating) return;
            _activeView = ActiveView.Start;
            _isNavigating = true;

            CloseAllFlyouts();
            if (_isSemanticZoomedOut) SemanticZoomIn();

            double h = ActualHeight > 0 ? ActualHeight : SystemParameters.PrimaryScreenHeight;
            double slideDistance = h > 0 ? h : 1080.0;

            // Включаем аппаратный BitmapCache на время слайд-анимации
            StartScreenContainer.CacheMode = new BitmapCache { EnableClearType = false, RenderAtScale = 1.0 };
            AppsScreenContainer.CacheMode = new BitmapCache { EnableClearType = false, RenderAtScale = 1.0 };

            StartScreenTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            AppsScreenTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            RibbonTranslate?.BeginAnimation(TranslateTransform.YProperty, null);

            StartScreenContainer.Visibility = Visibility.Visible;
            AppsScreenContainer.Visibility = Visibility.Visible;
            AppsScreenTranslate.Y = 0;
            StartScreenTranslate.Y = -slideDistance;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(260);

            // 1. Список приложений уходит вниз за пределы экрана (0 -> slideDistance) и быстро затухает
            var slideOutApps = new DoubleAnimation(0.0, slideDistance, duration)
            {
                EasingFunction = ease
            };
            var fadeOutApps = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            // 2. Плитки въезжают сверху (-slideDistance -> 0) и плавно проявляются
            StartScreenContainer.Opacity = 0.0;
            var slideInStart = new DoubleAnimation(-slideDistance, 0.0, duration)
            {
                EasingFunction = ease
            };
            var fadeInStart = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // 3. Линии/узоры (RibbonContainer + RibbonCanvas) ПЛАВНО ВЪЕЗЖАЮТ СВЕРХУ И ПРОЯВЛЯЮТСЯ ТОЛЬКО ЕСЛИ НЕТ КАСТОМНЫХ ОБОЕВ
            bool hasCustomWp = !string.IsNullOrEmpty(ThemeManager.CurrentTheme.CustomWallpaperPath) && File.Exists(ThemeManager.CurrentTheme.CustomWallpaperPath);
            if (RibbonContainer != null)
            {
                if (hasCustomWp)
                {
                    RibbonContainer.BeginAnimation(OpacityProperty, null);
                    if (RibbonTranslate != null) RibbonTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                    RibbonContainer.Opacity = 0.0;
                    RibbonContainer.Visibility = Visibility.Collapsed;
                }
                else
                {
                    RibbonContainer.Visibility = Visibility.Visible;
                    RibbonContainer.Opacity = 0.0;
                    RibbonContainer.BeginAnimation(OpacityProperty, null);
                    if (RibbonTranslate != null)
                    {
                        RibbonTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                        RibbonTranslate.Y = -slideDistance;
                        var slideInRibbon = new DoubleAnimation(-slideDistance, 0.0, duration)
                        {
                            EasingFunction = ease
                        };
                        RibbonTranslate.BeginAnimation(TranslateTransform.YProperty, slideInRibbon);
                    }
                    var fadeInRibbon = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    RibbonContainer.BeginAnimation(OpacityProperty, fadeInRibbon);
                }
            }
            if (RibbonCanvas != null)
            {
                if (hasCustomWp)
                {
                    RibbonCanvas.BeginAnimation(OpacityProperty, null);
                    RibbonCanvas.Opacity = 0.0;
                    RibbonCanvas.Visibility = Visibility.Collapsed;
                }
                else
                {
                    RibbonCanvas.Visibility = Visibility.Visible;
                    RibbonCanvas.Opacity = 1.0;
                }
            }

            slideInStart.Completed += (s, ev) =>
            {
                _isNavigating = false;
                AppsScreenContainer.Visibility = Visibility.Hidden;
                AppsScreenTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                AppsScreenTranslate.Y = slideDistance;
                AppsScreenContainer.BeginAnimation(OpacityProperty, null);
                AppsScreenContainer.Opacity = 1.0;

                StartScreenTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                StartScreenTranslate.Y = 0;
                StartScreenContainer.BeginAnimation(OpacityProperty, null);
                StartScreenContainer.Opacity = 1.0;

                // Отключаем BitmapCache после завершения анимации
                StartScreenContainer.CacheMode = null;
                AppsScreenContainer.CacheMode = null;
                LiveTileCoordinator.Start();

                bool currentHasCustomWp = !string.IsNullOrEmpty(ThemeManager.CurrentTheme.CustomWallpaperPath) && File.Exists(ThemeManager.CurrentTheme.CustomWallpaperPath);
                if (RibbonContainer != null)
                {
                    RibbonContainer.BeginAnimation(OpacityProperty, null);
                    if (RibbonTranslate != null)
                    {
                        RibbonTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                        RibbonTranslate.Y = 0;
                    }
                    RibbonContainer.Opacity = currentHasCustomWp ? 0.0 : 1.0;
                    RibbonContainer.Visibility = currentHasCustomWp ? Visibility.Collapsed : Visibility.Visible;
                }
                if (RibbonCanvas != null)
                {
                    RibbonCanvas.BeginAnimation(OpacityProperty, null);
                    RibbonCanvas.Opacity = currentHasCustomWp ? 0.0 : 1.0;
                    RibbonCanvas.Visibility = currentHasCustomWp ? Visibility.Collapsed : Visibility.Visible;
                }
            };

            AppsScreenTranslate.BeginAnimation(TranslateTransform.YProperty, slideOutApps);
            AppsScreenContainer.BeginAnimation(OpacityProperty, fadeOutApps);

            StartScreenTranslate.BeginAnimation(TranslateTransform.YProperty, slideInStart);
            StartScreenContainer.BeginAnimation(OpacityProperty, fadeInStart);
        }

        // ======================== SEMANTIC ZOOM (ALL APPS) ========================

        private void LetterHeader_Click(object sender, RoutedEventArgs e) => SemanticZoomOut();
        private void TriggerSemanticZoomOut_Click(object sender, RoutedEventArgs e) => SemanticZoomOut();
        private void JumpGridBackdrop_MouseDown(object sender, MouseButtonEventArgs e) => SemanticZoomIn();

        public void SemanticZoomOut()
        {
            if (_isSemanticZoomedOut) return;
            _isSemanticZoomedOut = true;

            PopulateAlphabetJumpMatrix();
            ZoomedOutJumpGridLayer.Visibility = Visibility.Visible;

            var zoomOutDetail = new DoubleAnimation(1.0, 0.75, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var fadeOutDetail = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200));
            AppsZoomInScale.BeginAnimation(ScaleTransform.ScaleXProperty, zoomOutDetail);
            AppsZoomInScale.BeginAnimation(ScaleTransform.ScaleYProperty, zoomOutDetail);
            ZoomedInAppsLayer.BeginAnimation(OpacityProperty, fadeOutDetail);

            var zoomInMatrix = new DoubleAnimation(1.3, 1.0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var fadeInMatrix = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220));
            JumpGridScale.BeginAnimation(ScaleTransform.ScaleXProperty, zoomInMatrix);
            JumpGridScale.BeginAnimation(ScaleTransform.ScaleYProperty, zoomInMatrix);
            ZoomedOutJumpGridLayer.BeginAnimation(OpacityProperty, fadeInMatrix);
        }

        public void SemanticZoomIn(string? targetLetter = null)
        {
            if (!_isSemanticZoomedOut) return;
            _isSemanticZoomedOut = false;

            var fadeOutMatrix = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(180));
            fadeOutMatrix.Completed += (s, e) => ZoomedOutJumpGridLayer.Visibility = Visibility.Collapsed;
            ZoomedOutJumpGridLayer.BeginAnimation(OpacityProperty, fadeOutMatrix);

            var zoomInDetail = new DoubleAnimation(0.75, 1.0, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var fadeInDetail = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(240));
            AppsZoomInScale.BeginAnimation(ScaleTransform.ScaleXProperty, zoomInDetail);
            AppsZoomInScale.BeginAnimation(ScaleTransform.ScaleYProperty, zoomInDetail);
            ZoomedInAppsLayer.BeginAnimation(OpacityProperty, fadeInDetail);

            if (!string.IsNullOrEmpty(targetLetter))
            {
                int index = AlphabetGroups.TakeWhile(g => g.Letter != targetLetter).Count();
                if (index < AlphabetGroups.Count)
                {
                    AppsListScrollViewer.ScrollToHorizontalOffset(index * 245.0);
                }
            }
        }

        private void PopulateAlphabetJumpMatrix()
        {
            AlphabetJumpMatrixWrapPanel.Children.Clear();
            string[] alphabet = {
                "A","B","C","D","E","F","G","H","I","J","K","L","M","N","O","P","Q","R","S","T","U","V","W","X","Y","Z",
                "А","Б","В","Г","Д","Е","Ж","З","И","К","Л","М","Н","О","П","Р","С","Т","У","Ф","Х","Ц","Ч","Ш","Щ","Э","Ю","Я"
            };

            var activeLetters = new HashSet<string>(AlphabetGroups.Where(g => g.HasApps).Select(g => g.Letter));

            foreach (var letter in alphabet)
            {
                bool isActive = activeLetters.Contains(letter);
                var btn = new Button
                {
                    Width = 50,
                    Height = 50,
                    Margin = new Thickness(4),
                    Background = isActive ? (Brush)FindResource("ThemeAccentBrush") : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                    BorderThickness = new Thickness(0),
                    Cursor = isActive ? Cursors.Hand : Cursors.Arrow,
                    IsEnabled = isActive,
                    Content = new TextBlock
                    {
                        Text = letter,
                        FontSize = 20,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = isActive ? Brushes.White : new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };

                if (isActive)
                {
                    btn.Click += (s, e) => SemanticZoomIn(letter);
                }
                AlphabetJumpMatrixWrapPanel.Children.Add(btn);
            }
        }

        // ======================== ПЕРСОНАЛИЗАЦИЯ (SETTINGS FLYOUT) ========================

        public void OpenPersonalizeDirectly()
        {
            OpenScreenAnimated();
            OpenSettingsCharm();
            NavigateToSettingsPersonalize_Click(this, new RoutedEventArgs());
        }

        public void UpdateWallpaperTheme(Color primColor, Color secColor, Color accColor)
        {
            var lineCol = (Color)ColorConverter.ConvertFromString(string.IsNullOrEmpty(ThemeManager.CurrentTheme.LineColor) ? "#FF602FCE" : ThemeManager.CurrentTheme.LineColor);
            UpdateWallpaperTheme(primColor, secColor, accColor, lineCol);
        }

        public void UpdateWallpaperTheme(Color primColor, Color secColor, Color accColor, Color lineColor)
        {
            GradStop1.Color = primColor;
            GradStop2.Color = secColor;
            GradStop3.Color = primColor;
            Background = new SolidColorBrush(primColor);
            SettingsCharmFlyout.Background = new SolidColorBrush(Color.FromArgb(242, (byte)Math.Max(0, primColor.R - 10), (byte)Math.Max(0, primColor.G - 10), (byte)Math.Max(0, primColor.B - 10)));
            SearchCharmFlyout.Background = new SolidColorBrush(Color.FromArgb(255, 10, 10, 10));
            UserFlyout.Background = new SolidColorBrush(primColor);
            PowerFlyout.Background = new SolidColorBrush(primColor);

            UpdateRibbonColor(lineColor);
        }

        public void UpdateRibbonColor(Color color)
        {
            byte r = color.R;
            byte g = color.G;
            byte b = color.B;

            byte tr = (byte)Math.Min(255, (int)(r * 1.35 + 40));
            byte tg = (byte)Math.Min(255, (int)(g * 1.35 + 40));
            byte tb = (byte)Math.Min(255, (int)(b * 1.35 + 40));

            byte dr = (byte)(r * 0.70);
            byte dg = (byte)(g * 0.70);
            byte db = (byte)(b * 0.70);

            byte sr = (byte)(r * 0.45);
            byte sg = (byte)(g * 0.45);
            byte sb = (byte)(b * 0.45);

            // 0. Base Continuous Underlay
            if (BaseStop1 != null) BaseStop1.Color = Color.FromArgb(0xFF, dr, dg, db);
            if (BaseStop2 != null) BaseStop2.Color = Color.FromArgb(0xFF, r, g, b);
            if (BaseStop3 != null) BaseStop3.Color = Color.FromArgb(0xFF, dr, dg, db);
            if (BaseStop4 != null) BaseStop4.Color = Color.FromArgb(0xFF, sr, sg, sb);
            if (BaseStop5 != null) BaseStop5.Color = Color.FromArgb(0x00, sr, sg, sb);

            // Lower Ambient Veil
            if (VeilStop1 != null) VeilStop1.Color = Color.FromArgb(0x80, dr, dg, db);
            if (VeilStop2 != null) VeilStop2.Color = Color.FromArgb(0x50, sr, sg, sb);
            if (VeilStop3 != null) VeilStop3.Color = Color.FromArgb(0x00, sr, sg, sb);

            // 1. Band 1 (Left continuous drape)
            if (B1Stop1 != null) B1Stop1.Color = Color.FromArgb(0xFF, dr, dg, db);
            if (B1Stop2 != null) B1Stop2.Color = Color.FromArgb(0xFF, r, g, b);
            if (B1Stop3 != null) B1Stop3.Color = Color.FromArgb(0xFF, dr, dg, db);
            if (B1Stop4 != null) B1Stop4.Color = Color.FromArgb(0x00, sr, sg, sb);

            // 2. Upper Bands 2, 3, 4
            if (B2Stop1 != null) B2Stop1.Color = Color.FromArgb(0xFF, r, g, b);
            if (B2Stop2 != null) B2Stop2.Color = Color.FromArgb(0xFF, dr, dg, db);
            if (B2Stop3 != null) B2Stop3.Color = Color.FromArgb(0xFF, sr, sg, sb);

            if (B3Stop1 != null) B3Stop1.Color = Color.FromArgb(0xFF, tr, tg, tb);
            if (B3Stop2 != null) B3Stop2.Color = Color.FromArgb(0xFF, r, g, b);
            if (B3Stop3 != null) B3Stop3.Color = Color.FromArgb(0xFF, dr, dg, db);

            if (B4Stop1 != null) B4Stop1.Color = Color.FromArgb(0xFF, dr, dg, db);
            if (B4Stop2 != null) B4Stop2.Color = Color.FromArgb(0xFF, sr, sg, sb);
            if (B4Stop3 != null) B4Stop3.Color = Color.FromArgb(0x00, sr, sg, sb);

            // 3. Lower Cascading Petals
            if (LP2Stop1 != null) LP2Stop1.Color = Color.FromArgb(0x95, r, g, b);
            if (LP2Stop2 != null) LP2Stop2.Color = Color.FromArgb(0x95, dr, dg, db);
            if (LP2Stop3 != null) LP2Stop3.Color = Color.FromArgb(0x50, sr, sg, sb);
            if (LP2Stop4 != null) LP2Stop4.Color = Color.FromArgb(0x00, sr, sg, sb);

            if (LP1Stop1 != null) LP1Stop1.Color = Color.FromArgb(0xE5, tr, tg, tb);
            if (LP1Stop2 != null) LP1Stop2.Color = Color.FromArgb(0xC5, r, g, b);
            if (LP1Stop3 != null) LP1Stop3.Color = Color.FromArgb(0x80, dr, dg, db);
            if (LP1Stop4 != null) LP1Stop4.Color = Color.FromArgb(0x00, sr, sg, sb);

            // 4. Crisp Luminous Fold Highlight Stroke
            if (FoldStop1 != null) FoldStop1.Color = Color.FromArgb(0x00, 255, 255, 255);
            if (FoldStop2 != null) FoldStop2.Color = Color.FromArgb(0xC0, tr, tg, tb);
            if (FoldStop3 != null) FoldStop3.Color = Color.FromArgb(0xFF, 255, 255, 255);
            if (FoldStop4 != null) FoldStop4.Color = Color.FromArgb(0xF0, tr, tg, tb);
            if (FoldStop5 != null) FoldStop5.Color = Color.FromArgb(0x00, 255, 255, 255);
        }

        private void RootLayout_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_activeView != ActiveView.Start) return;

            if (StartTiles.Any(t => t.IsSelected))
            {
                DeselectAllTiles();
                e.Handled = true;
                return;
            }

            ToggleGroupNamingMode();
            e.Handled = true;
        }

        private void SettingsCharmToggle_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyouts();
            if (_isSettingsCharmOpen) CloseSettingsCharm();
            else OpenSettingsCharm();
        }

        public void OpenSettingsCharm()
        {
            if (_isSettingsCharmOpen) return;
            _isSettingsCharmOpen = true;

            CloseSearchCharm();
            CloseAllFlyouts();
            NavigateToSettingsMain();

            var anim = new DoubleAnimation(0, -360, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            SettingsCharmTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
        }

        public void CloseSettingsCharm()
        {
            if (!_isSettingsCharmOpen) return;
            _isSettingsCharmOpen = false;

            var anim = new DoubleAnimation(-360, 0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            SettingsCharmTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
        }

        private void NavigateToSettingsMain()
        {
            SettingsMainView.Visibility = Visibility.Visible;
            SettingsPersonalizeView.Visibility = Visibility.Collapsed;
            SettingsAddTileView.Visibility = Visibility.Collapsed;
            if (SettingsStudioView != null) SettingsStudioView.Visibility = Visibility.Collapsed;
            if (SettingsMonitorsView != null) SettingsMonitorsView.Visibility = Visibility.Collapsed;
            if (SettingsLanguageView != null) SettingsLanguageView.Visibility = Visibility.Collapsed;
            if (SettingsFoundersView != null) SettingsFoundersView.Visibility = Visibility.Collapsed;
            if (SettingsAutostartView != null) SettingsAutostartView.Visibility = Visibility.Collapsed;
        }

        private void NavigateToSettingsMain_Click(object sender, RoutedEventArgs e) => NavigateToSettingsMain();

        private void NavigateToSettingsPersonalize_Click(object sender, RoutedEventArgs e)
        {
            SettingsMainView.Visibility = Visibility.Collapsed;
            SettingsPersonalizeView.Visibility = Visibility.Visible;
            SettingsAddTileView.Visibility = Visibility.Collapsed;
            if (SettingsStudioView != null) SettingsStudioView.Visibility = Visibility.Collapsed;
            if (SettingsMonitorsView != null) SettingsMonitorsView.Visibility = Visibility.Collapsed;
            if (SettingsLanguageView != null) SettingsLanguageView.Visibility = Visibility.Collapsed;
            if (SettingsFoundersView != null) SettingsFoundersView.Visibility = Visibility.Collapsed;
            if (SettingsAutostartView != null) SettingsAutostartView.Visibility = Visibility.Collapsed;
        }

        private void NavigateToSettingsAddTile_Click(object sender, RoutedEventArgs e)
        {
            SettingsMainView.Visibility = Visibility.Collapsed;
            SettingsPersonalizeView.Visibility = Visibility.Collapsed;
            SettingsAddTileView.Visibility = Visibility.Visible;
            if (SettingsStudioView != null) SettingsStudioView.Visibility = Visibility.Collapsed;
            if (SettingsMonitorsView != null) SettingsMonitorsView.Visibility = Visibility.Collapsed;
            if (SettingsLanguageView != null) SettingsLanguageView.Visibility = Visibility.Collapsed;
            if (SettingsFoundersView != null) SettingsFoundersView.Visibility = Visibility.Collapsed;
            if (SettingsAutostartView != null) SettingsAutostartView.Visibility = Visibility.Collapsed;
            InitAddTileView();
        }

        public void NavigateToSettingsStudio(TileModel? targetTile = null)
        {
            SettingsMainView.Visibility = Visibility.Collapsed;
            SettingsPersonalizeView.Visibility = Visibility.Collapsed;
            SettingsAddTileView.Visibility = Visibility.Collapsed;
            if (SettingsStudioView != null) SettingsStudioView.Visibility = Visibility.Visible;
            if (SettingsMonitorsView != null) SettingsMonitorsView.Visibility = Visibility.Collapsed;
            if (SettingsLanguageView != null) SettingsLanguageView.Visibility = Visibility.Collapsed;
            if (SettingsFoundersView != null) SettingsFoundersView.Visibility = Visibility.Collapsed;
            if (SettingsAutostartView != null) SettingsAutostartView.Visibility = Visibility.Collapsed;
            InitStudioView(targetTile);
        }

        private void NavigateToSettingsMonitors_Click(object sender, RoutedEventArgs e)
        {
            SettingsMainView.Visibility = Visibility.Collapsed;
            SettingsPersonalizeView.Visibility = Visibility.Collapsed;
            SettingsAddTileView.Visibility = Visibility.Collapsed;
            if (SettingsStudioView != null) SettingsStudioView.Visibility = Visibility.Collapsed;
            if (SettingsLanguageView != null) SettingsLanguageView.Visibility = Visibility.Collapsed;
            if (SettingsFoundersView != null) SettingsFoundersView.Visibility = Visibility.Collapsed;
            if (SettingsAutostartView != null) SettingsAutostartView.Visibility = Visibility.Collapsed;
            if (SettingsMonitorsView != null) SettingsMonitorsView.Visibility = Visibility.Visible;
            InitMonitorsView();
        }

        private void NavigateToSettingsLanguage_Click(object sender, RoutedEventArgs e)
        {
            SettingsMainView.Visibility = Visibility.Collapsed;
            SettingsPersonalizeView.Visibility = Visibility.Collapsed;
            SettingsAddTileView.Visibility = Visibility.Collapsed;
            if (SettingsStudioView != null) SettingsStudioView.Visibility = Visibility.Collapsed;
            if (SettingsMonitorsView != null) SettingsMonitorsView.Visibility = Visibility.Collapsed;
            if (SettingsFoundersView != null) SettingsFoundersView.Visibility = Visibility.Collapsed;
            if (SettingsAutostartView != null) SettingsAutostartView.Visibility = Visibility.Collapsed;
            if (SettingsLanguageView != null) SettingsLanguageView.Visibility = Visibility.Visible;
            InitLanguageView();
        }

        private void NavigateToSettingsAutostart_Click(object sender, RoutedEventArgs e)
        {
            SettingsMainView.Visibility = Visibility.Collapsed;
            SettingsPersonalizeView.Visibility = Visibility.Collapsed;
            SettingsAddTileView.Visibility = Visibility.Collapsed;
            if (SettingsStudioView != null) SettingsStudioView.Visibility = Visibility.Collapsed;
            if (SettingsMonitorsView != null) SettingsMonitorsView.Visibility = Visibility.Collapsed;
            if (SettingsLanguageView != null) SettingsLanguageView.Visibility = Visibility.Collapsed;
            if (SettingsFoundersView != null) SettingsFoundersView.Visibility = Visibility.Collapsed;
            if (SettingsAutostartView != null) SettingsAutostartView.Visibility = Visibility.Visible;
            InitAutostartView();
        }

        public void InitAutostartView()
        {
            bool isAutostart = AutostartHelper.IsAutostartEnabled();
            if (AutostartCheckMark != null)
                AutostartCheckMark.Visibility = isAutostart ? Visibility.Visible : Visibility.Collapsed;
            if (AutostartStatusText != null)
            {
                AutostartStatusText.Text = isAutostart ? LocalizationManager.Get("AutostartStatusEnabled", "Включено") : LocalizationManager.Get("AutostartStatusDisabled", "Отключено");
                AutostartStatusText.Foreground = isAutostart ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 255, 128)) : new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200));
            }

            bool bootToStart = ThemeManager.CurrentTheme.LaunchStartScreenOnBoot;
            if (BootToStartCheckMark != null)
                BootToStartCheckMark.Visibility = bootToStart ? Visibility.Visible : Visibility.Collapsed;
            if (BootToStartStatusText != null)
            {
                BootToStartStatusText.Text = bootToStart ? LocalizationManager.Get("AutostartStatusEnabled", "Включено") : LocalizationManager.Get("AutostartStatusDisabled", "Отключено");
                BootToStartStatusText.Foreground = bootToStart ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 255, 128)) : new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200));
            }

            string progExe = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WinMosaic", "WinMosaic.exe");
            if (AutostartExePathText != null)
            {
                AutostartExePathText.Text = $"Файл: {progExe}";
            }
        }

        private void ToggleAutostart_Click(object sender, RoutedEventArgs e)
        {
            bool current = AutostartHelper.IsAutostartEnabled();
            AutostartHelper.SetAutostart(!current);
            InitAutostartView();
        }

        private void ToggleBootToStart_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.CurrentTheme.LaunchStartScreenOnBoot = !ThemeManager.CurrentTheme.LaunchStartScreenOnBoot;
            ThemeManager.SaveTheme();
            InitAutostartView();
        }

        private void NavigateToSettingsFounders_Click(object sender, RoutedEventArgs e)
        {
            SettingsMainView.Visibility = Visibility.Collapsed;
            SettingsPersonalizeView.Visibility = Visibility.Collapsed;
            SettingsAddTileView.Visibility = Visibility.Collapsed;
            if (SettingsStudioView != null) SettingsStudioView.Visibility = Visibility.Collapsed;
            if (SettingsMonitorsView != null) SettingsMonitorsView.Visibility = Visibility.Collapsed;
            if (SettingsLanguageView != null) SettingsLanguageView.Visibility = Visibility.Collapsed;
            if (SettingsAutostartView != null) SettingsAutostartView.Visibility = Visibility.Collapsed;
            if (SettingsFoundersView != null) SettingsFoundersView.Visibility = Visibility.Visible;
        }

        // =========================================================================
        // ЭКРАНЫ И МОНИТОРЫ (DISPLAYS & MONITORS)
        // =========================================================================
        public void InitMonitorsView()
        {
            try
            {
                var screens = System.Windows.Forms.Screen.AllScreens;
                var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen ?? screens[0];
                var secondaryScreen = screens.FirstOrDefault(s => !s.Primary);

                if (PrimaryMonitorDetailsText != null)
                {
                    PrimaryMonitorDetailsText.Text = $"{primaryScreen.Bounds.Width} × {primaryScreen.Bounds.Height} ({primaryScreen.DeviceName.Replace(@"\\.\", "")})";
                }

                if (secondaryScreen != null)
                {
                    if (SecondaryMonitorButton != null) SecondaryMonitorButton.IsEnabled = true;
                    if (SecondaryMonitorCard != null) SecondaryMonitorCard.Opacity = 1.0;
                    if (SecondaryMonitorDetailsText != null)
                    {
                        SecondaryMonitorDetailsText.Text = $"{secondaryScreen.Bounds.Width} × {secondaryScreen.Bounds.Height} ({secondaryScreen.DeviceName.Replace(@"\\.\", "")})";
                    }
                    if (NoSecondMonitorNotice != null) NoSecondMonitorNotice.Visibility = Visibility.Collapsed;
                }
                else
                {
                    if (SecondaryMonitorButton != null) SecondaryMonitorButton.IsEnabled = false;
                    if (SecondaryMonitorCard != null) SecondaryMonitorCard.Opacity = 0.45;
                    if (SecondaryMonitorDetailsText != null)
                    {
                        SecondaryMonitorDetailsText.Text = LocalizationManager.Get("MonitorsNoSecond", "Не обнаружен");
                    }
                    if (NoSecondMonitorNotice != null) NoSecondMonitorNotice.Visibility = Visibility.Visible;
                }

                bool isSecondary = TargetMonitor.Equals("Secondary", StringComparison.OrdinalIgnoreCase) && secondaryScreen != null;
                if (PrimaryMonitorCheckMark != null) PrimaryMonitorCheckMark.Visibility = !isSecondary ? Visibility.Visible : Visibility.Collapsed;
                if (SecondaryMonitorCheckMark != null) SecondaryMonitorCheckMark.Visibility = isSecondary ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InitMonitorsView] Error: {ex.Message}");
            }
        }

        private void SelectPrimaryMonitor_Click(object sender, RoutedEventArgs e)
        {
            TargetMonitor = "Primary";
            ApplyMonitorConfiguration();
            SaveLayoutConfig();
            InitMonitorsView();
        }

        private void SelectSecondaryMonitor_Click(object sender, RoutedEventArgs e)
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            if (screens.Length < 2)
            {
                TargetMonitor = "Primary";
                ApplyMonitorConfiguration();
                SaveLayoutConfig();
                InitMonitorsView();
                return;
            }

            TargetMonitor = "Secondary";
            ApplyMonitorConfiguration();
            SaveLayoutConfig();
            InitMonitorsView();
        }

        private void RefreshMonitors_Click(object sender, RoutedEventArgs e)
        {
            InitMonitorsView();
            ApplyMonitorConfiguration();
        }

        // =========================================================================
        // ЛОКАЛИЗАЦИЯ И ЯЗЫКИ (LOCALIZATION & LANGUAGES)
        // =========================================================================
        public void InitLanguageView()
        {
            bool isRu = LocalizationManager.CurrentLanguage == "ru";
            if (LangRuCheckMark != null) LangRuCheckMark.Visibility = isRu ? Visibility.Visible : Visibility.Collapsed;
            if (LangEnCheckMark != null) LangEnCheckMark.Visibility = !isRu ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SelectLangRu_Click(object sender, RoutedEventArgs e)
        {
            LocalizationManager.SetLanguage("ru");
            UpdateUiLanguage();
            SaveLayoutConfig();
            InitLanguageView();
        }

        private void SelectLangEn_Click(object sender, RoutedEventArgs e)
        {
            LocalizationManager.SetLanguage("en");
            UpdateUiLanguage();
            SaveLayoutConfig();
            InitLanguageView();
        }

        public void UpdateUiLanguage()
        {
            try
            {
                // Заголовок главного экрана Пуск
                if (StartTitleTextBlock != null) StartTitleTextBlock.Text = LocalizationManager.Get("StartTitle", "Пуск");

                // Меню пользователя
                if (UserPersonalizeBtn != null) UserPersonalizeBtn.Content = LocalizationManager.Get("UserPersonalize", "Персонализация");
                if (UserChangeAvatarBtn != null) UserChangeAvatarBtn.Content = LocalizationManager.Get("UserChangeAvatar", "Сменить аватар");
                if (UserLockBtn != null) UserLockBtn.Content = LocalizationManager.Get("UserLock", "Заблокировать");
                if (UserSignOutBtn != null) UserSignOutBtn.Content = LocalizationManager.Get("UserSignOut", "Выйти");

                // Меню питания
                if (PowerSleepBtn != null) PowerSleepBtn.Content = LocalizationManager.Get("PowerSleep", "Спящий режим");
                if (PowerShutdownBtn != null) PowerShutdownBtn.Content = LocalizationManager.Get("PowerShutdown", "Завершение работы");
                if (PowerRestartBtn != null) PowerRestartBtn.Content = LocalizationManager.Get("PowerRestart", "Перезагрузка");

                // Панель параметров (Settings Charm)
                if (SettingsTitleText != null) SettingsTitleText.Text = LocalizationManager.Get("SettingsTitle", "Параметры");
                if (SettingsSubtitleText != null) SettingsSubtitleText.Text = LocalizationManager.Get("SettingsSubtitle", "Начальный экран");
                if (SettingsPersonalizeBtnText != null) SettingsPersonalizeBtnText.Text = LocalizationManager.Get("SettingsPersonalize", "Персонализация");
                if (SettingsAddTileBtnText != null) SettingsAddTileBtnText.Text = LocalizationManager.Get("SettingsAddTile", "Добавить свою плитку");
                if (SettingsMonitorsBtnText != null) SettingsMonitorsBtnText.Text = LocalizationManager.Get("SettingsMonitors", "Экраны и мониторы");
                if (SettingsLanguageBtnText != null) SettingsLanguageBtnText.Text = LocalizationManager.Get("SettingsLanguage", "Язык интерфейса");
                if (SettingsAutostartBtnText != null) SettingsAutostartBtnText.Text = LocalizationManager.Get("SettingsAutostart", "Автозагрузка");
                if (SettingsFoundersBtnText != null) SettingsFoundersBtnText.Text = LocalizationManager.Get("SettingsFounders", "Основатели");
                if (SettingsResetTilesBtnText != null) SettingsResetTilesBtnText.Text = LocalizationManager.Get("SettingsResetTiles", "Восстановить плитки");
                if (SettingsControlPanelBtnText != null) SettingsControlPanelBtnText.Text = LocalizationManager.Get("SettingsControlPanel", "Панель управления");
                if (SettingsTaskManagerBtnText != null) SettingsTaskManagerBtnText.Text = LocalizationManager.Get("SettingsTaskManager", "Диспетчер задач");
                if (SettingsStudioBtnText != null) SettingsStudioBtnText.Text = LocalizationManager.Get("SettingsStudio", "Редактор экрана (Studio)");
                if (SettingsPCSettingsBtnText != null) SettingsPCSettingsBtnText.Text = LocalizationManager.Get("SettingsPCSettings", "Изменение параметров компьютера");

                // Подменю Экраны
                if (SettingsMonitorsTitleText != null) SettingsMonitorsTitleText.Text = LocalizationManager.Get("MonitorsTitle", "Экраны");
                if (SettingsMonitorsSubtitleText != null) SettingsMonitorsSubtitleText.Text = LocalizationManager.Get("MonitorsSubtitle", "Выберите монитор для начального экрана:");
                if (PrimaryMonitorTitleText != null) PrimaryMonitorTitleText.Text = LocalizationManager.Get("MonitorsPrimary", "Основной монитор");
                if (SecondaryMonitorTitleText != null) SecondaryMonitorTitleText.Text = LocalizationManager.Get("MonitorsSecondary", "Второй монитор");
                if (NoSecondMonitorText != null) NoSecondMonitorText.Text = LocalizationManager.Get("MonitorsNoSecond", "Второй монитор не обнаружен. Подключите дополнительный дисплей для отображения начального экрана на нём.");

                // Подменю Язык
                if (SettingsLanguageTitleText != null) SettingsLanguageTitleText.Text = LocalizationManager.Get("LanguageTitle", "Язык");
                if (SettingsLanguageSubtitleText != null) SettingsLanguageSubtitleText.Text = LocalizationManager.Get("LanguageSubtitle", "Выберите язык интерфейса:");

                // Подменю Автозагрузка
                if (SettingsAutostartTitleText != null) SettingsAutostartTitleText.Text = LocalizationManager.Get("AutostartTitle", "Автозагрузка");
                if (SettingsAutostartSubtitleText != null) SettingsAutostartSubtitleText.Text = LocalizationManager.Get("AutostartSubtitle", "Параметры запуска WinMosaic вместе с Windows");
                if (AutostartWindowsHeader != null) AutostartWindowsHeader.Text = LocalizationManager.Get("AutostartWindowsHeader", "АВТОЗАПУСК С WINDOWS");
                if (AutostartWindowsDesc != null) AutostartWindowsDesc.Text = LocalizationManager.Get("AutostartWindowsDesc", "Запускать оболочку начального экрана автоматически при входе в систему.");
                if (AutostartBootToStartHeader != null) AutostartBootToStartHeader.Text = LocalizationManager.Get("AutostartBootToStartHeader", "ОТКРЫВАТЬ «ПУСК» ПРИ ВКЛЮЧЕНИИ ПК");
                if (AutostartBootToStartDesc != null) AutostartBootToStartDesc.Text = LocalizationManager.Get("AutostartBootToStartDesc", "Сразу открывать начальный экран при загрузке системы (как в оригинальной Windows 8.1).");
                InitAutostartView();

                // Подменю Основатели (Титры)
                if (SettingsFoundersTitleText != null) SettingsFoundersTitleText.Text = LocalizationManager.Get("FoundersTitle", "Основатели");
                if (SettingsFoundersLeadRoleText != null) SettingsFoundersLeadRoleText.Text = LocalizationManager.Get("FoundersAuthorHeader", "СОЗДАТЕЛЬ ПРОЕКТА");
                if (SettingsFoundersLeadBioText != null) SettingsFoundersLeadBioText.Text = LocalizationManager.Get("FoundersAuthorSubtext", "Создал в одиночку by ПЕТЯ");
                if (SettingsFoundersStudioRoleText != null) SettingsFoundersStudioRoleText.Text = LocalizationManager.Get("FoundersStudioHeader", "СТУДИЯ РАЗРАБОТКИ");
                if (SettingsFoundersStudioBioText != null) SettingsFoundersStudioBioText.Text = LocalizationManager.Get("FoundersStudioSubtext", "Создано студией ChaosMaster - Studio®");
                if (SettingsFoundersAboutHeader != null) SettingsFoundersAboutHeader.Text = LocalizationManager.Get("FoundersAboutHeader", "О ПРОЕКТЕ");
                if (SettingsFoundersTaglineText != null) SettingsFoundersTaglineText.Text = LocalizationManager.Get("FoundersTagline", "Всё, что создано в этом проекте — создано студией ChaosMaster - Studio® и создал в одиночку by ПЕТЯ.");
                if (SettingsFoundersDescText != null) SettingsFoundersDescText.Text = LocalizationManager.Get("FoundersDesc", "WinMosaic — воссоздание легендарного начального экрана Windows 8.1 для Windows 10 и Windows 11 со всеми эффектами, живыми плитками и поддержкой нескольких мониторов.");
                if (SettingsFoundersCopyrightText != null) SettingsFoundersCopyrightText.Text = LocalizationManager.Get("FoundersCopyright", "© 2026 by ПЕТЯ & ChaosMaster - Studio®");

                // Экран приложений
                if (AppsTitleTextBlock != null) AppsTitleTextBlock.Text = LocalizationManager.Get("AppsTitle", "Приложения");
                if (SortByNameItem != null) SortByNameItem.Header = LocalizationManager.Get("SortByName", "по имени");
                if (SortByDateItem != null) SortByDateItem.Header = LocalizationManager.Get("SortByDate", "по дате установки");
                if (SortByUsageItem != null) SortByUsageItem.Header = LocalizationManager.Get("SortByUsage", "по частоте");
                if (SortByCategoryItem != null) SortByCategoryItem.Header = LocalizationManager.Get("SortByCategory", "по категории");

                if (CurrentSortTextBlock != null)
                {
                    if (CurrentSortTextBlock.Text.Contains("имени") || CurrentSortTextBlock.Text.Contains("name"))
                        CurrentSortTextBlock.Text = LocalizationManager.Get("SortByName", "по имени");
                }

                // Динамическое обновление живого текста и заголовков системных плиток
                foreach (var tile in StartTiles)
                {
                    string title = tile.Title;
                    if (tile.IsSportsTile)
                    {
                        tile.LiveHeadline = LocalizationManager.Get("LiveSportsHeadline", "Роман Широков: Быть");
                        tile.LiveSubheadline = LocalizationManager.Get("LiveSportsSubheadline", "капитаном — это...");
                    }
                    else if (tile.IsNewsTile)
                    {
                        tile.LiveHeadline = LocalizationManager.Get("LiveNewsHeadline", "Последняя молитва");
                    }
                    else if (tile.IsWeatherTemplate)
                    {
                        tile.LiveCity = LocalizationManager.Get("LiveWeatherCity", "Москва");
                        tile.LiveCondition = LocalizationManager.Get("LiveWeatherCondition", "Преимущественно солнечно");
                        tile.LiveText = LocalizationManager.Get("LiveWeatherText", "Москва, Ясно\n+21°C | Влажность 45%");
                    }
                    else if (title.Equals("Calendar", StringComparison.OrdinalIgnoreCase) || title.Equals("Календарь", StringComparison.OrdinalIgnoreCase))
                    {
                        tile.LiveText = LocalizationManager.Get("LiveCalendarText", "14:00 - Встреча команды\n18:30 - Тренировка");
                    }
                    else if (tile.IsStoreTemplate)
                    {
                        tile.StoreHeadline = LocalizationManager.Get("LiveStoreHeadline", "Войдите в Лабораторию...");
                        tile.StoreAppName = LocalizationManager.Get("LiveStoreAppName", "Гадкий Я: Minion Rush");
                        tile.StoreRatingPrice = LocalizationManager.Get("LiveStoreRatingPrice", "Бесплатно ★★★★★ 12 369");
                        tile.StoreTopApp1Sub = LocalizationManager.Get("LiveStoreTop1Sub", "Бесплатно ★★★★★ 18 420");
                        tile.StoreTopApp2Name = LocalizationManager.Get("LiveStoreTop2Name", "Asphalt 8: На взлёт");
                        tile.StoreTopApp2Sub = LocalizationManager.Get("LiveStoreTop2Sub", "Бесплатно ★★★★★ 95 430");
                        tile.StoreTopApp3Sub = LocalizationManager.Get("LiveStoreTop3Sub", "Бесплатно ★★★★★ 27 890");
                    }
                    else if (title.Equals("Food & Drink", StringComparison.OrdinalIgnoreCase) || title.Equals("Кулинария", StringComparison.OrdinalIgnoreCase))
                    {
                        tile.LiveHeadline = LocalizationManager.Get("LiveFoodHeadline", "Паста с брокколи");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateUiLanguage] Error: {ex.Message}");
            }
        }

        public void OpenStudioForTile(TileModel tile)
        {
            OpenSettingsCharm();
            NavigateToSettingsStudio(tile);
        }

        private void OpenPersonalizeFromUser_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyouts();
            OpenPersonalizeDirectly();
        }

        private void PersonalizeToggle_Click(object sender, RoutedEventArgs e) => SettingsCharmToggle_Click(sender, e);
        private void OpenPersonalize() => OpenPersonalizeDirectly();
        private void ClosePersonalize_Click(object sender, RoutedEventArgs e) => CloseSettingsCharm();
        private void ClosePersonalize() => CloseSettingsCharm();

        // =========================================================================
        // ДОБАВИТЬ СВОЮ ПЛИТКУ (ADD CUSTOM TILE)
        // =========================================================================
        private void InitAddTileView()
        {
            if (!_addTileInitialized)
            {
                _addTileInitialized = true;
                InitAddTilePalette();
            }
            UpdateTilePreview();
        }

        private void InitAddTilePalette()
        {
            string[] metroColors = new[]
            {
                "#FF0078D7", "#FF004E8C", "#FF008A00", "#FF107C41",
                "#FF68217A", "#FFD80073", "#FFD24726", "#FFFF4500",
                "#FFFF8C00", "#FF008299", "#FF00A4EF", "#FF2D2D30"
            };

            AddTileColorsWrapPanel.Children.Clear();
            foreach (var colorHex in metroColors)
            {
                var btn = new Border
                {
                    Width = 32,
                    Height = 32,
                    Margin = new Thickness(0, 0, 8, 8),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                    Cursor = Cursors.Hand,
                    BorderThickness = colorHex == _addTileSelectedColor ? new Thickness(2) : new Thickness(1),
                    BorderBrush = colorHex == _addTileSelectedColor ? Brushes.White : new SolidColorBrush(Color.FromArgb(80, 255, 255, 255))
                };

                string hexCapture = colorHex;
                btn.MouseLeftButtonUp += (s, e) =>
                {
                    _addTileSelectedColor = hexCapture;
                    _isUpdatingColorHex = true;
                    if (AddTileColorHexInput != null) AddTileColorHexInput.Text = hexCapture;
                    _isUpdatingColorHex = false;

                    foreach (Border child in AddTileColorsWrapPanel.Children)
                    {
                        bool isSel = (child == btn);
                        child.BorderThickness = isSel ? new Thickness(2) : new Thickness(1);
                        child.BorderBrush = isSel ? Brushes.White : new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
                    }
                    UpdateTilePreview();
                };
                AddTileColorsWrapPanel.Children.Add(btn);
            }
        }

        private void AddTileColorHexInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingColorHex) return;
            string text = AddTileColorHexInput.Text?.Trim() ?? "";
            if (!text.StartsWith("#")) text = "#" + text;
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(text);
                _addTileSelectedColor = text;
                if (AddTileColorSwatch != null)
                {
                    AddTileColorSwatch.Background = new SolidColorBrush(color);
                }
                UpdateTilePreview();
            }
            catch { }
        }

        // ======================== ИНСТРУМЕНТ «ПИПЕТКА» (EYEDROPPER) ========================

        private void Eyedropper_Click(object sender, RoutedEventArgs e)
        {
            if (_isEyedropperActive)
            {
                StopEyedropper(applyColor: false);
            }
            else
            {
                StartEyedropper();
            }
        }

        private void StartEyedropper()
        {
            _isEyedropperActive = true;
            if (EyedropperHud != null) EyedropperHud.Visibility = Visibility.Visible;
            Mouse.OverrideCursor = Cursors.Cross;

            if (_eyedropperTimer == null)
            {
                _eyedropperTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(16)
                };
                _eyedropperTimer.Tick += EyedropperTimer_Tick;
            }
            _eyedropperTimer.Start();
        }

        private void StopEyedropper(bool applyColor = true)
        {
            if (!_isEyedropperActive) return;

            _isEyedropperActive = false;
            _eyedropperTimer?.Stop();
            if (EyedropperHud != null) EyedropperHud.Visibility = Visibility.Collapsed;
            Mouse.OverrideCursor = null;

            if (applyColor)
            {
                string hex = $"#{_eyedropperSampledColor.A:X2}{_eyedropperSampledColor.R:X2}{_eyedropperSampledColor.G:X2}{_eyedropperSampledColor.B:X2}";
                if (_isStudioEyedropper)
                {
                    _isStudioEyedropper = false;
                    if (_studioSelectedTile != null)
                    {
                        _studioSelectedTile.BackgroundColor = hex;
                        SaveLayoutConfig();
                        UpdateStudioSelectedTileUI();
                    }
                }
                else
                {
                    _addTileSelectedColor = hex;
                    _isUpdatingColorHex = true;
                    if (AddTileColorHexInput != null) AddTileColorHexInput.Text = hex;
                    _isUpdatingColorHex = false;
                    UpdateTilePreview();
                }
            }
            else
            {
                _isStudioEyedropper = false;
            }
        }

        private void EyedropperTimer_Tick(object? sender, EventArgs e)
        {
            if (GetCursorPos(out POINT pt))
            {
                _eyedropperSampledColor = SamplePixelColor(pt.X, pt.Y);
                string hex = $"#{_eyedropperSampledColor.A:X2}{_eyedropperSampledColor.R:X2}{_eyedropperSampledColor.G:X2}{_eyedropperSampledColor.B:X2}";

                if (EyedropperSwatch != null) EyedropperSwatch.Background = new SolidColorBrush(_eyedropperSampledColor);
                if (EyedropperHexText != null) EyedropperHexText.Text = hex;
                if (EyedropperRgbText != null) EyedropperRgbText.Text = $" (RGB: {_eyedropperSampledColor.R}, {_eyedropperSampledColor.G}, {_eyedropperSampledColor.B})";

                if ((GetAsyncKeyState(0x1B) & 0x8000) != 0) // VK_ESCAPE
                {
                    StopEyedropper(applyColor: false);
                    return;
                }

                if ((GetAsyncKeyState(0x01) & 0x8000) != 0) // VK_LBUTTON pressed
                {
                    StopEyedropper(applyColor: true);
                    return;
                }
            }
        }

        private static Color SamplePixelColor(int screenX, int screenY)
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            if (hdc != IntPtr.Zero)
            {
                try
                {
                    uint pixel = GetPixel(hdc, screenX, screenY);
                    byte r = (byte)(pixel & 0x000000FF);
                    byte g = (byte)((pixel & 0x0000FF00) >> 8);
                    byte b = (byte)((pixel & 0x00FF0000) >> 16);
                    return Color.FromRgb(r, g, b);
                }
                finally
                {
                    ReleaseDC(IntPtr.Zero, hdc);
                }
            }
            return Color.FromRgb(0, 120, 215);
        }

        // ======================== КАТАЛОГ И МАСШТАБ ЗНАЧКОВ ========================

        private void OpenMetroIconCatalog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Win8StartScreen.Views.MetroIconPickerDialog
                {
                    Owner = this
                };

                if (dlg.ShowDialog() == true)
                {
                    if (!string.IsNullOrEmpty(dlg.SelectedIconPath))
                    {
                        _addTileCustomIconPath = dlg.SelectedIconPath;
                        if (dlg.ChosenIconSize > 0)
                        {
                            _addTileIconSize = dlg.ChosenIconSize;
                            if (AddTileIconSizeSlider != null)
                            {
                                AddTileIconSizeSlider.Value = _addTileIconSize;
                            }
                        }
                        UpdateTilePreview();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenMetroIconCatalog] Error: {ex.Message}");
            }
        }

        private void AddTileIconSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _addTileIconSize = e.NewValue;
            if (AddTileIconSizeText != null)
            {
                AddTileIconSizeText.Text = $"{e.NewValue:F0} px ({(e.NewValue / 64.0) * 100:F0}%)";
            }
            UpdateTilePreview();
        }

        private void AddTileIconPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                if (tag == "MAX")
                {
                    _addTileIconStretch = "UniformToFill";
                    _addTileIconSize = _addTileSelectedSize switch
                    {
                        TileSize.Small => 71,
                        TileSize.Wide => 240,
                        TileSize.Large => 240,
                        _ => 150
                    };
                    if (AddTileIconSizeSlider != null) AddTileIconSizeSlider.Value = Math.Min(300, _addTileIconSize);
                }
                else if (tag == "RESET")
                {
                    _addTileIconStretch = "Uniform";
                    _addTileIconSize = 64.0;
                    if (AddTileIconSizeSlider != null) AddTileIconSizeSlider.Value = 64;
                }
                else if (double.TryParse(tag, out double size))
                {
                    _addTileIconStretch = "Uniform";
                    _addTileIconSize = size;
                    if (AddTileIconSizeSlider != null) AddTileIconSizeSlider.Value = size;
                }
                UpdateTilePreview();
            }
        }

        private static bool IsWebUrl(string input, out string normalizedUrl, out string domainOrName)
        {
            normalizedUrl = string.Empty;
            domainOrName = string.Empty;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string trimmed = input.Trim();

            // Already has http/https protocol
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                normalizedUrl = trimmed;
                try
                {
                    var uri = new Uri(trimmed);
                    string host = uri.Host;
                    if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                        host = host.Substring(4);
                    domainOrName = host;
                    return true;
                }
                catch
                {
                    return true;
                }
            }

            // Starts with www.
            if (trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                normalizedUrl = "https://" + trimmed;
                domainOrName = trimmed.Substring(4);
                int slashIdx = domainOrName.IndexOf('/');
                if (slashIdx >= 0) domainOrName = domainOrName.Substring(0, slashIdx);
                return true;
            }

            // Check if looks like a domain name with known TLD
            string[] tlds = { ".com", ".ru", ".org", ".net", ".io", ".app", ".tv", ".me", ".info", ".co", ".dev", ".gg", ".to", ".ai" };
            foreach (var tld in tlds)
            {
                if (trimmed.IndexOf(tld, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    normalizedUrl = "https://" + trimmed;
                    string host = trimmed;
                    int slashIdx = host.IndexOf('/');
                    if (slashIdx >= 0) host = host.Substring(0, slashIdx);
                    if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                        host = host.Substring(4);
                    domainOrName = host;
                    return true;
                }
            }

            return false;
        }

        private static string PrettyDomainTitle(string domainOrName)
        {
            if (string.IsNullOrWhiteSpace(domainOrName)) return string.Empty;

            string clean = domainOrName.Trim();
            int dotIdx = clean.IndexOf('.');
            string baseName = (dotIdx > 0) ? clean.Substring(0, dotIdx) : clean;

            return baseName.ToLowerInvariant() switch
            {
                "youtube" => "YouTube",
                "google" => "Google",
                "vk" => "ВКонтакте",
                "github" => "GitHub",
                "twitter" => "Twitter",
                "twitch" => "Twitch",
                "yandex" or "ya" => "Яндекс",
                "reddit" => "Reddit",
                "wikipedia" => "Википедия",
                "facebook" or "fb" => "Facebook",
                "instagram" => "Instagram",
                "telegram" or "t" => "Telegram",
                "discord" => "Discord",
                "spotify" => "Spotify",
                "netflix" => "Netflix",
                "amazon" => "Amazon",
                "ebay" => "eBay",
                "steam" => "Steam",
                _ => char.ToUpper(baseName[0]) + (baseName.Length > 1 ? baseName.Substring(1) : "")
            };
        }

        private void AddTileInputs_Changed(object sender, TextChangedEventArgs e)
        {
            // If sender is AddTilePathInput, check if it's a URL and auto-fill title / icon if user hasn't specified them
            if (sender == AddTilePathInput)
            {
                string text = AddTilePathInput.Text?.Trim() ?? "";
                if (IsWebUrl(text, out _, out string domainOrName))
                {
                    if (string.IsNullOrWhiteSpace(AddTileTitleInput.Text))
                    {
                        AddTileTitleInput.Text = PrettyDomainTitle(domainOrName);
                    }

                    if (string.IsNullOrEmpty(_addTileCustomIconPath))
                    {
                        string icon = MetroIconResolver.ResolveIconPath(domainOrName);
                        if (string.IsNullOrEmpty(icon))
                        {
                            string pretty = PrettyDomainTitle(domainOrName);
                            icon = MetroIconResolver.ResolveIconPath(pretty);
                        }
                        if (string.IsNullOrEmpty(icon))
                        {
                            icon = MetroIconResolver.ResolveIconPath("Internet Explorer 10");
                        }
                        if (!string.IsNullOrEmpty(icon))
                        {
                            _addTileCustomIconPath = icon;
                        }
                    }
                }
            }

            UpdateTilePreview();
        }

        private void SelectTileSize_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string sizeStr)
            {
                _addTileSelectedSize = sizeStr switch
                {
                    "Small" => TileSize.Small,
                    "Wide" => TileSize.Wide,
                    "Large" => TileSize.Large,
                    _ => TileSize.Medium
                };

                foreach (Button child in TileSizeSelectorGrid.Children.OfType<Button>())
                {
                    bool isCurrent = (child == btn);
                    child.Background = isCurrent ? (Brush)FindResource("ThemeAccentBrush") : new SolidColorBrush(Color.FromArgb(255, 38, 38, 38));
                    child.BorderBrush = isCurrent ? Brushes.White : new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
                    child.BorderThickness = isCurrent ? new Thickness(2) : new Thickness(1);
                }

                UpdateTilePreview();
            }
        }

        private void UpdateTilePreview()
        {
            if (TilePreviewBox == null) return;

            // 1. Цвет
            try
            {
                TilePreviewBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_addTileSelectedColor));
                if (AddTileColorSwatch != null)
                {
                    AddTileColorSwatch.Background = TilePreviewBox.Background;
                }
            }
            catch
            {
                TilePreviewBox.Background = new SolidColorBrush(Color.FromRgb(0, 120, 215));
            }

            // 2. Название
            string title = AddTileTitleInput?.Text?.Trim() ?? "";
            TilePreviewTitle.Text = string.IsNullOrEmpty(title) ? "Моя плитка" : title;

            // 3. Размер предпросмотра
            switch (_addTileSelectedSize)
            {
                case TileSize.Small:
                    TilePreviewBox.Width = 71;
                    TilePreviewBox.Height = 71;
                    TilePreviewTitle.Visibility = Visibility.Collapsed;
                    break;
                case TileSize.Wide:
                    TilePreviewBox.Width = 240;
                    TilePreviewBox.Height = 116;
                    TilePreviewTitle.Visibility = Visibility.Visible;
                    break;
                case TileSize.Large:
                    TilePreviewBox.Width = 180;
                    TilePreviewBox.Height = 180;
                    TilePreviewTitle.Visibility = Visibility.Visible;
                    break;
                case TileSize.Medium:
                default:
                    TilePreviewBox.Width = 140;
                    TilePreviewBox.Height = 140;
                    TilePreviewTitle.Visibility = Visibility.Visible;
                    break;
            }

            // 4. Иконка и её масштаб/приближение
            TilePreviewImage.Width = _addTileIconSize;
            TilePreviewImage.Height = _addTileIconSize;
            TilePreviewImage.Stretch = _addTileIconStretch == "UniformToFill" ? Stretch.UniformToFill : Stretch.Uniform;

            TilePreviewVector.Width = Math.Min(44, _addTileIconSize);
            TilePreviewVector.Height = Math.Min(44, _addTileIconSize);

            if (!string.IsNullOrEmpty(_addTileCustomIconPath) && File.Exists(_addTileCustomIconPath))
            {
                try
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.UriSource = new Uri(_addTileCustomIconPath, UriKind.Absolute);
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.EndInit();
                    bi.Freeze();
                    TilePreviewImage.Source = bi;
                    TilePreviewImage.Visibility = Visibility.Visible;
                    TilePreviewVector.Visibility = Visibility.Collapsed;
                    TilePreviewGlyph.Visibility = Visibility.Collapsed;
                }
                catch
                {
                    TilePreviewImage.Visibility = Visibility.Collapsed;
                    TilePreviewVector.Visibility = Visibility.Visible;
                }
            }
            else
            {
                TilePreviewImage.Visibility = Visibility.Collapsed;
                TilePreviewVector.Visibility = Visibility.Visible;
            }
        }

        private void AddTilePathInput_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) ||
                e.Data.GetDataPresent(DataFormats.Text) ||
                e.Data.GetDataPresent(DataFormats.UnicodeText))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void AddTilePathInput_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    string droppedFile = files[0];
                    AddTilePathInput.Text = droppedFile;
                    if (string.IsNullOrWhiteSpace(AddTileTitleInput.Text))
                    {
                        AddTileTitleInput.Text = Path.GetFileNameWithoutExtension(droppedFile);
                    }
                    string? icon = ExtractAndSaveIcon(droppedFile);
                    if (!string.IsNullOrEmpty(icon))
                    {
                        _addTileCustomIconPath = icon;
                    }
                    UpdateTilePreview();
                    e.Handled = true;
                    return;
                }
            }

            if (e.Data.GetDataPresent(DataFormats.UnicodeText) || e.Data.GetDataPresent(DataFormats.Text))
            {
                string text = (e.Data.GetData(DataFormats.UnicodeText) as string ?? e.Data.GetData(DataFormats.Text) as string)?.Trim() ?? "";
                if (!string.IsNullOrEmpty(text))
                {
                    AddTilePathInput.Text = text;
                    e.Handled = true;
                }
            }
        }

        private void BrowseTileApp_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Выберите приложение или файл для плитки",
                Filter = "Все исполняемые файлы и ярлыки (*.exe;*.lnk;*.bat;*.*)|*.exe;*.lnk;*.bat;*.*|Исполняемые файлы (*.exe)|*.exe|Ярлыки (*.lnk)|*.lnk|Все файлы (*.*)|*.*",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() == true)
            {
                string filePath = dlg.FileName;
                AddTilePathInput.Text = filePath;

                if (string.IsNullOrWhiteSpace(AddTileTitleInput.Text))
                {
                    AddTileTitleInput.Text = Path.GetFileNameWithoutExtension(filePath);
                }

                string? extractedIcon = ExtractAndSaveIcon(filePath);
                if (!string.IsNullOrEmpty(extractedIcon))
                {
                    _addTileCustomIconPath = extractedIcon;
                }

                UpdateTilePreview();
            }
        }

        private string? ExtractAndSaveIcon(string targetPath)
        {
            try
            {
                using var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(targetPath);
                if (sysIcon != null)
                {
                    using var bmp = sysIcon.ToBitmap();
                    string iconsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen", "CustomIcons");
                    if (!Directory.Exists(iconsDir)) Directory.CreateDirectory(iconsDir);

                    string safeName = string.Join("_", Path.GetFileNameWithoutExtension(targetPath).Split(Path.GetInvalidFileNameChars()));
                    string destFile = Path.Combine(iconsDir, $"{safeName}_{Guid.NewGuid():N}.png");
                    bmp.Save(destFile, System.Drawing.Imaging.ImageFormat.Png);
                    return destFile;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExtractAndSaveIcon] Error: {ex.Message}");
            }
            return null;
        }

        private void BrowseTileIcon_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Выберите значок для плитки",
                Filter = "Файлы изображений и значков (*.png;*.ico;*.jpg;*.jpeg;*.bmp)|*.png;*.ico;*.jpg;*.jpeg;*.bmp|Все файлы (*.*)|*.*",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() == true)
            {
                _addTileCustomIconPath = dlg.FileName;
                UpdateTilePreview();
            }
        }

        private void ResetTileIcon_Click(object sender, RoutedEventArgs e)
        {
            _addTileCustomIconPath = string.Empty;
            _addTileIconSize = 64.0;
            _addTileIconStretch = "Uniform";
            if (AddTileIconSizeSlider != null) AddTileIconSizeSlider.Value = 64;
            UpdateTilePreview();
        }

        private void CreateCustomTile_Click(object sender, RoutedEventArgs e)
        {
            string title = AddTileTitleInput.Text?.Trim() ?? "";
            string rawPath = AddTilePathInput.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(rawPath))
            {
                AddTileStatusMessage.Text = "Пожалуйста, укажите приложение, файл или ссылку (URL).";
                AddTileStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
                AddTileStatusMessage.Visibility = Visibility.Visible;
                AddTilePathInput.Focus();
                return;
            }

            bool isUrl = IsWebUrl(rawPath, out string normalizedUrl, out string domain);
            string finalExecutablePath = isUrl ? normalizedUrl : rawPath;

            if (string.IsNullOrEmpty(title))
            {
                if (isUrl)
                {
                    title = PrettyDomainTitle(domain);
                }
                else
                {
                    AddTileStatusMessage.Text = "Пожалуйста, укажите название плитки.";
                    AddTileStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
                    AddTileStatusMessage.Visibility = Visibility.Visible;
                    AddTileTitleInput.Focus();
                    return;
                }
            }

            if (string.IsNullOrEmpty(_addTileCustomIconPath))
            {
                if (isUrl)
                {
                    string icon = MetroIconResolver.ResolveIconPath(domain);
                    if (string.IsNullOrEmpty(icon)) icon = MetroIconResolver.ResolveIconPath(PrettyDomainTitle(domain));
                    if (string.IsNullOrEmpty(icon)) icon = MetroIconResolver.ResolveIconPath("Internet Explorer 10");
                    if (!string.IsNullOrEmpty(icon)) _addTileCustomIconPath = icon;
                }
                else if (File.Exists(finalExecutablePath))
                {
                    _addTileCustomIconPath = ExtractAndSaveIcon(finalExecutablePath) ?? "";
                }
            }

            var newTile = new TileModel
            {
                Id = "custom_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Title = title,
                ExecutablePath = finalExecutablePath,
                Size = _addTileSelectedSize,
                BackgroundColor = _addTileSelectedColor,
                BackgroundBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_addTileSelectedColor)),
                IconImagePath = _addTileCustomIconPath ?? "",
                IconSize = _addTileIconSize,
                IconScale = 1.0,
                IconStretch = _addTileIconStretch,
                IconGlyph = string.IsNullOrEmpty(_addTileCustomIconPath) ? "★" : "",
                IsLiveTileEnabled = false
            };

            double maxRight = StartTiles.Count > 0 ? StartTiles.Max(t => t.X + t.PixelWidth) : 0;
            double targetX = Math.Ceiling(maxRight / 158.0) * 158.0;
            newTile.X = targetX;
            newTile.Y = 0;

            StartTiles.Add(newTile);
            SanitizeTileLayout(StartTiles);

            var control = new LiveTileControl
            {
                DataContext = newTile
            };
            Canvas.SetLeft(control, newTile.X);
            Canvas.SetTop(control, newTile.Y);
            StartTilesCanvas.Children.Add(control);

            double newMax = StartTiles.Max(t => t.X + t.PixelWidth);
            SyncCanvasWidths(Math.Max(2400, newMax + 300));
            UpdateGroupHeaders();

            SaveLayoutConfig();

            StartTilesScrollViewer.ScrollToHorizontalOffset(Math.Max(0, newTile.X - 300));

            AddTileStatusMessage.Text = $"Плитка «{title}» успешно создана и добавлена на экран «Пуск»!";
            AddTileStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(128, 255, 128));
            AddTileStatusMessage.Visibility = Visibility.Visible;

            AddTileTitleInput.Text = string.Empty;
            AddTilePathInput.Text = string.Empty;
            _addTileCustomIconPath = string.Empty;
            UpdateTilePreview();
        }

        // =========================================================================
        // БЫСТРЫЕ ДЕЙСТВИЯ И СИСТЕМНЫЕ ССЫЛКИ ИЗ ПАРАМЕТРОВ (WINDOWS 8.1)
        // =========================================================================
        private void OpenSettingsPCSettings_Click(object sender, RoutedEventArgs e)
        {
            CloseSettingsCharm();
            CloseScreenAnimated();
            try { Process.Start(new ProcessStartInfo("ms-settings:") { UseShellExecute = true }); }
            catch { }
        }

        private void OpenSettingsControlPanel_Click(object sender, RoutedEventArgs e)
        {
            CloseSettingsCharm();
            CloseScreenAnimated();
            try { Process.Start(new ProcessStartInfo("control.exe") { UseShellExecute = true }); }
            catch { }
        }

        private void OpenSettingsTaskManager_Click(object sender, RoutedEventArgs e)
        {
            CloseSettingsCharm();
            CloseScreenAnimated();
            try { Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); }
            catch { }
        }

        private void OpenSettingsStudio_Click(object sender, RoutedEventArgs e)
        {
            NavigateToSettingsStudio();
        }

        // =========================================================================
        // ВСТРОЕННЫЙ РЕДАКТОР ЭКРАНА (STUDIO): РАЗМЕР, ПЕРЕМЕЩЕНИЕ И НАСТРОЙКА
        // =========================================================================
        private TileModel? _studioSelectedTile = null;
        private bool _isStudioUpdating = false;
        private bool _isStudioEyedropper = false;

        private void InitStudioView(TileModel? targetTile = null)
        {
            _isStudioUpdating = true;
            try
            {
                InitStudioColorsPalette();

                StudioTileSelector.ItemsSource = null;
                StudioTileSelector.ItemsSource = StartTiles.ToList();

                if (targetTile != null && StartTiles.Contains(targetTile))
                {
                    StudioTileSelector.SelectedItem = targetTile;
                }
                else if (StartTiles.Count > 0)
                {
                    StudioTileSelector.SelectedIndex = 0;
                }

                UpdateStudioSelectedTileUI();
            }
            finally
            {
                _isStudioUpdating = false;
            }
        }

        private void InitStudioColorsPalette()
        {
            if (StudioColorsWrapPanel == null || StudioColorsWrapPanel.Children.Count > 0) return;

            string[] metroColors = new[]
            {
                "#FF0078D7", "#FF004E8C", "#FF008A00", "#FF107C41",
                "#FF68217A", "#FFD80073", "#FFD24726", "#FFFF4500",
                "#FFFF8C00", "#FF008299", "#FF00A4EF", "#FF2D2D30"
            };

            foreach (var colorHex in metroColors)
            {
                var btn = new Border
                {
                    Width = 32,
                    Height = 32,
                    Margin = new Thickness(0, 0, 8, 8),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                    Cursor = Cursors.Hand,
                    Tag = colorHex,
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255))
                };

                string hexCapture = colorHex;
                btn.MouseLeftButtonUp += (s, e) =>
                {
                    if (_studioSelectedTile == null) return;
                    _studioSelectedTile.BackgroundColor = hexCapture;
                    SaveLayoutConfig();
                    UpdateStudioSelectedTileUI();
                };
                StudioColorsWrapPanel.Children.Add(btn);
            }
        }

        private void StudioTileSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isStudioUpdating) return;
            _studioSelectedTile = StudioTileSelector.SelectedItem as TileModel;
            UpdateStudioSelectedTileUI();
        }

        private void UpdateStudioSelectedTileUI()
        {
            _studioSelectedTile = StudioTileSelector.SelectedItem as TileModel;
            if (_studioSelectedTile == null)
            {
                StudioTileTitleText.Text = "Плитка не выбрана";
                StudioTileDetailsText.Text = "Размер: -, Позиция: -";
                HighlightStudioSizeButtons(null);
                if (StudioTilePreviewBox != null) StudioTilePreviewBox.Background = Brushes.Transparent;
                if (StudioTilePreviewImage != null) StudioTilePreviewImage.Visibility = Visibility.Collapsed;
                if (StudioTilePreviewVector != null) StudioTilePreviewVector.Visibility = Visibility.Collapsed;
                if (StudioTilePreviewGlyph != null) StudioTilePreviewGlyph.Visibility = Visibility.Collapsed;
                return;
            }

            string sizeStr = _studioSelectedTile.Size switch
            {
                TileSize.Small => "◽ Мелкий (71×71)",
                TileSize.Medium => "◻ Средний (150×150)",
                TileSize.Wide => "▭ Широкий (308×150)",
                TileSize.Large => "◼ Большой (308×308)",
                _ => "◻ Средний"
            };

            StudioTileTitleText.Text = _studioSelectedTile.Title;
            StudioTileDetailsText.Text = $"Размер: {sizeStr}\nПозиция: X={Math.Round(_studioSelectedTile.X)}, Y={Math.Round(_studioSelectedTile.Y)}";
            HighlightStudioSizeButtons(_studioSelectedTile.Size);

            // Обновление заголовка в поле ввода
            if (StudioTileTitleInput != null && StudioTileTitleInput.Text != _studioSelectedTile.Title)
            {
                _isStudioUpdating = true;
                StudioTileTitleInput.Text = _studioSelectedTile.Title;
                _isStudioUpdating = false;
            }

            // Обновление цвета и образца
            if (StudioTilePreviewBox != null)
            {
                StudioTilePreviewBox.Background = _studioSelectedTile.BackgroundBrush;
            }
            if (StudioColorSwatch != null)
            {
                StudioColorSwatch.Background = _studioSelectedTile.BackgroundBrush;
            }
            if (StudioColorHexInput != null && StudioColorHexInput.Text != _studioSelectedTile.BackgroundColor)
            {
                _isStudioUpdating = true;
                StudioColorHexInput.Text = _studioSelectedTile.BackgroundColor;
                _isStudioUpdating = false;
            }

            // Обновление ползунка размера значка
            if (StudioIconSizeSlider != null)
            {
                _isStudioUpdating = true;
                StudioIconSizeSlider.Value = Math.Clamp(_studioSelectedTile.IconSize, 24, 300);
                _isStudioUpdating = false;
            }
            if (StudioIconSizeText != null)
            {
                StudioIconSizeText.Text = $"{_studioSelectedTile.IconSize:F0} px ({(_studioSelectedTile.IconSize / 64.0) * 100:F0}%)";
            }

            // Обновление миниатюры значка в превью
            if (_studioSelectedTile.HasIconImage && _studioSelectedTile.IconImageSource != null)
            {
                if (StudioTilePreviewImage != null)
                {
                    StudioTilePreviewImage.Source = _studioSelectedTile.IconImageSource;
                    StudioTilePreviewImage.Visibility = Visibility.Visible;
                }
                if (StudioTilePreviewVector != null) StudioTilePreviewVector.Visibility = Visibility.Collapsed;
                if (StudioTilePreviewGlyph != null) StudioTilePreviewGlyph.Visibility = Visibility.Collapsed;
            }
            else if (_studioSelectedTile.HasVectorPath)
            {
                if (StudioTilePreviewVector != null)
                {
                    try
                    {
                        StudioTilePreviewVector.Data = Geometry.Parse(_studioSelectedTile.IconVectorPath);
                        StudioTilePreviewVector.Visibility = Visibility.Visible;
                    }
                    catch
                    {
                        StudioTilePreviewVector.Visibility = Visibility.Collapsed;
                    }
                }
                if (StudioTilePreviewImage != null) StudioTilePreviewImage.Visibility = Visibility.Collapsed;
                if (StudioTilePreviewGlyph != null) StudioTilePreviewGlyph.Visibility = Visibility.Collapsed;
            }
            else if (!string.IsNullOrEmpty(_studioSelectedTile.IconGlyph))
            {
                if (StudioTilePreviewGlyph != null)
                {
                    StudioTilePreviewGlyph.Text = _studioSelectedTile.IconGlyph;
                    StudioTilePreviewGlyph.Visibility = Visibility.Visible;
                }
                if (StudioTilePreviewImage != null) StudioTilePreviewImage.Visibility = Visibility.Collapsed;
                if (StudioTilePreviewVector != null) StudioTilePreviewVector.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (StudioTilePreviewImage != null) StudioTilePreviewImage.Visibility = Visibility.Collapsed;
                if (StudioTilePreviewVector != null) StudioTilePreviewVector.Visibility = Visibility.Collapsed;
                if (StudioTilePreviewGlyph != null) StudioTilePreviewGlyph.Visibility = Visibility.Collapsed;
            }

            // Выделение активного цвета в палитре
            if (StudioColorsWrapPanel != null)
            {
                foreach (UIElement child in StudioColorsWrapPanel.Children)
                {
                    if (child is Border b && b.Tag is string hex)
                    {
                        bool isSel = string.Equals(hex, _studioSelectedTile.BackgroundColor, StringComparison.OrdinalIgnoreCase);
                        b.BorderThickness = isSel ? new Thickness(2) : new Thickness(1);
                        b.BorderBrush = isSel ? Brushes.White : new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
                    }
                }
            }

            // Прокручиваем холст к выбранной плитке для наглядности
            StartTilesScrollViewer.ScrollToHorizontalOffset(Math.Max(0, _studioSelectedTile.X - 200));
        }

        private void StudioTileTitleInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isStudioUpdating || _studioSelectedTile == null) return;
            _studioSelectedTile.Title = StudioTileTitleInput.Text?.Trim() ?? "";
            SaveLayoutConfig();
            if (StudioTileTitleText != null) StudioTileTitleText.Text = _studioSelectedTile.Title;
        }

        private void StudioOpenIconCatalog_Click(object sender, RoutedEventArgs e)
        {
            if (_studioSelectedTile == null) return;
            try
            {
                var dlg = new Win8StartScreen.Views.MetroIconPickerDialog
                {
                    Owner = this
                };
                if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.SelectedIconPath))
                {
                    _studioSelectedTile.IconImagePath = dlg.SelectedIconPath;
                    _studioSelectedTile.IconVectorPath = string.Empty;
                    _studioSelectedTile.IconGlyph = string.Empty;
                    if (dlg.ChosenIconSize > 0)
                    {
                        _studioSelectedTile.IconSize = dlg.ChosenIconSize;
                        if (StudioIconSizeSlider != null) StudioIconSizeSlider.Value = dlg.ChosenIconSize;
                    }
                    SaveLayoutConfig();
                    UpdateStudioSelectedTileUI();
                    ShowStudioStatus($"Значок плитки «{_studioSelectedTile.Title}» успешно изменен!");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StudioOpenIconCatalog] Error: {ex.Message}");
            }
        }

        private void StudioBrowseIcon_Click(object sender, RoutedEventArgs e)
        {
            if (_studioSelectedTile == null) return;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Выберите значок для плитки",
                Filter = "Файлы изображений и значков (*.png;*.ico;*.jpg;*.jpeg;*.bmp;*.webp;*.svg)|*.png;*.ico;*.jpg;*.jpeg;*.bmp;*.webp;*.svg|Все файлы (*.*)|*.*",
                CheckFileExists = true
            };
            if (dlg.ShowDialog() == true && System.IO.File.Exists(dlg.FileName))
            {
                _studioSelectedTile.IconImagePath = dlg.FileName;
                _studioSelectedTile.IconVectorPath = string.Empty;
                _studioSelectedTile.IconGlyph = string.Empty;
                SaveLayoutConfig();
                UpdateStudioSelectedTileUI();
                ShowStudioStatus($"Значок плитки «{_studioSelectedTile.Title}» обновлен из файла!");
            }
        }

        private void StudioResetIcon_Click(object sender, RoutedEventArgs e)
        {
            if (_studioSelectedTile == null) return;
            _studioSelectedTile.IconImagePath = string.Empty;
            _studioSelectedTile.IconSize = TileModel.GetDefaultIconSize(_studioSelectedTile.Size);
            _studioSelectedTile.IconStretch = "Uniform";
            if (StudioIconSizeSlider != null) StudioIconSizeSlider.Value = _studioSelectedTile.IconSize;
            SaveLayoutConfig();
            UpdateStudioSelectedTileUI();
            ShowStudioStatus("Значок плитки сброшен.");
        }

        private void StudioIconSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isStudioUpdating || _studioSelectedTile == null) return;
            _studioSelectedTile.IconSize = e.NewValue;
            if (StudioIconSizeText != null)
            {
                StudioIconSizeText.Text = $"{e.NewValue:F0} px ({(e.NewValue / 64.0) * 100:F0}%)";
            }
            SaveLayoutConfig();
        }

        private void StudioIconPreset_Click(object sender, RoutedEventArgs e)
        {
            if (_studioSelectedTile == null) return;
            if (sender is Button btn && btn.Tag is string tag)
            {
                if (tag == "MAX")
                {
                    _studioSelectedTile.IconStretch = "UniformToFill";
                    _studioSelectedTile.IconSize = _studioSelectedTile.Size switch
                    {
                        TileSize.Small => 71,
                        TileSize.Wide => 240,
                        TileSize.Large => 240,
                        _ => 150
                    };
                }
                else if (tag == "RESET")
                {
                    _studioSelectedTile.IconStretch = "Uniform";
                    _studioSelectedTile.IconSize = TileModel.GetDefaultIconSize(_studioSelectedTile.Size);
                }
                else if (double.TryParse(tag, out double size))
                {
                    _studioSelectedTile.IconStretch = "Uniform";
                    _studioSelectedTile.IconSize = size;
                }
                if (StudioIconSizeSlider != null) StudioIconSizeSlider.Value = _studioSelectedTile.IconSize;
                SaveLayoutConfig();
                UpdateStudioSelectedTileUI();
            }
        }

        private void StudioColorHexInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isStudioUpdating || _studioSelectedTile == null) return;
            string hex = StudioColorHexInput.Text?.Trim() ?? "";
            if (!hex.StartsWith("#")) hex = "#" + hex;
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                _studioSelectedTile.BackgroundColor = hex;
                if (StudioColorSwatch != null) StudioColorSwatch.Background = new SolidColorBrush(color);
                if (StudioTilePreviewBox != null) StudioTilePreviewBox.Background = new SolidColorBrush(color);
                SaveLayoutConfig();
            }
            catch { }
        }

        private void StudioEyedropper_Click(object sender, RoutedEventArgs e)
        {
            _isStudioEyedropper = true;
            Eyedropper_Click(sender, e);
        }

        private void ShowStudioStatus(string message)
        {
            if (StudioStatusMessage == null) return;
            StudioStatusMessage.Text = message;
            StudioStatusMessage.Visibility = Visibility.Visible;
        }

        private void HighlightStudioSizeButtons(TileSize? activeSize)
        {
            var inactiveBg = new SolidColorBrush(Color.FromRgb(38, 38, 38));
            var inactiveBorder = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            var activeBorder = Brushes.White;
            var activeBg = (Brush)FindResource("ThemeAccentBrush");

            Button[] btns = { StudioSizeSmallBtn, StudioSizeMediumBtn, StudioSizeWideBtn, StudioSizeLargeBtn };
            TileSize[] sizes = { TileSize.Small, TileSize.Medium, TileSize.Wide, TileSize.Large };

            for (int i = 0; i < btns.Length; i++)
            {
                if (btns[i] == null) continue;
                bool isCur = activeSize.HasValue && sizes[i] == activeSize.Value;
                btns[i].Background = isCur ? activeBg : inactiveBg;
                btns[i].BorderBrush = isCur ? activeBorder : inactiveBorder;
                btns[i].BorderThickness = new Thickness(isCur ? 2 : 1);
            }
        }

        private void StudioSize_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string tagStr) return;
            if (_studioSelectedTile == null) return;

            TileSize targetSize = tagStr switch
            {
                "Small" => TileSize.Small,
                "Wide" => TileSize.Wide,
                "Large" => TileSize.Large,
                _ => TileSize.Medium
            };

            _studioSelectedTile.Size = targetSize;
            SanitizeTileLayout(StartTiles);
            RelayoutTileControls(true);
            SaveLayoutConfig();

            UpdateStudioSelectedTileUI();

            StudioStatusMessage.Text = $"Размер плитки «{_studioSelectedTile.Title}» изменен на {tagStr}!";
            StudioStatusMessage.Visibility = Visibility.Visible;
        }

        private void StudioMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (_studioSelectedTile == null) return;
            _studioSelectedTile.Y = Math.Max(0, _studioSelectedTile.Y - 156.0);
            ApplyStudioMoveAndRelayout();
        }

        private void StudioMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (_studioSelectedTile == null) return;
            double maxAllowedY = Math.Max(0, GetAvailableCanvasHeight() - _studioSelectedTile.PixelHeight);
            _studioSelectedTile.Y = Math.Min(maxAllowedY, _studioSelectedTile.Y + 156.0);
            ApplyStudioMoveAndRelayout();
        }

        private void StudioMoveLeft_Click(object sender, RoutedEventArgs e)
        {
            if (_studioSelectedTile == null) return;
            _studioSelectedTile.X = Math.Max(0, _studioSelectedTile.X - 156.0);
            ApplyStudioMoveAndRelayout();
        }

        private void StudioMoveRight_Click(object sender, RoutedEventArgs e)
        {
            if (_studioSelectedTile == null) return;
            _studioSelectedTile.X += 156.0;
            ApplyStudioMoveAndRelayout();
        }

        private void StudioSwapNext_Click(object sender, RoutedEventArgs e)
        {
            if (_studioSelectedTile == null) return;
            int idx = StartTiles.IndexOf(_studioSelectedTile);
            if (idx < 0 || StartTiles.Count < 2) return;

            int nextIdx = (idx + 1) % StartTiles.Count;
            var nextTile = StartTiles[nextIdx];

            // Меняем координаты местами
            double tmpX = _studioSelectedTile.X;
            double tmpY = _studioSelectedTile.Y;
            _studioSelectedTile.X = nextTile.X;
            _studioSelectedTile.Y = nextTile.Y;
            nextTile.X = tmpX;
            nextTile.Y = tmpY;

            ApplyStudioMoveAndRelayout();
        }

        private void ApplyStudioMoveAndRelayout()
        {
            SanitizeTileLayout(StartTiles);
            RelayoutTileControls(true);
            SaveLayoutConfig();
            UpdateStudioSelectedTileUI();
        }

        private void StudioToggleLiveTile_Click(object sender, RoutedEventArgs e)
        {
            if (_studioSelectedTile == null) return;
            _studioSelectedTile.IsLiveTileEnabled = !_studioSelectedTile.IsLiveTileEnabled;
            SaveLayoutConfig();
            StudioStatusMessage.Text = _studioSelectedTile.IsLiveTileEnabled 
                ? $"Динамическая плитка «{_studioSelectedTile.Title}» включена!"
                : $"Динамическая плитка «{_studioSelectedTile.Title}» выключена!";
            StudioStatusMessage.Visibility = Visibility.Visible;
        }

        private void StudioUnpinTile_Click(object sender, RoutedEventArgs e)
        {
            if (_studioSelectedTile == null) return;
            var toRemove = _studioSelectedTile;
            UnpinTile(toRemove);
            InitStudioView();
            StudioStatusMessage.Text = $"Плитка «{toRemove.Title}» откреплена от начального экрана!";
            StudioStatusMessage.Visibility = Visibility.Visible;
        }

        private void SystemNetwork_Click(object sender, RoutedEventArgs e)
        {
            CloseSettingsCharm();
            try { Process.Start(new ProcessStartInfo("ms-settings:network") { UseShellExecute = true }); }
            catch { try { Process.Start(new ProcessStartInfo("control.exe", "ncpa.cpl") { UseShellExecute = true }); } catch { } }
        }

        private void SystemVolume_Click(object sender, RoutedEventArgs e)
        {
            CloseSettingsCharm();
            try { Process.Start(new ProcessStartInfo("sndvol.exe") { UseShellExecute = true }); }
            catch { try { Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true }); } catch { } }
        }

        private void SystemBrightness_Click(object sender, RoutedEventArgs e)
        {
            CloseSettingsCharm();
            try { Process.Start(new ProcessStartInfo("ms-settings:display") { UseShellExecute = true }); }
            catch { }
        }

        private void SystemNotifications_Click(object sender, RoutedEventArgs e)
        {
            CloseSettingsCharm();
            try { Process.Start(new ProcessStartInfo("ms-settings:notifications") { UseShellExecute = true }); }
            catch { }
        }

        private void SystemKeyboard_Click(object sender, RoutedEventArgs e)
        {
            CloseSettingsCharm();
            try { Process.Start(new ProcessStartInfo("osk.exe") { UseShellExecute = true }); }
            catch { }
        }

        private void InitializePersonalizationPalette()
        {
            // Фоновые узоры Windows 8.1
            string[] patterns = { "waves_01", "ribbons_02", "lines_03", "mesh_04" };
            foreach (var p in patterns)
            {
                var btn = new Button
                {
                    Width = 68,
                    Height = 44,
                    Margin = new Thickness(4),
                    Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                    BorderThickness = new Thickness(1),
                    BorderBrush = Brushes.White,
                    Cursor = Cursors.Hand,
                    Content = new TextBlock
                    {
                        Text = p == "waves_01" ? "♒ Волны" : p == "ribbons_02" ? "〰 Ленты" : p == "lines_03" ? "☰ Линии" : "▦ Сетка",
                        FontSize = 11,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                };
                btn.Click += (s, e) =>
                {
                    PatternPath1.Opacity = (p == "lines_03") ? 0.3 : (p == "mesh_04" ? 0.2 : 0.7);
                    PatternPath2.Opacity = (p == "mesh_04") ? 0.8 : (p == "lines_03" ? 0.2 : 0.4);
                    ThemeManager.CurrentTheme.PatternId = p;
                    ThemeManager.SaveTheme();
                    SaveLayoutConfig();
                };
                PatternsWrapPanel.Children.Add(btn);
            }

            // Цветовая тема (Windows 8.1 Color Theme Matrix - меняет фоновые линии)
            InitializeThemeColorMatrix();

            // Матрица цветов фона (Windows 8.1 Background Matrix: 3x6 оттенков + 18 базовых цветов)
            InitializeBackgroundColorMatrix();

            // Цвета системного акцента
            string[] accentColors = {
                "#FF0078D7", "#FF00A4EF", "#FF107C41", "#FFD83B01",
                "#FFB91D47", "#FF68217A", "#FFFF8C00", "#FF008272",
                "#FF880015", "#FFE81123", "#FF9B00E8", "#FF00B294"
            };

            foreach (var color in accentColors)
            {
                var btn = new Button
                {
                    Width = 34,
                    Height = 34,
                    Margin = new Thickness(3),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
                    Cursor = Cursors.Hand
                };
                btn.Click += (s, e) =>
                {
                    ThemeManager.CurrentTheme.AccentColor = color;
                    ThemeManager.ApplyTheme(ThemeManager.CurrentTheme);
                    SaveLayoutConfig();
                };
                AccentColorsWrapPanel.Children.Add(btn);
            }

            UpdateCustomWallpaperUI();
        }

        private void UpdateCustomWallpaperUI()
        {
            string path = ThemeManager.CurrentTheme.CustomWallpaperPath;
            double darkness = ThemeManager.CurrentTheme.CustomWallpaperDarkness;

            bool hasCustom = !string.IsNullOrEmpty(path) && File.Exists(path);
            ResetWallpaperButton.Visibility = hasCustom ? Visibility.Visible : Visibility.Collapsed;
            WallpaperDarknessContainer.Visibility = hasCustom ? Visibility.Visible : Visibility.Collapsed;
            UploadWallpaperButton.Content = hasCustom ? "📁 Сменить обои..." : "📁 Выбрать обои...";

            WallpaperDarknessSlider.Value = Math.Clamp(darkness, 0.1, 0.85);
            DarknessValueTextBlock.Text = $"{(int)(WallpaperDarknessSlider.Value * 100)}%";

            ApplyCustomWallpaper(path, darkness);
        }

        public void ApplyCustomWallpaper(string? path, double darkness)
        {
            if (CustomWallpaperImage == null || CustomWallpaperDarkTint == null) return;

            darkness = Math.Clamp(darkness, 0.05, 0.95);
            CustomWallpaperDarkTint.Opacity = darkness;

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    var ms = new MemoryStream(bytes);

                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = ms;
                    bi.EndInit();
                    bi.Freeze();

                    CustomWallpaperImage.Source = bi;
                    CustomWallpaperImage.Visibility = Visibility.Visible;
                    CustomWallpaperDarkTint.Visibility = Visibility.Visible;
                    if (RibbonContainer != null)
                    {
                        RibbonContainer.BeginAnimation(OpacityProperty, null);
                        RibbonContainer.Opacity = 0.0;
                        RibbonContainer.Visibility = Visibility.Collapsed;
                    }
                    if (RibbonCanvas != null)
                    {
                        RibbonCanvas.BeginAnimation(OpacityProperty, null);
                        RibbonCanvas.Opacity = 0.0;
                        RibbonCanvas.Visibility = Visibility.Collapsed;
                    }
                }
                catch (Exception ex)
                {
                    string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen", "app.log");
                    try { File.AppendAllText(logPath, $"[{DateTime.Now}] ApplyCustomWallpaper error: {ex}\n"); } catch { }
                    CustomWallpaperImage.Source = null;
                    CustomWallpaperImage.Visibility = Visibility.Collapsed;
                    CustomWallpaperDarkTint.Visibility = Visibility.Collapsed;
                    if (RibbonContainer != null) RibbonContainer.Visibility = Visibility.Visible;
                    if (RibbonCanvas != null) RibbonCanvas.Visibility = Visibility.Visible;
                }
            }
            else
            {
                CustomWallpaperImage.Source = null;
                CustomWallpaperImage.Visibility = Visibility.Collapsed;
                CustomWallpaperDarkTint.Visibility = Visibility.Collapsed;
                if (RibbonContainer != null) RibbonContainer.Visibility = Visibility.Visible;
                if (RibbonCanvas != null) RibbonCanvas.Visibility = Visibility.Visible;
            }
        }

        private void UploadWallpaper_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Выберите фоновое изображение для экрана Пуск",
                Filter = "Изображения (*.jpg;*.jpeg;*.png;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.webp|Все файлы (*.*)|*.*",
                Multiselect = false
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    string ext = Path.GetExtension(ofd.FileName);
                    string destDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen", "Wallpapers");
                    Directory.CreateDirectory(destDir);

                    // Удаляем старые кастомные обои, чтобы не накапливать мусор
                    try
                    {
                        var oldFiles = Directory.GetFiles(destDir, "custom_wallpaper*");
                        foreach (var old in oldFiles)
                        {
                            try { File.Delete(old); } catch { }
                        }
                    }
                    catch { }

                    string destFile = Path.Combine(destDir, $"custom_wallpaper_{DateTime.Now.Ticks}{ext}");
                    File.Copy(ofd.FileName, destFile, overwrite: true);

                    ThemeManager.CurrentTheme.CustomWallpaperPath = destFile;
                    if (ThemeManager.CurrentTheme.CustomWallpaperDarkness <= 0.01)
                    {
                        ThemeManager.CurrentTheme.CustomWallpaperDarkness = 0.45;
                    }
                    ThemeManager.SaveTheme();
                    UpdateCustomWallpaperUI();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось загрузить изображение: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ResetWallpaper_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.CurrentTheme.CustomWallpaperPath = "";
            ThemeManager.SaveTheme();
            UpdateCustomWallpaperUI();
        }

        private void WallpaperDarknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DarknessValueTextBlock != null)
            {
                DarknessValueTextBlock.Text = $"{(int)(e.NewValue * 100)}%";
            }
            if (CustomWallpaperDarkTint != null)
            {
                CustomWallpaperDarkTint.Opacity = e.NewValue;
            }
            ThemeManager.CurrentTheme.CustomWallpaperDarkness = e.NewValue;
            ThemeManager.SaveTheme();
        }

        private static readonly string[] BaseHueList = new string[]
        {
            "#FF303030", // 0: Charcoal
            "#FF777777", // 1: Gray
            "#FFDA0025", // 2: Crimson
            "#FFF01800", // 3: Red
            "#FFFF4300", // 4: Red-Orange
            "#FFFD6C05", // 5: Orange
            "#FFFEAB07", // 6: Amber
            "#FFFFC91E", // 7: Yellow
            "#FF93C900", // 8: Lime
            "#FF54C300", // 9: Green
            "#FF00AB62", // 10: Sea Green
            "#FF00C3C4", // 11: Cyan
            "#FF009BF0", // 12: Sky Blue
            "#FF006AFE", // 13: Royal Blue
            "#FF3F00DD", // 14: Deep Violet / Indigo
            "#FF9025FF", // 15: Purple
            "#FFFF3EC2", // 16: Pink
            "#FFFE0B6B"  // 17: Magenta
        };

        private int _selectedHueIndex = 14;
        private string _selectedLineHex = "#FF602FCE";
        private readonly List<Border> _hueBorders = new();

        private int _selectedBgHueIndex = 0;
        private string _selectedBgPrimaryHex = "#FF000000";
        private readonly List<Border> _bgHueBorders = new();

        private void InitializeBackgroundColorMatrix()
        {
            _selectedBgPrimaryHex = string.IsNullOrEmpty(ThemeManager.CurrentTheme.BackgroundPrimary) ? "#FF000000" : ThemeManager.CurrentTheme.BackgroundPrimary;

            // Determine active background hue index
            _selectedBgHueIndex = 0; // Default to Charcoal / Black
            for (int h = 0; h < BaseHueList.Length; h++)
            {
                var shades = GetBackgroundShadesForHue(h);
                if (Array.Exists(shades, s => string.Equals(s, _selectedBgPrimaryHex, StringComparison.OrdinalIgnoreCase)))
                {
                    _selectedBgHueIndex = h;
                    break;
                }
            }

            BgHueSliderPanel.Children.Clear();
            _bgHueBorders.Clear();

            for (int i = 0; i < BaseHueList.Length; i++)
            {
                bool isSel = (i == _selectedBgHueIndex);
                var border = new Border
                {
                    Width = isSel ? 16 : 15,
                    Height = isSel ? 26 : 16,
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(BaseHueList[i])),
                    Margin = (i == 1) ? new Thickness(0, 0, 3, 0) : new Thickness(0, 0, 1, 0),
                    Cursor = Cursors.Hand,
                    BorderBrush = isSel ? Brushes.White : Brushes.Transparent,
                    BorderThickness = isSel ? new Thickness(1.5) : new Thickness(0)
                };

                int hueIdx = i;
                border.MouseLeftButtonUp += (s, e) =>
                {
                    SelectBaseBgHue(hueIdx);
                };

                _bgHueBorders.Add(border);
                BgHueSliderPanel.Children.Add(border);
            }

            PopulateBgShadesGrid(GetBackgroundShadesForHue(_selectedBgHueIndex));
        }

        private void SelectBaseBgHue(int idx)
        {
            _selectedBgHueIndex = idx;
            for (int i = 0; i < _bgHueBorders.Count; i++)
            {
                bool isSel = (i == idx);
                _bgHueBorders[i].Width = isSel ? 16 : 15;
                _bgHueBorders[i].Height = isSel ? 26 : 16;
                _bgHueBorders[i].BorderBrush = isSel ? Brushes.White : Brushes.Transparent;
                _bgHueBorders[i].BorderThickness = isSel ? new Thickness(1.5) : new Thickness(0);
            }

            var shades = GetBackgroundShadesForHue(idx);
            if (!Array.Exists(shades, s => string.Equals(s, _selectedBgPrimaryHex, StringComparison.OrdinalIgnoreCase)))
            {
                _selectedBgPrimaryHex = shades[12];
                ApplyBackgroundColor(_selectedBgPrimaryHex);
            }

            PopulateBgShadesGrid(shades);
        }

        private void PopulateBgShadesGrid(string[] shades)
        {
            BgShadesGrid.Children.Clear();
            foreach (var shadeHex in shades)
            {
                bool isCurrent = string.Equals(shadeHex, _selectedBgPrimaryHex, StringComparison.OrdinalIgnoreCase);
                var btn = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(shadeHex)),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0.5)
                };

                if (isCurrent)
                {
                    btn.Child = new TextBlock
                    {
                        Text = "✓",
                        FontSize = 22,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }

                string curHex = shadeHex;
                btn.MouseLeftButtonUp += (s, e) =>
                {
                    _selectedBgPrimaryHex = curHex;
                    ApplyBackgroundColor(curHex);
                    PopulateBgShadesGrid(shades);
                };

                BgShadesGrid.Children.Add(btn);
            }
        }

        private void ApplyBackgroundColor(string primaryHex)
        {
            Color primColor = (Color)ColorConverter.ConvertFromString(primaryHex);
            byte r2 = (byte)Math.Min(255, (int)(primColor.R * 1.25 + 10));
            byte g2 = (byte)Math.Min(255, (int)(primColor.G * 1.25 + 10));
            byte b2 = (byte)Math.Min(255, (int)(primColor.B * 1.25 + 10));
            string secHex = $"#FF{r2:X2}{g2:X2}{b2:X2}";

            ThemeManager.CurrentTheme.BackgroundPrimary = primaryHex;
            ThemeManager.CurrentTheme.BackgroundSecondary = secHex;
            ThemeManager.ApplyTheme(ThemeManager.CurrentTheme);
            SaveLayoutConfig();
        }

        private string[] GetBackgroundShadesForHue(int hueIndex)
        {
            if (hueIndex == 0) // Exact Windows 8.1 screenshot palette from media_1789201996075.png
            {
                return new string[]
                {
                    "#FF071111", "#FF1F2627", "#FF353C3D", "#FF4B5354", "#FF636B6C", "#FF7C8485",
                    "#FF0A1011", "#FF1E2223", "#FF323637", "#FF454B4C", "#FF5C6162", "#FF727878",
                    "#FF000000", "#FF1D1D1D", "#FF252525", "#FF3C3C3C", "#FF525252", "#FF6B6B6B"
                };
            }
            if (hueIndex == 1) // Gray
            {
                return new string[]
                {
                    "#FF141618", "#FF262A2E", "#FF3C4248", "#FF525A62", "#FF6C757E", "#FF87919B",
                    "#FF111315", "#FF202427", "#FF34393E", "#FF484F55", "#FF606870", "#FF79828C",
                    "#FF000000", "#FF181818", "#FF282828", "#FF3E3E3E", "#FF565656", "#FF707070"
                };
            }

            var baseColor = (Color)ColorConverter.ConvertFromString(BaseHueList[hueIndex]);
            RgbToHsl(baseColor, out double h, out double s, out double l);

            string[] shades = new string[18];
            // Row 1: Deep rich tones (L: 0.12 .. 0.32)
            double[] r1Lightness = { 0.12, 0.16, 0.20, 0.24, 0.28, 0.32 };
            for (int i = 0; i < 6; i++)
            {
                shades[i] = HslToHex(h, Math.Min(1.0, s * 0.90), r1Lightness[i]);
            }
            // Row 2: Midnight muted tones (L: 0.09 .. 0.26, S: 0.60)
            double[] r2Lightness = { 0.09, 0.12, 0.15, 0.19, 0.23, 0.26 };
            for (int i = 0; i < 6; i++)
            {
                shades[6 + i] = HslToHex(h, s * 0.60, r2Lightness[i]);
            }
            // Row 3: Dark shadow tones down to near-black (L: 0.04 .. 0.20, S: 0.45)
            double[] r3Lightness = { 0.04, 0.07, 0.10, 0.13, 0.17, 0.20 };
            for (int i = 0; i < 6; i++)
            {
                shades[12 + i] = HslToHex(h, s * 0.45, r3Lightness[i]);
            }
            return shades;
        }

        private void InitializeThemeColorMatrix()
        {
            _selectedLineHex = string.IsNullOrEmpty(ThemeManager.CurrentTheme.LineColor) ? "#FF602FCE" : ThemeManager.CurrentTheme.LineColor;

            // Determine active hue index
            _selectedHueIndex = 14; // Default to Indigo / Violet
            for (int h = 0; h < BaseHueList.Length; h++)
            {
                var shades = GetShadesForHue(h);
                if (Array.Exists(shades, s => string.Equals(s, _selectedLineHex, StringComparison.OrdinalIgnoreCase)))
                {
                    _selectedHueIndex = h;
                    break;
                }
            }

            // Build bottom hue strip (18 colors)
            HueSliderPanel.Children.Clear();
            _hueBorders.Clear();

            for (int i = 0; i < BaseHueList.Length; i++)
            {
                bool isSel = (i == _selectedHueIndex);
                var border = new Border
                {
                    Width = isSel ? 16 : 15,
                    Height = isSel ? 26 : 16,
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(BaseHueList[i])),
                    Margin = (i == 1) ? new Thickness(0, 0, 3, 0) : new Thickness(0, 0, 1, 0),
                    Cursor = Cursors.Hand,
                    BorderBrush = isSel ? Brushes.White : Brushes.Transparent,
                    BorderThickness = isSel ? new Thickness(1.5) : new Thickness(0)
                };

                int hueIdx = i;
                border.MouseLeftButtonUp += (s, e) =>
                {
                    SelectBaseHue(hueIdx);
                };

                _hueBorders.Add(border);
                HueSliderPanel.Children.Add(border);
            }

            // Populate 2x6 shades grid for the active hue
            PopulateThemeShadesGrid(GetShadesForHue(_selectedHueIndex));
        }

        private void SelectBaseHue(int idx)
        {
            _selectedHueIndex = idx;
            for (int i = 0; i < _hueBorders.Count; i++)
            {
                bool isSel = (i == idx);
                _hueBorders[i].Width = isSel ? 16 : 15;
                _hueBorders[i].Height = isSel ? 26 : 16;
                _hueBorders[i].BorderBrush = isSel ? Brushes.White : Brushes.Transparent;
                _hueBorders[i].BorderThickness = isSel ? new Thickness(1.5) : new Thickness(0);
            }

            var shades = GetShadesForHue(idx);
            if (!Array.Exists(shades, s => string.Equals(s, _selectedLineHex, StringComparison.OrdinalIgnoreCase)))
            {
                _selectedLineHex = shades[2];
                ThemeManager.CurrentTheme.LineColor = _selectedLineHex;
                ThemeManager.ApplyTheme(ThemeManager.CurrentTheme);
                SaveLayoutConfig();
            }

            PopulateThemeShadesGrid(shades);
        }

        private void PopulateThemeShadesGrid(string[] shades)
        {
            ThemeShadesGrid.Children.Clear();
            foreach (var shadeHex in shades)
            {
                bool isCurrent = string.Equals(shadeHex, _selectedLineHex, StringComparison.OrdinalIgnoreCase);
                var btn = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(shadeHex)),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0.5)
                };

                if (isCurrent)
                {
                    btn.Child = new TextBlock
                    {
                        Text = "✓",
                        FontSize = 22,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }

                string curHex = shadeHex;
                btn.MouseLeftButtonUp += (s, e) =>
                {
                    _selectedLineHex = curHex;
                    ThemeManager.CurrentTheme.LineColor = curHex;
                    ThemeManager.ApplyTheme(ThemeManager.CurrentTheme);
                    SaveLayoutConfig();
                    PopulateThemeShadesGrid(shades);
                };

                ThemeShadesGrid.Children.Add(btn);
            }
        }

        private string[] GetShadesForHue(int hueIndex)
        {
            if (hueIndex == 14) // Authentic Windows 8.1 screenshot palette
            {
                return new string[]
                {
                    "#FF2E00A3", "#FF4617B4", "#FF602FCE", "#FF7742E4", "#FF8E56FB", "#FFA46AFF",
                    "#FF391B79", "#FF4B2A89", "#FF613FA1", "#FF7651B6", "#FF8B64CC", "#FFA077E1"
                };
            }
            if (hueIndex == 0) // Charcoal
            {
                return new string[]
                {
                    "#FF1E1E1E", "#FF3B3B3B", "#FF585858", "#FF787878", "#FF9E9E9E", "#FFC8C8C8",
                    "#FF24282C", "#FF383E44", "#FF4E565E", "#FF66707A", "#FF828E9A", "#FFA2B0BD"
                };
            }
            if (hueIndex == 1) // Gray
            {
                return new string[]
                {
                    "#FF353535", "#FF505050", "#FF6E6E6E", "#FF8D8D8D", "#FFACACAC", "#FFD0D0D0",
                    "#FF3C4248", "#FF525A62", "#FF6C757E", "#FF87919B", "#FFA3ADB8", "#FFC1CAD4"
                };
            }

            var baseColor = (Color)ColorConverter.ConvertFromString(BaseHueList[hueIndex]);
            RgbToHsl(baseColor, out double h, out double s, out double l);

            string[] shades = new string[12];
            double[] r1Lightness = { 0.30, 0.38, 0.48, 0.58, 0.66, 0.73 };
            for (int i = 0; i < 6; i++)
            {
                shades[i] = HslToHex(h, Math.Min(1.0, s * 0.95), r1Lightness[i]);
            }
            double[] r2Lightness = { 0.26, 0.34, 0.43, 0.52, 0.61, 0.68 };
            for (int i = 0; i < 6; i++)
            {
                shades[6 + i] = HslToHex(h, s * 0.48, r2Lightness[i]);
            }
            return shades;
        }

        private static void RgbToHsl(Color c, out double h, out double s, out double l)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;
            l = (max + min) / 2.0;
            s = 0.0;
            h = 0.0;
            if (delta > 0.0001)
            {
                s = l <= 0.5 ? delta / (max + min) : delta / (2.0 - max - min);
                if (r == max) h = (g - b) / delta;
                else if (g == max) h = 2.0 + (b - r) / delta;
                else h = 4.0 + (r - g) / delta;
                h *= 60.0;
                if (h < 0.0) h += 360.0;
            }
        }

        private static string HslToHex(double h, double s, double l)
        {
            h /= 360.0;
            s = Math.Max(0.0, Math.Min(1.0, s));
            l = Math.Max(0.0, Math.Min(1.0, l));
            if (s == 0.0)
            {
                byte v = (byte)Math.Round(l * 255);
                return $"#FF{v:X2}{v:X2}{v:X2}";
            }
            double q = l < 0.5 ? l * (1.0 + s) : l + s - (l * s);
            double p = 2.0 * l - q;
            double HueToRgb(double pVal, double qVal, double t)
            {
                if (t < 0.0) t += 1.0;
                if (t > 1.0) t -= 1.0;
                if (t < 1.0 / 6.0) return pVal + (qVal - pVal) * 6.0 * t;
                if (t < 1.0 / 2.0) return qVal;
                if (t < 2.0 / 3.0) return pVal + (qVal - pVal) * (2.0 / 3.0 - t) * 6.0;
                return pVal;
            }
            byte r = (byte)Math.Round(HueToRgb(p, q, h + 1.0 / 3.0) * 255);
            byte g = (byte)Math.Round(HueToRgb(p, q, h) * 255);
            byte b = (byte)Math.Round(HueToRgb(p, q, h - 1.0 / 3.0) * 255);
            return $"#FF{r:X2}{g:X2}{b:X2}";
        }

        // ======================== BOTTOM METRO APPBAR ========================

        public void UpdateAppBarState()
        {
            // BottomAppBar removed per user request
        }

        private void DeselectAllTiles()
        {
            foreach (var tile in StartTiles) tile.IsSelected = false;
            UpdateAppBarState();
        }

        private void UnpinSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = StartTiles.Where(t => t.IsSelected).ToList();
            foreach (var tile in selected)
            {
                UnpinTile(tile);
            }
            UpdateAppBarState();
        }

        public void UnpinTile(TileModel tile)
        {
            StartTiles.Remove(tile);
            var controlToRemove = StartTilesCanvas.Children.OfType<LiveTileControl>().FirstOrDefault(c => c.DataContext == tile);
            if (controlToRemove != null)
            {
                StartTilesCanvas.Children.Remove(controlToRemove);
            }
            SaveLayoutConfig();
            UpdateAppBarState();
        }

        private void CycleTileSize_Click(object sender, RoutedEventArgs e)
        {
            var selected = StartTiles.Where(t => t.IsSelected).ToList();
            if (selected.Count == 0) return;

            foreach (var tile in selected)
            {
                tile.Size = tile.Size switch
                {
                    TileSize.Small => TileSize.Medium,
                    TileSize.Medium => TileSize.Wide,
                    TileSize.Wide => TileSize.Large,
                    TileSize.Large => TileSize.Small,
                    _ => TileSize.Medium
                };
            }
            SaveLayoutConfig();
        }

        private void ToggleLiveTile_Click(object sender, RoutedEventArgs e)
        {
            foreach (var tile in StartTiles.Where(t => t.IsSelected))
            {
                tile.IsLiveTileEnabled = !tile.IsLiveTileEnabled;
            }
            SaveLayoutConfig();
        }

        public void AnimateTilesEntrance()
        {
            var tiles = StartTilesCanvas.Children.OfType<LiveTileControl>()
                .OrderBy(t => Canvas.GetLeft(t))
                .ThenBy(t => Canvas.GetTop(t))
                .ToList();

            foreach (var tile in tiles)
            {
                double x = Canvas.GetLeft(tile);
                int colIndex = Math.Max(0, (int)Math.Floor(x / 130.0));
                int delayMs = Math.Min(320, colIndex * 35);
                tile.TriggerEntranceAnimation(delayMs);
            }
        }

        public void SaveLayoutConfig()
        {
            try
            {
                string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen");
                if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);
                string configPath = Path.Combine(configDir, "layout_config.json");

                if (_configWatcher != null) _configWatcher.EnableRaisingEvents = false;

                string version = "1.2";
                string bgPrimary = ThemeManager.CurrentTheme.BackgroundPrimary;
                string bgSecondary = ThemeManager.CurrentTheme.BackgroundSecondary;
                string accent = ThemeManager.CurrentTheme.AccentColor;
                string pattern = "waves_01";
                double globalSpeed = 1.0;

                if (File.Exists(configPath))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(configPath));
                        if (doc.RootElement.TryGetProperty("Version", out var v)) version = v.GetString() ?? version;
                        if (doc.RootElement.TryGetProperty("BackgroundPrimary", out var bp)) bgPrimary = bp.GetString() ?? bgPrimary;
                        if (doc.RootElement.TryGetProperty("BackgroundSecondary", out var bs)) bgSecondary = bs.GetString() ?? bgSecondary;
                        if (doc.RootElement.TryGetProperty("AccentColor", out var ac)) accent = ac.GetString() ?? accent;
                        if (doc.RootElement.TryGetProperty("BackgroundPattern", out var pat)) pattern = pat.GetString() ?? pattern;
                        if (doc.RootElement.TryGetProperty("GlobalAnimationSpeed", out var spd)) globalSpeed = spd.GetDouble();
                    }
                    catch { }
                }

                var rootObj = new
                {
                    Version = version,
                    Language = LocalizationManager.CurrentLanguage,
                    TargetMonitor = TargetMonitor,
                    BackgroundPrimary = bgPrimary,
                    BackgroundSecondary = bgSecondary,
                    AccentColor = accent,
                    BackgroundPattern = pattern,
                    GlobalAnimationSpeed = globalSpeed,
                    Tiles = StartTiles.ToList()
                };

                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                string json = System.Text.Json.JsonSerializer.Serialize(rootObj, options);
                File.WriteAllText(configPath, json);

                System.Threading.Tasks.Task.Delay(400).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (_configWatcher != null) _configWatcher.EnableRaisingEvents = true;
                    });
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SaveLayoutConfig] Error: {ex.Message}");
            }
        }

        public void OpenStudioEditor()
        {
            try
            {
                string studioPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Win8StartStudio.exe");
                if (!File.Exists(studioPath))
                {
                    studioPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WinMosaic", "Win8StartStudio.exe");
                }
                if (!File.Exists(studioPath))
                {
                    string scratchStudio = @"D:\Users\amir_\.gemini\antigravity\scratch\Win8StartStudio\bin\Release\net8.0-windows\Win8StartStudio.exe";
                    if (File.Exists(scratchStudio)) studioPath = scratchStudio;
                }
                if (File.Exists(studioPath))
                {
                    Process.Start(new ProcessStartInfo(studioPath) { UseShellExecute = true });
                }
            }
            catch { }
        }

        // ======================== ПОИСК (SEARCH CHARM) ========================

        private bool _isSelfUpdatingSearchInput = false;

        private readonly List<SearchResultItem> _systemSettings = new()
        {
            new SearchResultItem
            {
                Name = "Проводник",
                ExecutablePath = "explorer.exe",
                Category = "Приложения",
                IconImagePath = GetAssetPath("explorer_search_tile.png"),
                TileBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x82, 0x99))
            },
            new SearchResultItem
            {
                Name = "Параметры компьютера",
                ExecutablePath = "ms-settings:",
                Category = "Параметры",
                IconImagePath = GetAssetPath("pc_settings_tile.png"),
                TileBrush = new SolidColorBrush(Color.FromRgb(0x5C, 0x2D, 0x91)),
                IsSetting = true
            },
            new SearchResultItem
            {
                Name = "Параметры ввода с клавиатуры",
                ExecutablePath = "ms-settings:keyboard",
                Category = "Параметры",
                IconImagePath = GetAssetPath("settings_gear_tile.png"),
                TileBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28)),
                IsSetting = true
            },
            new SearchResultItem
            {
                Name = "Параметры даты и времени",
                ExecutablePath = "timedate.cpl",
                Category = "Параметры",
                IconImagePath = GetAssetPath("settings_gear_tile.png"),
                TileBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28)),
                IsSetting = true
            },
            new SearchResultItem
            {
                Name = "Параметры экрана",
                ExecutablePath = "desk.cpl",
                Category = "Параметры",
                IconImagePath = GetAssetPath("settings_gear_tile.png"),
                TileBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28)),
                IsSetting = true
            },
            new SearchResultItem
            {
                Name = "Параметры звука",
                ExecutablePath = "mmsys.cpl",
                Category = "Параметры",
                IconImagePath = GetAssetPath("settings_gear_tile.png"),
                TileBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28)),
                IsSetting = true
            },
            new SearchResultItem
            {
                Name = "Параметры мыши",
                ExecutablePath = "main.cpl",
                Category = "Параметры",
                IconImagePath = GetAssetPath("settings_gear_tile.png"),
                TileBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28)),
                IsSetting = true
            },
            new SearchResultItem
            {
                Name = "Параметры сети и Интернет",
                ExecutablePath = "ms-settings:network",
                Category = "Параметры",
                IconImagePath = GetAssetPath("settings_gear_tile.png"),
                TileBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28)),
                IsSetting = true
            },
            new SearchResultItem
            {
                Name = "Параметры электропитания",
                ExecutablePath = "powercfg.cpl",
                Category = "Параметры",
                IconImagePath = GetAssetPath("settings_gear_tile.png"),
                TileBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28)),
                IsSetting = true
            },
            new SearchResultItem
            {
                Name = "Панель управления",
                ExecutablePath = "control.exe",
                Category = "Параметры",
                IconImagePath = GetAssetPath("settings_gear_tile.png"),
                TileBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28)),
                IsSetting = true
            },
            new SearchResultItem
            {
                Name = "Диспетчер задач",
                ExecutablePath = "taskmgr.exe",
                Category = "Приложения",
                IconGlyph = "📊",
                TileBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7))
            },
            new SearchResultItem
            {
                Name = "Командная строка",
                ExecutablePath = "cmd.exe",
                Category = "Приложения",
                IconGlyph = ">_",
                TileBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E))
            },
            new SearchResultItem
            {
                Name = "Windows PowerShell",
                ExecutablePath = "powershell.exe",
                Category = "Приложения",
                IconGlyph = ">_",
                TileBrush = new SolidColorBrush(Color.FromRgb(0x01, 0x24, 0x56))
            },
            new SearchResultItem
            {
                Name = "Блокнот",
                ExecutablePath = "notepad.exe",
                Category = "Приложения",
                IconGlyph = "📝",
                TileBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7))
            },
            new SearchResultItem
            {
                Name = "Калькулятор",
                ExecutablePath = "calc.exe",
                Category = "Приложения",
                IconGlyph = "🧮",
                TileBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7))
            }
        };

        private void SearchCharmToggle_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyouts();
            if (_isSearchCharmOpen) CloseSearchCharm();
            else OpenSearchCharm();
        }

        public void OpenSearchCharm(string initialText = "")
        {
            if (_isSearchCharmOpen)
            {
                if (!string.IsNullOrEmpty(initialText))
                {
                    SearchQueryInput.Text = initialText;
                    SearchQueryInput.CaretIndex = initialText.Length;
                }
                SearchQueryInput.Focus();
                return;
            }

            _isSearchCharmOpen = true;
            ClosePersonalize();
            CloseAllFlyouts();

            var anim = new DoubleAnimation(0, -340, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            SearchCharmTranslate.BeginAnimation(TranslateTransform.XProperty, anim);

            if (!string.IsNullOrEmpty(initialText))
            {
                SearchQueryInput.Text = initialText;
                SearchQueryInput.CaretIndex = initialText.Length;
            }
            SearchQueryInput.Focus();
        }

        public void CloseSearchCharm()
        {
            if (!_isSearchCharmOpen) return;
            _isSearchCharmOpen = false;

            var anim = new DoubleAnimation(-340, 0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            SearchCharmTranslate.BeginAnimation(TranslateTransform.XProperty, anim);

            _isSelfUpdatingSearchInput = true;
            SearchQueryInput.Text = string.Empty;
            _isSelfUpdatingSearchInput = false;

            SearchQueryPrefixGhost.Text = string.Empty;
            SearchAutocompleteText.Text = string.Empty;
            SearchAutocompleteBadge.Visibility = Visibility.Collapsed;

            SearchResultsListBox.ItemsSource = null;
            SearchDivider.Visibility = Visibility.Collapsed;
            SearchQuerySuggestionButton.Visibility = Visibility.Collapsed;
        }

        private void SearchScopeButton_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1C)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Foreground = Brushes.White,
                FontFamily = (FontFamily)FindResource("SegoeRegular"),
                FontSize = 14
            };

            string[] scopes = { "Везде", "Параметры", "Файлы", "Веб-изображения", "Веб-видео" };
            foreach (var scope in scopes)
            {
                var mi = new MenuItem
                {
                    Header = scope,
                    Foreground = Brushes.White,
                    Background = Brushes.Transparent,
                    Padding = new Thickness(14, 6, 24, 6)
                };
                mi.Click += (s, ev) =>
                {
                    SearchScopeText.Text = scope;
                    PerformSearch(SearchQueryInput.Text, triggerAutocomplete: false);
                };
                menu.Items.Add(mi);
            }

            menu.PlacementTarget = SearchScopeButton;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void SearchQueryInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSelfUpdatingSearchInput) return;
            PerformSearch(SearchQueryInput.Text, triggerAutocomplete: true);
        }

        public void PerformSearch(string rawQuery, bool triggerAutocomplete = true)
        {
            if (string.IsNullOrWhiteSpace(rawQuery))
            {
                SearchQueryPrefixGhost.Text = string.Empty;
                SearchAutocompleteText.Text = string.Empty;
                SearchAutocompleteBadge.Visibility = Visibility.Collapsed;

                SearchResultsListBox.ItemsSource = null;
                SearchDivider.Visibility = Visibility.Collapsed;
                SearchQuerySuggestionButton.Visibility = Visibility.Collapsed;
                return;
            }

            string query = rawQuery.Trim();
            string scope = SearchScopeText?.Text ?? "Везде";

            var pool = new List<SearchResultItem>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Системные параметры и служебные приложения
            foreach (var sys in _systemSettings)
            {
                if (scope == "Параметры" && !sys.IsSetting) continue;
                if (scope == "Файлы") continue;

                if (seenNames.Add(sys.Name))
                {
                    pool.Add(sys);
                }
            }

            // 2. Установленные приложения системы
            if (scope != "Параметры" && scope != "Файлы")
            {
                foreach (var app in AllAppsViewItems.Where(a => a.IsApp && !string.IsNullOrWhiteSpace(a.Name)))
                {
                    if (seenNames.Add(app.Name))
                    {
                        pool.Add(new SearchResultItem
                        {
                            Name = app.Name,
                            ExecutablePath = app.ExecutablePath,
                            Category = "Приложения",
                            IconImagePath = app.IconImagePath,
                            IconGlyph = app.IconGlyph,
                            IconVectorPath = app.IconVectorPath,
                            TileBrush = app.TileBrush
                        });
                    }
                }
            }

            // Ранжирование:
            // 1. Точные совпадения с префикса (начинается на query)
            // 2. Совпадения по началу любого отдельного слова
            // 3. Содержит подстроку
            var prefixMatches = new List<SearchResultItem>();
            var wordMatches = new List<SearchResultItem>();
            var substringMatches = new List<SearchResultItem>();

            foreach (var item in pool)
            {
                if (item.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                {
                    prefixMatches.Add(item);
                }
                else if (item.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                  .Any(w => w.StartsWith(query, StringComparison.OrdinalIgnoreCase)))
                {
                    wordMatches.Add(item);
                }
                else if (item.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    substringMatches.Add(item);
                }
            }

            var results = new List<SearchResultItem>();
            results.AddRange(prefixMatches);
            results.AddRange(wordMatches);
            results.AddRange(substringMatches);

            SearchResultsListBox.ItemsSource = results;

            if (results.Count > 0)
            {
                SearchResultsListBox.SelectedIndex = 0;

                // Аутентичный инлайн-автокомплит Windows 8.1
                if (triggerAutocomplete && prefixMatches.Count > 0)
                {
                    var topMatch = prefixMatches[0];
                    if (topMatch.Name.Length > rawQuery.Length)
                    {
                        SearchQueryPrefixGhost.Text = rawQuery;
                        SearchAutocompleteText.Text = topMatch.Name.Substring(rawQuery.Length);
                        SearchAutocompleteBadge.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        SearchAutocompleteBadge.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    SearchAutocompleteBadge.Visibility = Visibility.Collapsed;
                }

                SearchDivider.Visibility = Visibility.Visible;
                SearchQuerySuggestionButton.Visibility = Visibility.Visible;
                SearchQuerySuggestionText.Text = query;
            }
            else
            {
                SearchAutocompleteBadge.Visibility = Visibility.Collapsed;
                SearchDivider.Visibility = Visibility.Collapsed;
                SearchQuerySuggestionButton.Visibility = Visibility.Collapsed;
            }
        }

        private void SearchQueryInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (SearchResultsListBox.SelectedItem is SearchResultItem selectedItem)
                {
                    LaunchSearchResult(selectedItem);
                }
                else if (SearchResultsListBox.Items.Count > 0 && SearchResultsListBox.Items[0] is SearchResultItem firstItem)
                {
                    LaunchSearchResult(firstItem);
                }
                else if (!string.IsNullOrWhiteSpace(SearchQueryInput.Text))
                {
                    LaunchSearchResult(new SearchResultItem { ExecutablePath = SearchQueryInput.Text });
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (SearchResultsListBox.Items.Count > 0)
                {
                    int nextIndex = SearchResultsListBox.SelectedIndex + 1;
                    if (nextIndex < SearchResultsListBox.Items.Count)
                    {
                        SearchResultsListBox.SelectedIndex = nextIndex;
                        SearchResultsListBox.ScrollIntoView(SearchResultsListBox.SelectedItem);
                    }
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (SearchResultsListBox.Items.Count > 0)
                {
                    int prevIndex = SearchResultsListBox.SelectedIndex - 1;
                    if (prevIndex >= 0)
                    {
                        SearchResultsListBox.SelectedIndex = prevIndex;
                        SearchResultsListBox.ScrollIntoView(SearchResultsListBox.SelectedItem);
                    }
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CloseSearchCharm();
                e.Handled = true;
            }
            else if (e.Key == Key.Tab || e.Key == Key.Right)
            {
                if (SearchAutocompleteBadge.Visibility == Visibility.Visible && !string.IsNullOrEmpty(SearchAutocompleteText.Text))
                {
                    _isSelfUpdatingSearchInput = true;
                    SearchQueryInput.Text = SearchQueryPrefixGhost.Text + SearchAutocompleteText.Text;
                    SearchQueryInput.CaretIndex = SearchQueryInput.Text.Length;
                    _isSelfUpdatingSearchInput = false;
                    SearchAutocompleteBadge.Visibility = Visibility.Collapsed;
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Back)
            {
                if (SearchAutocompleteBadge.Visibility == Visibility.Visible)
                {
                    SearchAutocompleteBadge.Visibility = Visibility.Collapsed;
                    SearchAutocompleteText.Text = string.Empty;
                }
            }
        }

        private void SearchResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Сохраняем акцентное выделение на строке
        }

        private void SearchResultsListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (SearchResultsListBox.SelectedItem is SearchResultItem item)
            {
                LaunchSearchResult(item);
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (SearchResultsListBox.SelectedItem is SearchResultItem item)
            {
                LaunchSearchResult(item);
            }
            else if (SearchResultsListBox.Items.Count > 0 && SearchResultsListBox.Items[0] is SearchResultItem firstItem)
            {
                LaunchSearchResult(firstItem);
            }
            else if (!string.IsNullOrWhiteSpace(SearchQueryInput.Text))
            {
                LaunchSearchResult(new SearchResultItem { ExecutablePath = SearchQueryInput.Text });
            }
        }

        private void SearchQuerySuggestion_Click(object sender, RoutedEventArgs e)
        {
            string q = SearchQuerySuggestionText.Text.Trim();
            if (!string.IsNullOrEmpty(q))
            {
                CloseScreenAnimated();
                try
                {
                    Process.Start(new ProcessStartInfo($"https://www.bing.com/search?q={Uri.EscapeDataString(q)}") { UseShellExecute = true });
                }
                catch { }
            }
        }

        private void LaunchSearchResult(SearchResultItem item)
        {
            CloseScreenAnimated();
            try
            {
                if (string.IsNullOrWhiteSpace(item.ExecutablePath))
                {
                    if (!string.IsNullOrWhiteSpace(SearchQueryInput.Text))
                    {
                        Process.Start(new ProcessStartInfo($"https://www.bing.com/search?q={Uri.EscapeDataString(SearchQueryInput.Text)}") { UseShellExecute = true });
                    }
                    return;
                }

                Process.Start(new ProcessStartInfo(item.ExecutablePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LaunchSearchResult] Error: {ex.Message}");
            }
        }

        // ======================== МЕНЮ ПОЛЬЗОВАТЕЛЯ И ПИТАНИЯ ========================

        private void UserButton_Click(object sender, RoutedEventArgs e)
        {
            PowerFlyout.Visibility = Visibility.Collapsed;
            if (UserFlyout.Visibility == Visibility.Visible)
            {
                UserFlyout.Visibility = Visibility.Collapsed;
            }
            else
            {
                PositionUserFlyout();
                UserFlyout.Visibility = Visibility.Visible;
            }
        }

        private void PowerButton_Click(object sender, RoutedEventArgs e)
        {
            UserFlyout.Visibility = Visibility.Collapsed;
            if (PowerFlyout.Visibility == Visibility.Visible)
            {
                PowerFlyout.Visibility = Visibility.Collapsed;
            }
            else
            {
                PositionPowerFlyout();
                PowerFlyout.Visibility = Visibility.Visible;
            }
        }

        private void PositionUserFlyout()
        {
            if (UserFlyout == null || RootLayout == null) return;

            FrameworkElement anchor = (FrameworkElement)UserAvatarBorder ?? UserButtonControl;
            if (anchor == null) return;

            anchor.UpdateLayout();
            RootLayout.UpdateLayout();

            double anchorW = anchor.ActualWidth > 0 ? anchor.ActualWidth : 38.0;
            double anchorH = anchor.ActualHeight > 0 ? anchor.ActualHeight : 38.0;

            Point pt = anchor.TranslatePoint(new Point(anchorW, anchorH), RootLayout);
            double top = pt.Y + 6.0;
            double right = Math.Max(0, RootLayout.ActualWidth - pt.X);

            UserFlyout.HorizontalAlignment = HorizontalAlignment.Right;
            UserFlyout.VerticalAlignment = VerticalAlignment.Top;
            UserFlyout.Margin = new Thickness(0, top, right, 0);
        }

        private void PositionPowerFlyout()
        {
            if (PowerButtonControl == null || PowerFlyout == null || RootLayout == null) return;

            PowerButtonControl.UpdateLayout();
            RootLayout.UpdateLayout();

            double btnW = PowerButtonControl.ActualWidth > 0 ? PowerButtonControl.ActualWidth : 38.0;
            double btnH = PowerButtonControl.ActualHeight > 0 ? PowerButtonControl.ActualHeight : 38.0;

            Point pt = PowerButtonControl.TranslatePoint(new Point(btnW, btnH), RootLayout);
            double top = pt.Y + 6.0;
            double right = Math.Max(0, RootLayout.ActualWidth - pt.X);

            PowerFlyout.HorizontalAlignment = HorizontalAlignment.Right;
            PowerFlyout.VerticalAlignment = VerticalAlignment.Top;
            PowerFlyout.Margin = new Thickness(0, top, right, 0);
        }

        private void RootLayout_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (UserFlyout != null && UserFlyout.Visibility == Visibility.Visible)
            {
                if (e.OriginalSource is DependencyObject dep)
                {
                    if (!IsDescendantOf(dep, UserFlyout) && !IsDescendantOf(dep, UserButtonControl))
                    {
                        UserFlyout.Visibility = Visibility.Collapsed;
                    }
                }
            }

            if (PowerFlyout != null && PowerFlyout.Visibility == Visibility.Visible)
            {
                if (e.OriginalSource is DependencyObject dep)
                {
                    if (!IsDescendantOf(dep, PowerFlyout) && !IsDescendantOf(dep, PowerButtonControl))
                    {
                        PowerFlyout.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private static bool IsDescendantOf(DependencyObject? node, DependencyObject target)
        {
            while (node != null)
            {
                if (node == target) return true;
                if (node is Visual || node is System.Windows.Media.Media3D.Visual3D)
                {
                    node = VisualTreeHelper.GetParent(node);
                }
                else
                {
                    node = LogicalTreeHelper.GetParent(node);
                }
            }
            return false;
        }

        private void CloseAllFlyouts()
        {
            if (UserFlyout != null) UserFlyout.Visibility = Visibility.Collapsed;
            if (PowerFlyout != null) PowerFlyout.Visibility = Visibility.Collapsed;
            ExitGroupNamingMode();
        }

        private void ChangeAvatar_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyouts();
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:yourinfo") { UseShellExecute = true });
            }
            catch
            {
                try { Process.Start(new ProcessStartInfo("control.exe", "/name Microsoft.UserAccounts") { UseShellExecute = true }); }
                catch { }
            }
        }

        private void LockSystem_Click(object sender, RoutedEventArgs e)
        {
            CloseScreenAnimated();
            LockWorkStation();
        }

        private void LogoffSystem_Click(object sender, RoutedEventArgs e)
        {
            CloseScreenAnimated();
            ExitWindowsEx(0, 0);
        }

        private void SleepSystem_Click(object sender, RoutedEventArgs e)
        {
            CloseScreenAnimated();
            SetSuspendState(false, true, true);
        }

        private void ShutdownSystem_Click(object sender, RoutedEventArgs e)
        {
            CloseScreenAnimated();
            Process.Start("shutdown", "/s /t 0");
        }

        private void RestartSystem_Click(object sender, RoutedEventArgs e)
        {
            CloseScreenAnimated();
            Process.Start("shutdown", "/r /t 0");
        }

        // ======================== ДЕЙСТВИЯ ИЗ СПИСКА ВСЕХ ПРИЛОЖЕНИЙ ========================

        private void AppInventoryItem_Click(object sender, RoutedEventArgs e)
        {
            string? execPath = null;
            if (sender is FrameworkElement fe)
            {
                if (fe.DataContext is AppsViewItem avi) execPath = avi.ExecutablePath;
                else if (fe.DataContext is InstalledAppItem iai) execPath = iai.ExecutablePath;
            }

            if (!string.IsNullOrEmpty(execPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(execPath) { UseShellExecute = true });
                    CloseScreenAnimated();
                    return;
                }
                catch
                {
                    try
                    {
                        if (!File.Exists(execPath))
                        {
                            string sys = Path.Combine(Environment.SystemDirectory, execPath);
                            if (File.Exists(sys))
                            {
                                Process.Start(new ProcessStartInfo(sys) { UseShellExecute = true });
                                CloseScreenAnimated();
                                return;
                            }
                            string win = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), execPath);
                            if (File.Exists(win))
                            {
                                Process.Start(new ProcessStartInfo(win) { UseShellExecute = true });
                                CloseScreenAnimated();
                                return;
                            }
                        }
                    }
                    catch { }
                }
                CloseScreenAnimated();
            }
        }

        private void PinAppToStart_Click(object sender, RoutedEventArgs e)
        {
            string? name = null;
            string? glyph = null;
            string? iconPath = null;
            Brush? brush = null;
            string? execPath = null;

            if (sender is FrameworkElement fe)
            {
                if (fe.DataContext is AppsViewItem avi)
                {
                    name = avi.Name;
                    glyph = avi.IconGlyph;
                    iconPath = avi.IconImagePath;
                    brush = avi.TileBrush;
                    execPath = avi.ExecutablePath;
                }
                else if (fe.DataContext is InstalledAppItem iai)
                {
                    name = iai.Name;
                    glyph = iai.IconGlyph;
                    iconPath = iai.IconImagePath;
                    brush = iai.TileBrush;
                    execPath = iai.ExecutablePath;
                }
            }

            if (!string.IsNullOrEmpty(name))
            {
                if (string.IsNullOrEmpty(iconPath)) iconPath = MetroIconResolver.ResolveIconPath(name);

                StartTiles.Add(new TileModel
                {
                    Title = name,
                    IconImagePath = iconPath,
                    IconGlyph = string.IsNullOrEmpty(iconPath) ? (glyph ?? "📁") : "",
                    BackgroundBrush = brush ?? Brushes.RoyalBlue,
                    ExecutablePath = execPath ?? "",
                    Size = TileSize.Medium
                });
                MessageBox.Show($"Приложение '{name}' закреплено на начальном экране.", "Metro UI");
            }
        }

        private void OpenAppLocation_Click(object sender, RoutedEventArgs e)
        {
            string? execPath = null;
            if (sender is FrameworkElement fe)
            {
                if (fe.DataContext is AppsViewItem avi) execPath = avi.ExecutablePath;
                else if (fe.DataContext is InstalledAppItem iai) execPath = iai.ExecutablePath;
            }

            if (!string.IsNullOrEmpty(execPath) && File.Exists(execPath))
            {
                Process.Start("explorer.exe", $"/select,\"{execPath}\"");
            }
        }

        private void UninstallApp_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("appwiz.cpl");
        }

        // ======================== ИНИЦИАЛИЗАЦИЯ ДАННЫХ ========================

        public static string GetAssetPath(string fileName)
        {
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", fileName);
            if (File.Exists(fullPath)) return fullPath;

            string localProgPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WinMosaic", "Assets", fileName);
            if (File.Exists(localProgPath)) return localProgPath;

            string localProgPathLegacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Win8StartScreen", "Assets", fileName);
            if (File.Exists(localProgPathLegacy)) return localProgPathLegacy;

            return fullPath;
        }

        private void InitializeStartScreenTiles()
        {
            MailTile = new TileModel
            {
                Title = "Mail",
                Size = TileSize.Wide,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                IconImagePath = MetroIconResolver.ResolveIconPath("Mail"),
                IconVectorPath = "M20,8L12,13L4,8V6L12,11L20,6M20,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V6C22,4.89 21.1,4 20,4Z",
                LiveText = string.Empty,
                IsLiveTileEnabled = false
            };

            CalendarTile = new TileModel
            {
                Title = "Calendar",
                Size = TileSize.Wide,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(104, 33, 122)),
                IconImagePath = MetroIconResolver.ResolveIconPath("Calendar"),
                IconVectorPath = "M19,4H18V2H16V4H8V2H6V4H5C3.89,4 3,4.89 3,6V20A2,2 0 0,0 5,22H19A2,2 0 0,0 21,20V6A2,2 0 0,0 19,4M19,20H5V9H19V20M19,8H5V6H19V8M7,11H12V16H7",
                LiveText = "14:00 - Встреча команды\n18:30 - Тренировка"
            };

            IETile = new TileModel
            {
                Title = "Internet Explorer",
                Size = TileSize.Medium,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(0, 164, 239)),
                IconImagePath = MetroIconResolver.ResolveIconPath("Internet Explorer"),
                IconGlyph = "🌐"
            };

            VideoTile = new TileModel
            {
                Title = "",
                Size = TileSize.Small,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(216, 0, 115)),
                IconImagePath = MetroIconResolver.ResolveIconPath("Videos"),
                IconVectorPath = "M18,3H6A3,3 0 0,0 3,6V18A3,3 0 0,0 6,21H18A3,3 0 0,0 21,18V6A3,3 0 0,0 18,3M10,16V8L16,12L10,16Z"
            };

            MusicTile = new TileModel
            {
                Title = "",
                Size = TileSize.Small,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(240, 150, 9)),
                IconImagePath = MetroIconResolver.ResolveIconPath("Music"),
                IconVectorPath = "M12,3A9,9 0 0,0 3,12V19A3,3 0 0,0 6,22H8V14H5V12A7,7 0 0,1 12,5A7,7 0 0,1 19,12V14H16V22H18A3,3 0 0,0 21,19V12A9,9 0 0,0 12,3Z"
            };

            GamesTile = new TileModel
            {
                Title = "",
                Size = TileSize.Small,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(51, 153, 51)),
                IconImagePath = MetroIconResolver.ResolveIconPath("Games"),
                IconVectorPath = "M19.5,6H4.5A2.5,2.5 0 0,0 2,8.5V15.5A2.5,2.5 0 0,0 4.5,18H6.9L8.4,14.5H15.6L17.1,18H19.5A2.5,2.5 0 0,0 22,15.5V8.5A2.5,2.5 0 0,0 19.5,6M7,13H5.5V11.5H4V10H5.5V8.5H7V10H8.5V11.5H7V13M16,10A1,1 0 1,1 17,9A1,1 0 0,1 16,10M18,12A1,1 0 1,1 19,11A1,1 0 0,1 18,12M18,8A1,1 0 1,1 19,7A1,1 0 0,1 18,8M20,10A1,1 0 1,1 21,9A1,1 0 0,1 20,10Z"
            };

            CameraTile = new TileModel
            {
                Title = "",
                Size = TileSize.Small,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(162, 0, 255)),
                IconImagePath = MetroIconResolver.ResolveIconPath("Camera"),
                IconVectorPath = "M4,4H7L9,2H15L17,4H20A2,2 0 0,1 22,6V18A2,2 0 0,1 20,20H4A2,2 0 0,1 2,18V6A2,2 0 0,1 4,4M12,7A5,5 0 0,0 7,12A5,5 0 0,0 12,17A5,5 0 0,0 17,12A5,5 0 0,0 12,7M12,9A3,3 0 0,1 15,12A3,3 0 0,1 12,15A3,3 0 0,1 9,12A3,3 0 0,1 12,9Z"
            };

            StoreTile = new TileModel
            {
                Title = "Store",
                Size = TileSize.Large,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(0, 168, 89)),
                IconImagePath = MetroIconResolver.ResolveIconPath("Windows Store"),
                IconVectorPath = "M19,6H16A4,4 0 0,0 12,2A4,4 0 0,0 8,6H5A2,2 0 0,0 3,8V20A2,2 0 0,0 5,22H19A2,2 0 0,0 21,20V8A2,2 0 0,0 19,6M12,4A2,2 0 0,1 14,6H10A2,2 0 0,1 12,4M19,20H5V8H19V20Z",
                LiveText = "Популярные игры и приложения доступны в Магазине Windows"
            };

            SportsTile = new TileModel
            {
                Title = "Sports",
                Size = TileSize.Wide,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(92, 45, 145)),
                IconImagePath = MetroIconResolver.ResolveIconPath("Sports"),
                IconVectorPath = "M19 5h-2V3H7v2H5c-1.1 0-2 .9-2 2v1c0 2.55 1.92 4.63 4.39 4.94A5.01 5.01 0 0 0 11 15.9V19H7v2h10v-2h-4v-3.1a5.01 5.01 0 0 0 3.61-2.96C19.08 12.63 21 10.55 21 8V7c0-1.1-.9-2-2-2zM5 8V7h2v3.82C5.84 10.4 5 9.3 5 8zm14 0c0 1.3-.84 2.4-2 2.82V7h2v1z",
                LiveText = "Роман Широков: Быть",
                LiveTemplate = "PhotoBanner",
                LiveImagePath = GetAssetPath("LiveTiles\\sports_shirokov.png"),
                LiveHeadline = "Роман Широков: Быть",
                LiveSubheadline = "капитаном — это...",
                LiveBannerColor = "#5C2D91",
                LiveBannerIcon = "M19 5h-2V3H7v2H5c-1.1 0-2 .9-2 2v1c0 2.55 1.92 4.63 4.39 4.94A5.01 5.01 0 0 0 11 15.9V19H7v2h10v-2h-4v-3.1a5.01 5.01 0 0 0 3.61-2.96C19.08 12.63 21 10.55 21 8V7c0-1.1-.9-2-2-2zM5 8V7h2v3.82C5.84 10.4 5 9.3 5 8zm14 0c0 1.3-.84 2.4-2 2.82V7h2v1z"
            };

            MoneyTile = new TileModel
            {
                Title = "Money",
                Size = TileSize.Wide,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(0, 138, 0)),
                BackgroundColor = "#FF008A00",
                IconImagePath = MetroIconResolver.ResolveIconPath("Money"),
                IconVectorPath = "M3.5,18.5L9.5,12.5L13.5,16.5L22,6.92L20.59,5.5L13.5,13.5L9.5,9.5L2,17L3.5,18.5M20,10H22V4H16V6H19.5",
                LiveText = "DOW 16 556,82",
                LiveTemplate = "FinanceQuotes",
                LiveQuote1Name = "DOW",
                LiveQuote1Value = "16 556,82",
                LiveQuote1Change = "-16,18",
                LiveQuote1IsUp = false,
                LiveQuote2Name = "FTSE 100",
                LiveQuote2Value = "6 655,20",
                LiveQuote2Change = "-3,84",
                LiveQuote2IsUp = false,
                LiveQuote3Name = "NIKKEI 225",
                LiveQuote3Value = "15 071,88",
                LiveQuote3Change = "+125,56",
                LiveQuote3IsUp = true
            };

            PeopleTile = new TileModel
            {
                Title = "People",
                Size = TileSize.Medium,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(224, 83, 34)),
                IconImagePath = MetroIconResolver.ResolveIconPath("People"),
                IconVectorPath = "M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z"
            };

            DesktopTile = new TileModel
            {
                Title = "Desktop",
                Size = TileSize.Medium,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(218, 165, 32)),
                IconImagePath = MetroIconResolver.ResolveIconPath("Desktop"),
                IconVectorPath = "M20,18c1.1,0 2,-0.9 2,-2V6c0,-1.1 -0.9,-2 -2,-2H4C2.9,4 2,4.9 2,6v10c0,1.1 0.9,2 2,2H0v2h24v-2h-4zM4,6h16v10H4V6z",
                ExecutablePath = "explorer.exe"
            };

            WeatherTile = new TileModel
            {
                Title = "Weather",
                Size = TileSize.Large,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                IconImagePath = MetroIconResolver.ResolveIconPath("Weather"),
                IconVectorPath = "M12,18C11.11,18 10.26,17.8 9.5,17.45C11.56,16.5 13,14.42 13,12C13,9.58 11.56,7.5 9.5,6.55C10.26,6.2 11.11,6 12,6A6,6 0 0,1 18,12A6,6 0 0,1 12,18M20,8.69V4H15.31L12,0.69L8.69,4H4V8.69L0.69,12L4,15.31V20H8.69L12,23.31L15.31,20H20V15.31L23.31,12L20,8.69Z",
                LiveTemplate = "Weather",
                LiveImagePath = GetAssetPath("LiveTiles\\weather_sky.png"),
                LiveCity = "Москва",
                LiveTemperature = "+21°",
                LiveCondition = "Преимущественно солнечно",
                LiveText = "Москва, Ясно\n+21°C | Влажность 45%"
            };

            PhotosTile = new TileModel
            {
                Title = "Photos",
                Size = TileSize.Medium,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(0, 130, 114)),
                IconImagePath = MetroIconResolver.ResolveIconPath("Photos"),
                IconVectorPath = "M8.5,13.5L11,16.5L14.5,12L19,18H5M21,19V5C21,3.89 20.1,3 19,3H5A2,2 0 0,0 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19Z"
            };

            OneNoteTile = new TileModel
            {
                Title = "OneNote",
                Size = TileSize.Medium,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(128, 57, 123)),
                IconImagePath = MetroIconResolver.ResolveIconPath("OneNote"),
                IconGlyph = "N"
            };

            NewsTile = new TileModel
            {
                Title = "News",
                Size = TileSize.Wide,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(162, 0, 37)),
                IconImagePath = MetroIconResolver.ResolveIconPath("News"),
                IconVectorPath = "M20,11H4V8H20M20,15H13V13H20M20,19H13V17H20M11,19H4V13H11M20,3H4C2.89,3 2,3.89 2,5V19A2,2 0 0,0 4,21H20A2,2 0 0,0 22,19V5C22,3.89 21.1,3 20,3Z",
                LiveText = "Последняя молитва",
                LiveTemplate = "PhotoBanner",
                LiveImagePath = GetAssetPath("LiveTiles\\news_pope.png"),
                LiveHeadline = "Последняя молитва",
                LiveBannerColor = "#A20025",
                LiveBannerIcon = "M20,11H4V8H20M20,15H13V13H20M20,19H13V17H20M11,19H4V13H11M20,3H4C2.89,3 2,3.89 2,5V19A2,2 0 0,0 4,21H20A2,2 0 0,0 22,19V5C22,3.89 21.1,3 20,3Z"
            };

            HelpTile = new TileModel
            {
                Title = "Help+Tips",
                Size = TileSize.Medium,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(224, 83, 34)),
                IconImagePath = MetroIconResolver.ResolveIconPath("Help"),
                IconVectorPath = "M11,18H13V16H11V18M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20M12,6A4,4 0 0,0 8,10H10A2,2 0 0,1 12,8A2,2 0 0,1 14,10C14,12 11,11.75 11,15H13C13,12.75 16,12.5 16,10A4,4 0 0,0 12,6Z"
            };

            OneDriveTile = new TileModel
            {
                Title = "OneDrive",
                Size = TileSize.Medium,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                IconImagePath = MetroIconResolver.ResolveIconPath("OneDrive"),
                IconVectorPath = "M19.35,10.04C18.67,6.59 15.64,4 12,4C9.11,4 6.6,5.64 5.35,8.04C2.34,8.36 0,10.91 0,14A6,6 0 0,0 6,20H19A5,5 0 0,0 24,15C24,12.36 21.95,10.22 19.35,10.04Z"
            };

            HealthTile = new TileModel
            {
                Title = "Health & Fitness",
                Size = TileSize.Medium,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(224, 83, 34)),
                IconImagePath = MetroIconResolver.ResolveIconPath("Health"),
                IconVectorPath = "M12,21.35L10.55,20.03C5.4,15.36 2,12.27 2,8.5C2,5.41 4.42,3 7.5,3C9.24,3 10.91,3.81 12,5.08C13.09,3.81 14.76,3 16.5,3C19.58,3 22,5.41 22,8.5C22,12.27 18.6,15.36 13.45,20.03L12,21.35Z"
            };

            FoodTile = new TileModel
            {
                Title = "Food & Drink",
                Size = TileSize.Medium,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(0, 130, 114)),
                IconImagePath = MetroIconResolver.ResolveIconPath("Food"),
                IconVectorPath = "M11,9H9V2H7V9H5V2H3V9C3,11.12 4.66,12.84 6.75,12.97V22H9.25V12.97C11.34,12.84 13,11.12 13,9V2H11V9M16,6V14H18.5V22H21V2C18.24,2 16,4.24 16,6Z",
                LiveTemplate = "PhotoBanner",
                LiveImagePath = GetAssetPath("LiveTiles\\food_pasta.png"),
                LiveHeadline = "Паста с брокколи",
                LiveBannerColor = "#008272",
                LiveBannerIcon = "M11,9H9V2H7V9H5V2H3V9C3,11.12 4.66,12.84 6.75,12.97V22H9.25V12.97C11.34,12.84 13,11.12 13,9V2H11V9M16,6V14H18.5V22H21V2C18.24,2 16,4.24 16,6Z"
            };

            MapsTile = new TileModel
            {
                Title = "Maps",
                Size = TileSize.Medium,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(104, 33, 122)),
                IconImagePath = MetroIconResolver.ResolveIconPath("Maps"),
                IconVectorPath = "M15,19L9,16.89L3.55,19.55C3.33,19.66 3.1,19.67 2.89,19.58C2.68,19.49 2.5,19.3 2.5,19.07V5.5C2.5,5.2 2.68,4.94 2.94,4.84L9,2.5L15,4.61L20.45,1.95C20.67,1.84 20.9,1.83 21.11,1.92C21.32,2.01 21.5,2.2 21.5,2.43V16C21.5,16.3 21.32,16.56 21.06,16.66L15,19Z"
            };

            ReadingListTile = new TileModel
            {
                Title = "Reading List",
                Size = TileSize.Medium,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(162, 0, 37)),
                IconImagePath = MetroIconResolver.ResolveIconPath("Reading List"),
                IconVectorPath = "M3,5H21V7H3V5M3,9H21V11H3V9M3,13H21V15H3V13M3,17H21V19H3V17Z"
            };

            PCSettingsTile = new TileModel
            {
                Title = "PC settings",
                Size = TileSize.Medium,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(104, 33, 122)),
                IconImagePath = MetroIconResolver.ResolveIconPath("Settings"),
                IconVectorPath = "M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L4.34,18.73C4.46,18.95 4.73,19.03 4.95,18.95L7.44,17.94C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.68 16.04,18.34 16.56,17.94L19.05,18.95C19.27,19.03 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z"
            };

            StartTiles = new ObservableCollection<TileModel>
            {
                MailTile, CalendarTile, IETile, VideoTile, MusicTile, GamesTile, CameraTile,
                StoreTile, SportsTile, MoneyTile, PeopleTile, DesktopTile, WeatherTile,
                PhotosTile, OneNoteTile, NewsTile, HelpTile, OneDriveTile, HealthTile,
                FoodTile, MapsTile, ReadingListTile, PCSettingsTile
            };
        }

        private string ResolveAppIcon(string name, string exePath, string? iconLocation = null, int iconIndex = 0)
        {
            string metro = MetroIconResolver.ResolveIconPath(name);
            if (!string.IsNullOrEmpty(metro) && File.Exists(metro)) return metro;

            string cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen", "AppIcons");
            if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);

            // 1. Попытка извлечь иконку из iconLocation (если задана в ярлыке .lnk)
            if (!string.IsNullOrEmpty(iconLocation) && File.Exists(iconLocation))
            {
                try
                {
                    string safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
                    string outPng = Path.Combine(cacheDir, safeName + ".png");
                    if (File.Exists(outPng)) return outPng;

                    using var ico = System.Drawing.Icon.ExtractAssociatedIcon(iconLocation);
                    if (ico != null)
                    {
                        using var bmp = ico.ToBitmap();
                        bmp.Save(outPng, System.Drawing.Imaging.ImageFormat.Png);
                        return outPng;
                    }
                }
                catch { }
            }

            // 2. Извлечение иконки из exePath или lnk-файла
            if (!string.IsNullOrEmpty(exePath))
            {
                try
                {
                    string target = exePath;
                    if (!File.Exists(target))
                    {
                        string sysTarget = Path.Combine(Environment.SystemDirectory, target);
                        if (File.Exists(sysTarget)) target = sysTarget;
                        else
                        {
                            string winTarget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), target);
                            if (File.Exists(winTarget)) target = winTarget;
                        }
                    }

                    if (File.Exists(target))
                    {
                        string safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
                        string outPng = Path.Combine(cacheDir, safeName + ".png");
                        if (File.Exists(outPng)) return outPng;

                        using var ico = System.Drawing.Icon.ExtractAssociatedIcon(target);
                        if (ico != null)
                        {
                            using var bmp = ico.ToBitmap();
                            bmp.Save(outPng, System.Drawing.Imaging.ImageFormat.Png);
                            return outPng;
                        }
                    }
                }
                catch { }
            }
            return "";
        }

        private class ScannedAppEntry
        {
            public string Name { get; set; } = string.Empty;
            public string ExePath { get; set; } = string.Empty;
            public string IconLocation { get; set; } = string.Empty;
            public int IconIndex { get; set; } = 0;
            public string Glyph { get; set; } = "📁";
            public Color Color { get; set; } = Color.FromRgb(0, 120, 215);
            public string Category { get; set; } = "Apps";
            public string? Subfolder { get; set; }
        }

        private void InitializeAppInventory()
        {
            if (_isAppInventoryScanning) return;
            _isAppInventoryScanning = true;
            try
            {
                MetroIconResolver.Initialize();

            // 1. Стандартные встроенные Metro/UWP и системные приложения Windows 8.1
            var defaultMetroApps = new List<(string Header, bool IsFolder, List<(string Name, string Exe, string Glyph, Color Color, string Cat)> Apps)>
            {
                ("Б", false, new()
                {
                    ("Будильники", "ms-clock:", "⏰", Color.FromRgb(216, 59, 1), "Tools")
                }),
                ("В", false, new()
                {
                    ("Видео", "mswindowsvideo:", "🎬", Color.FromRgb(180, 0, 158), "Media")
                }),
                ("Д", false, new()
                {
                    ("Документы", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "📁", Color.FromRgb(92, 92, 92), "System")
                }),
                ("З", false, new()
                {
                    ("Здоровье и фитнес", "binghealth:", "❤️", Color.FromRgb(232, 17, 35), "Lifestyle")
                }),
                ("И", false, new()
                {
                    ("Игры", "xbox:", "🎮", Color.FromRgb(16, 124, 65), "Games"),
                    ("Изображения", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "🖼️", Color.FromRgb(0, 130, 153), "Media")
                }),
                ("К", false, new()
                {
                    ("Календарь", "outlookcal:", "📅", Color.FromRgb(81, 51, 171), "Tools"),
                    ("Калькулятор", "calc.exe", "🔢", Color.FromRgb(16, 124, 65), "Tools"),
                    ("Камера", "microsoft.windows.camera:", "📷", Color.FromRgb(155, 78, 179), "Media"),
                    ("Карты", "bingmaps:", "🗺️", Color.FromRgb(116, 77, 169), "Navigation"),
                    ("Кулинария", "bingfood:", "🍴", Color.FromRgb(0, 130, 153), "Lifestyle")
                }),
                ("Л", false, new()
                {
                    ("Люди", "ms-people:", "👥", Color.FromRgb(216, 59, 1), "Communication")
                }),
                ("М", false, new()
                {
                    ("Магазин", "ms-windows-store:", "🛍️", Color.FromRgb(0, 138, 0), "Store"),
                    ("Музыка", "mswindowsmusic:", "🎵", Color.FromRgb(242, 80, 34), "Media")
                }),
                ("Н", false, new()
                {
                    ("Новости", "bingnews:", "📰", Color.FromRgb(209, 52, 56), "News")
                }),
                ("П", false, new()
                {
                    ("Параметры компьютера", "ms-settings:", "⚙️", Color.FromRgb(81, 51, 171), "System"),
                    ("Погода", "bingweather:", "☀️", Color.FromRgb(0, 120, 215), "Weather"),
                    ("Почта", "outlookmail:", "✉️", Color.FromRgb(0, 114, 198), "Communication"),
                    ("Путешествия Bing", "bingtravel:", "🧳", Color.FromRgb(0, 130, 153), "Lifestyle")
                }),
                ("Р", false, new()
                {
                    ("Рабочий стол", "explorer.exe", "🖥️", Color.FromRgb(59, 89, 152), "System")
                }),
                ("С", false, new()
                {
                    ("Сканер", "wfs.exe", "🖨️", Color.FromRgb(0, 120, 215), "Tools"),
                    ("Список для чтения", "readinglist:", "📚", Color.FromRgb(196, 62, 28), "Lifestyle"),
                    ("Спорт", "bingsports:", "🏆", Color.FromRgb(81, 51, 171), "Lifestyle"),
                    ("Справка+советы", "ms-help:", "❓", Color.FromRgb(216, 59, 1), "Help"),
                    ("Средство просмотра", "glance:", "👓", Color.FromRgb(202, 80, 16), "Tools"),
                    ("Студия звукозаписи", "soundrecorder.exe", "🎙️", Color.FromRgb(216, 59, 1), "Media")
                }),
                ("Ф", false, new()
                {
                    ("Финансы", "bingfinance:", "📈", Color.FromRgb(16, 124, 65), "Finance"),
                    ("Фотографии", "ms-photos:", "🌄", Color.FromRgb(0, 130, 153), "Media")
                }),
                ("Служебные — Windows", true, new()
                {
                    ("Windows PowerShell", "powershell.exe", "💻", Color.FromRgb(1, 36, 86), "System"),
                    ("Выполнить", "explorer.exe", "⚡", Color.FromRgb(0, 120, 215), "System"),
                    ("Диспетчер задач", "taskmgr.exe", "📊", Color.FromRgb(0, 130, 153), "System"),
                    ("Защитник Windows", "windowsdefender:", "🛡️", Color.FromRgb(92, 92, 92), "System"),
                    ("Командная строка", "cmd.exe", "⌨️", Color.FromRgb(12, 12, 12), "System"),
                    ("Панель управления", "control.exe", "⚙️", Color.FromRgb(0, 120, 215), "System"),
                    ("Проводник", "explorer.exe", "📁", Color.FromRgb(202, 80, 16), "System"),
                    ("Программы по умолчанию", "control.exe", "🔲", Color.FromRgb(0, 130, 153), "System"),
                    ("Справка и поддержка", "ms-help:", "❓", Color.FromRgb(0, 120, 215), "System"),
                    ("Средство переноса данных Windows", "migwiz.exe", "🔄", Color.FromRgb(0, 114, 198), "System"),
                    ("Этот компьютер", "explorer.exe", "🖥️", Color.FromRgb(0, 120, 215), "System")
                }),
                ("Спец. возможности", true, new()
                {
                    ("Распознавание речи Windows", "sapisvr.exe", "🎤", Color.FromRgb(70, 70, 70), "Accessibility"),
                    ("Экранная клавиатура", "osk.exe", "⌨️", Color.FromRgb(0, 114, 198), "Accessibility"),
                    ("Экранная лупа", "magnify.exe", "🔍", Color.FromRgb(0, 120, 215), "Accessibility"),
                    ("Экранный диктор", "narrator.exe", "🗣️", Color.FromRgb(0, 114, 198), "Accessibility")
                }),
                ("Стандартные — Windows", true, new()
                {
                    ("Paint", "mspaint.exe", "🎨", Color.FromRgb(0, 130, 114), "Accessories"),
                    ("WordPad", @"C:\Program Files\Windows NT\Accessories\wordpad.exe", "📝", Color.FromRgb(38, 114, 236), "Accessories"),
                    ("Блокнот", "notepad.exe", "📄", Color.FromRgb(0, 138, 0), "Accessories"),
                    ("Журнал Windows", "journal.exe", "📖", Color.FromRgb(0, 120, 215), "Accessories"),
                    ("Записки", "stikynot.exe", "📝", Color.FromRgb(242, 192, 0), "Accessories"),
                    ("Звукозапись", "soundrecorder.exe", "🎙️", Color.FromRgb(80, 80, 80), "Accessories"),
                    ("Калькулятор", "calc.exe", "🔢", Color.FromRgb(0, 130, 153), "Accessories"),
                    ("Ножницы", "snippingtool.exe", "✂️", Color.FromRgb(209, 52, 56), "Accessories"),
                    ("Панель математического ввода", "mip.exe", "📐", Color.FromRgb(0, 120, 215), "Accessories"),
                    ("Подключение к удаленному рабочему столу", "mstsc.exe", "🖥️", Color.FromRgb(0, 130, 153), "Accessories"),
                    ("Проигрыватель Windows Media", @"D:\Program Files (x86)\Windows Media Player\wmplayer.exe", "▶️", Color.FromRgb(232, 125, 13), "Accessories"),
                    ("Средство записи действий", "psr.exe", "⏺️", Color.FromRgb(0, 130, 153), "Accessories"),
                    ("Средство просмотра XPS", "xpsrchvw.exe", "📄", Color.FromRgb(0, 120, 215), "Accessories"),
                    ("Таблица символов", "charmap.exe", "🔣", Color.FromRgb(16, 124, 65), "Accessories"),
                    ("Факсы и сканирование", "wfs.exe", "📠", Color.FromRgb(0, 114, 198), "Accessories")
                })
            };

            // 2. Словарь известных акцентных цветов для популярных приложений
            var colorPalette = new[]
            {
                Color.FromRgb(0, 120, 215),
                Color.FromRgb(16, 124, 65),
                Color.FromRgb(232, 17, 35),
                Color.FromRgb(81, 51, 171),
                Color.FromRgb(242, 80, 34),
                Color.FromRgb(0, 150, 136),
                Color.FromRgb(216, 59, 1),
                Color.FromRgb(104, 33, 122),
                Color.FromRgb(0, 138, 0),
                Color.FromRgb(38, 114, 236),
                Color.FromRgb(202, 80, 16)
            };

            Color GetAppColor(string name)
            {
                string n = name.ToLowerInvariant();
                if (n.Contains("photoshop")) return Color.FromRgb(0, 30, 54);
                if (n.Contains("premiere")) return Color.FromRgb(0, 0, 91);
                if (n.Contains("blender")) return Color.FromRgb(232, 125, 13);
                if (n.Contains("brave")) return Color.FromRgb(255, 85, 0);
                if (n.Contains("discord")) return Color.FromRgb(88, 101, 242);
                if (n.Contains("telegram")) return Color.FromRgb(36, 161, 222);
                if (n.Contains("steam")) return Color.FromRgb(23, 26, 33);
                if (n.Contains("epic games")) return Color.FromRgb(47, 47, 47);
                if (n.Contains("visual studio code") || n.Contains("vs code")) return Color.FromRgb(0, 122, 204);
                if (n.Contains("visual studio")) return Color.FromRgb(92, 45, 145);
                if (n.Contains("skype")) return Color.FromRgb(0, 175, 240);
                if (n.Contains("qbittorrent")) return Color.FromRgb(47, 103, 146);
                if (n.Contains("winrar")) return Color.FromRgb(126, 56, 120);
                if (n.Contains("edge")) return Color.FromRgb(0, 120, 215);
                if (n.Contains("chrome")) return Color.FromRgb(234, 67, 53);
                if (n.Contains("firefox")) return Color.FromRgb(255, 113, 36);
                if (n.Contains("obs")) return Color.FromRgb(48, 46, 56);
                if (n.Contains("excel")) return Color.FromRgb(16, 124, 65);
                if (n.Contains("word")) return Color.FromRgb(24, 90, 189);
                if (n.Contains("powerpoint")) return Color.FromRgb(196, 62, 28);
                if (n.Contains("hydra")) return Color.FromRgb(30, 30, 46);
                if (n.Contains("hermes")) return Color.FromRgb(36, 59, 85);
                if (n.Contains("bandicam")) return Color.FromRgb(0, 150, 136);

                int hash = Math.Abs(name.GetHashCode());
                return colorPalette[hash % colorPalette.Length];
            }

            // 3. Динамическое сканирование всех установленных приложений из папок Start Menu
            var scannedShortcuts = new List<ScannedAppEntry>();
            var knownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string userStartDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs");
            string commonStartDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs");
            string cCommonStartDir = @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs";
            string dCommonStartDir = @"D:\ProgramData\Microsoft\Windows\Start Menu\Programs";

            var startRoots = new[] { userStartDir, commonStartDir, cCommonStartDir, dCommonStartDir }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            foreach (var root in startRoots)
            {
                if (!Directory.Exists(root)) continue;

                try
                {
                    var lnkFiles = Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories);
                    foreach (var lnk in lnkFiles)
                    {
                        try
                        {
                            string fileName = Path.GetFileNameWithoutExtension(lnk);
                            string lower = fileName.ToLowerInvariant();

                            // Фильтрация ненужных служебных ярлыков (деинсталляторы, хелпы, ридми, тулы и ссылки)
                            if (lower.Contains("uninstall") || lower.Contains("деинсталл") || lower.Contains("удалить") ||
                                lower.Contains("readme") || lower.Contains("read me") || lower.Contains("help") ||
                                lower.Contains("справка") || lower.Contains("documentation") || lower.Contains("license") ||
                                lower.Contains("release notes") || lower.Contains("website") || lower.Contains("url") ||
                                lower.Contains("setup") || lower.Contains("install") || lower == "desktop" ||
                                lower.Contains("manual") || lower.Contains("руководство") || lower.Contains("updater") ||
                                lower.Contains("troubleshoot") || lower.Contains("faq") || lower.Contains("support"))
                            {
                                continue;
                            }

                            // Определяем подпапку относительно Programs
                            string? subfolder = null;
                            string? dirPath = Path.GetDirectoryName(lnk);
                            if (!string.IsNullOrEmpty(dirPath) && !string.Equals(dirPath, root, StringComparison.OrdinalIgnoreCase))
                            {
                                string rel = Path.GetRelativePath(root, dirPath);
                                if (!string.IsNullOrEmpty(rel) && rel != ".")
                                {
                                    string[] parts = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                                    if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                                    {
                                        subfolder = parts[0].Trim();
                                    }
                                }
                            }

                            // Игнорируем стандартные папки Windows здесь, так как они уже представлены в defaultMetroApps
                            if (subfolder != null)
                            {
                                string sfLower = subfolder.ToLowerInvariant();
                                if (sfLower.Contains("стандартные") || sfLower.Contains("служебные") ||
                                    sfLower.Contains("accessories") || sfLower.Contains("system tools") ||
                                    sfLower.Contains("accessibility") || sfLower.Contains("спец. возможности") ||
                                    sfLower.Contains("администрирование") || sfLower.Contains("administrative tools"))
                                {
                                    continue;
                                }
                            }

                            // Извлечение реального пути через COM IShellLinkW
                            string exePath = lnk;
                            string iconLoc = "";
                            int iconIdx = 0;
                            var info = ShellShortcutResolver.ResolveShortcut(lnk);
                            if (info != null && !string.IsNullOrEmpty(info.TargetPath))
                            {
                                exePath = info.TargetPath;
                                iconLoc = info.IconLocation;
                                iconIdx = info.IconIndex;
                            }

                            // Фильтруем если target это uninstaller, setup, или неисполняемый документ/ссылка
                            string targetLower = Path.GetFileName(exePath).ToLowerInvariant();
                            string targetExt = Path.GetExtension(exePath).ToLowerInvariant();
                            if (targetLower.Contains("unins") || targetLower.Contains("setup") ||
                                targetExt == ".url" || targetExt == ".txt" || targetExt == ".pdf" ||
                                targetExt == ".html" || targetExt == ".htm" || targetExt == ".chm" ||
                                targetExt == ".ini" || targetExt == ".log")
                            {
                                continue;
                            }

                            scannedShortcuts.Add(new ScannedAppEntry
                            {
                                Name = fileName,
                                ExePath = exePath,
                                IconLocation = iconLoc,
                                IconIndex = iconIdx,
                                Color = GetAppColor(fileName),
                                Subfolder = subfolder
                            });
                        }
                        catch { }
                    }
                }
                catch { }
            }

            // 4. Построение групп приложений: Папки + Алфавитный порядок (A-Z, затем А-Я)
            var finalGroups = new List<(string Header, bool IsFolder, List<(string Name, string Exe, string IconLoc, int IconIdx, string Glyph, Color Color, string Cat)> Apps)>();

            // А. Сначала собираем стандартные группы и приложения из defaultMetroApps
            var folderGroupsMap = new Dictionary<string, List<(string Name, string Exe, string IconLoc, int IconIdx, string Glyph, Color Color, string Cat)>>(StringComparer.OrdinalIgnoreCase);
            var letterGroupsMap = new Dictionary<string, List<(string Name, string Exe, string IconLoc, int IconIdx, string Glyph, Color Color, string Cat)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var metroGrp in defaultMetroApps)
            {
                if (metroGrp.IsFolder)
                {
                    if (!folderGroupsMap.ContainsKey(metroGrp.Header))
                        folderGroupsMap[metroGrp.Header] = new();
                    foreach (var app in metroGrp.Apps)
                    {
                        folderGroupsMap[metroGrp.Header].Add((app.Name, app.Exe, "", 0, app.Glyph, app.Color, app.Cat));
                        knownNames.Add(app.Name);
                    }
                }
                else
                {
                    if (!letterGroupsMap.ContainsKey(metroGrp.Header))
                        letterGroupsMap[metroGrp.Header] = new();
                    foreach (var app in metroGrp.Apps)
                    {
                        letterGroupsMap[metroGrp.Header].Add((app.Name, app.Exe, "", 0, app.Glyph, app.Color, app.Cat));
                        knownNames.Add(app.Name);
                    }
                }
            }

            // Подсчет количества приложений в каждой подпапке
            var subfolderCounts = scannedShortcuts
                .Where(s => !string.IsNullOrEmpty(s.Subfolder))
                .GroupBy(s => s.Subfolder!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            // Б. Добавляем динамически найденные установленные программы (без дубликатов)
            foreach (var scanned in scannedShortcuts)
            {
                if (knownNames.Contains(scanned.Name)) continue;
                knownNames.Add(scanned.Name);

                // В Windows 8.1 приложения из папки пакетов (например "Microsoft Office" или "Image-Line")
                // группируются в секции папок, ТОЛЬКО если в подпапке несколько связанных утилит,
                // а не единичная программа вроде Blender или Brave.
                bool isFolderGroup = false;
                if (!string.IsNullOrEmpty(scanned.Subfolder))
                {
                    if (folderGroupsMap.ContainsKey(scanned.Subfolder))
                    {
                        isFolderGroup = true;
                    }
                    else if (subfolderCounts.TryGetValue(scanned.Subfolder, out int count) && count > 1)
                    {
                        // Если имя программы не повторяет название папки (например "A4Tech IM Magician", а не "Blender / Blender")
                        // или в папке несколько разных программ
                        isFolderGroup = true;
                    }
                }

                if (isFolderGroup && !string.IsNullOrEmpty(scanned.Subfolder))
                {
                    if (!folderGroupsMap.ContainsKey(scanned.Subfolder))
                        folderGroupsMap[scanned.Subfolder] = new();
                    folderGroupsMap[scanned.Subfolder].Add((scanned.Name, scanned.ExePath, scanned.IconLocation, scanned.IconIndex, scanned.Glyph, scanned.Color, scanned.Category));
                }
                else
                {
                    // Определяем первую букву
                    string headerLetter = "#";
                    if (!string.IsNullOrWhiteSpace(scanned.Name))
                    {
                        char firstChar = char.ToUpperInvariant(scanned.Name[0]);
                        if ((firstChar >= 'A' && firstChar <= 'Z') || (firstChar >= 'А' && firstChar <= 'Я'))
                        {
                            headerLetter = firstChar.ToString();
                        }
                    }

                    if (!letterGroupsMap.ContainsKey(headerLetter))
                        letterGroupsMap[headerLetter] = new();
                    letterGroupsMap[headerLetter].Add((scanned.Name, scanned.ExePath, scanned.IconLocation, scanned.IconIndex, scanned.Glyph, scanned.Color, scanned.Category));
                }
            }

            // В. Сортировка буквенных групп: сначала английский A-Z, затем русский А-Я, затем '#'
            var sortedLetters = letterGroupsMap.Keys.OrderBy(l =>
            {
                if (l.Length == 0) return 999;
                char c = l[0];
                if (c >= 'A' && c <= 'Z') return 100 + (c - 'A');
                if (c >= 'А' && c <= 'Я') return 200 + (c - 'А');
                return 300;
            }).ToList();

            foreach (var letter in sortedLetters)
            {
                var apps = letterGroupsMap[letter].OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                finalGroups.Add((letter, false, apps));
            }

            // Г. Сортировка папочных групп по алфавиту
            var sortedFolders = folderGroupsMap.Keys.OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase).ToList();
            foreach (var folder in sortedFolders)
            {
                var apps = folderGroupsMap[folder].OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                finalGroups.Add((folder, true, apps));
            }

            // 5. Заполнение AllAppsViewItems и AlphabetGroups
            AllAppsViewItems.Clear();
            AlphabetGroups.Clear();

            foreach (var grp in finalGroups)
            {
                // Заголовок секции
                AllAppsViewItems.Add(new AppsViewItem
                {
                    IsHeader = true,
                    IsLetterHeader = !grp.IsFolder,
                    IsFolderHeader = grp.IsFolder,
                    HeaderTitle = grp.Header
                });

                var alphabetGrp = new AppAlphabetGroup { Letter = grp.Header };

                foreach (var app in grp.Apps)
                {
                    string iconPath = ResolveAppIcon(app.Name, app.Exe, app.IconLoc, app.IconIdx);
                    string glyph = string.IsNullOrEmpty(iconPath) ? app.Glyph : string.Empty;
                    var appItem = new AppsViewItem
                    {
                        Name = app.Name,
                        ExecutablePath = app.Exe,
                        IconGlyph = glyph,
                        IconImagePath = iconPath,
                        TileBrush = new SolidColorBrush(app.Color),
                        Category = app.Cat
                    };
                    AllAppsViewItems.Add(appItem);

                    alphabetGrp.Apps.Add(new InstalledAppItem
                    {
                        Name = app.Name,
                        ExecutablePath = app.Exe,
                        IconGlyph = glyph,
                        IconImagePath = iconPath,
                        TileBrush = new SolidColorBrush(app.Color)
                    });
                }

                if (alphabetGrp.HasApps)
                {
                    AlphabetGroups.Add(alphabetGrp);
                }
            }

            DisplayedAppsViewItems.Clear();
            foreach (var it in AllAppsViewItems)
            {
                DisplayedAppsViewItems.Add(it);
            }

            AppsWrapItemsControl.ItemsSource = DisplayedAppsViewItems;
            PopulateAlphabetJumpMatrix();
            }
            finally
            {
                _isAppInventoryScanning = false;
            }
        }

        private void AppsListScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Height > 100 && AppsWrapItemsControl != null)
            {
                var wp = FindVisualChild<WrapPanel>(AppsWrapItemsControl);
                if (wp != null)
                {
                    wp.Height = e.NewSize.Height;
                }
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private void AppsSortButton_Click(object sender, RoutedEventArgs e)
        {
            if (AppsSortMenu != null)
            {
                AppsSortMenu.PlacementTarget = AppsSortButton;
                AppsSortMenu.IsOpen = true;
            }
        }

        private void SortOption_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                string header = mi.Header?.ToString() ?? "по имени";
                CurrentSortTextBlock.Text = header;
                foreach (var item in AppsSortMenu.Items.OfType<MenuItem>())
                {
                    item.IsChecked = (item == mi);
                }
                ApplyAppsSort(header);
            }
        }

        private void ApplyAppsSort(string sortType)
        {
            DisplayedAppsViewItems.Clear();
            foreach (var item in AllAppsViewItems)
            {
                DisplayedAppsViewItems.Add(item);
            }
        }

        private void AppsSearchInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterAppsView(AppsSearchInput.Text);
        }

        private void FilterAppsView(string query)
        {
            DisplayedAppsViewItems.Clear();
            if (string.IsNullOrWhiteSpace(query))
            {
                foreach (var item in AllAppsViewItems) DisplayedAppsViewItems.Add(item);
                return;
            }

            string q = query.Trim();
            string currentHeader = "";
            bool isFolder = false;
            bool isLetter = false;
            var matchingItems = new List<AppsViewItem>();

            foreach (var item in AllAppsViewItems)
            {
                if (item.IsHeader)
                {
                    currentHeader = item.HeaderTitle;
                    isFolder = item.IsFolderHeader;
                    isLetter = item.IsLetterHeader;
                }
                else if (item.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(currentHeader))
                    {
                        matchingItems.Add(new AppsViewItem
                        {
                            IsHeader = true,
                            IsLetterHeader = isLetter,
                            IsFolderHeader = isFolder,
                            HeaderTitle = currentHeader
                        });
                        currentHeader = "";
                    }
                    matchingItems.Add(item);
                }
            }

            foreach (var item in matchingItems)
            {
                DisplayedAppsViewItems.Add(item);
            }
        }

        // ======================== ДАННЫЕ ПОЛЬЗОВАТЕЛЯ WINDOWS (НИК И АВАТАРКА) ========================

        [DllImport("secur32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetUserNameEx(int nameFormat, StringBuilder userName, ref uint userNameSize);

        private void LoadWindowsUserInfo()
        {
            try
            {
                string displayName = GetUserDisplayName();
                if (UserNameTextBlock != null)
                {
                    UserNameTextBlock.Text = displayName;
                }

                string avatarPath = GetUserAvatarPath();
                if (!string.IsNullOrEmpty(avatarPath) && File.Exists(avatarPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(avatarPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 76;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    if (UserAvatarImage != null)
                    {
                        UserAvatarImage.Source = bitmap;
                        UserAvatarImage.Visibility = Visibility.Visible;
                    }
                    if (UserAvatarFallbackIcon != null)
                    {
                        UserAvatarFallbackIcon.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    if (UserAvatarImage != null) UserAvatarImage.Visibility = Visibility.Collapsed;
                    if (UserAvatarFallbackIcon != null) UserAvatarFallbackIcon.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                if (UserNameTextBlock != null) UserNameTextBlock.Text = Environment.UserName;
                if (UserAvatarImage != null) UserAvatarImage.Visibility = Visibility.Collapsed;
                if (UserAvatarFallbackIcon != null) UserAvatarFallbackIcon.Visibility = Visibility.Visible;
            }
        }

        private static string GetUserDisplayName()
        {
            try
            {
                var sb = new StringBuilder(256);
                uint size = (uint)sb.Capacity;
                // NameDisplay = 3 (e.g. "Amir Islamov")
                if (GetUserNameEx(3, sb, ref size) != 0 && sb.Length > 0)
                {
                    return sb.ToString();
                }
            }
            catch { }

            return !string.IsNullOrWhiteSpace(Environment.UserName) ? Environment.UserName : "User";
        }

        private static string GetUserAvatarPath()
        {
            try
            {
                string? sid = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value;
                if (!string.IsNullOrEmpty(sid))
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\AccountPicture\Users\{sid}");
                    if (key != null)
                    {
                        string[] preferred = { "Image192", "Image448", "Image96", "Image1080", "Image48", "Image32" };
                        foreach (var p in preferred)
                        {
                            var val = key.GetValue(p) as string;
                            if (!string.IsNullOrEmpty(val) && File.Exists(val))
                            {
                                return val;
                            }
                        }
                    }
                }
            }
            catch { }
            return "";
        }

        // ======================== WIN32 API ========================

        [DllImport("user32.dll")]
        private static extern bool LockWorkStation();

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool ExitWindowsEx(int flg, int rea);

        [DllImport("PowrProf.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern bool SetSuspendState(bool hiberate, bool forceCritical, bool disableWakeEvent);
    }
}
