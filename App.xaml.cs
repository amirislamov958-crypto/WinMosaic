using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Win8StartScreen
{
    public partial class App : System.Windows.Application
    {
        private GlobalKeyboardHook? _hook;
        private TaskbarHook? _taskbarHook;
        private MainWindow? _mainWindow;
        private NotifyIcon? _trayIcon;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                try
                {
                    string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen");
                    Directory.CreateDirectory(logDir);
                    File.AppendAllText(Path.Combine(logDir, "crash.log"), $"[{DateTime.Now}] {args.ExceptionObject}\n");
                }
                catch { }
            };

            DispatcherUnhandledException += (s, args) =>
            {
                try
                {
                    string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen");
                    Directory.CreateDirectory(logDir);
                    File.AppendAllText(Path.Combine(logDir, "crash.log"), $"[{DateTime.Now}] Dispatcher error: {args.Exception}\n");
                }
                catch { }
                args.Handled = true;
            };

            // Загрузка сохраненной темы
            ThemeManager.LoadTheme();

            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] OnStartup starting\n");

            try
            {
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Creating MainWindow...\n");
                _mainWindow = new MainWindow();
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] MainWindow instantiated successfully\n");
            }
            catch (Exception ex)
            {
                File.AppendAllText(Path.Combine(logDir, "crash.log"), $"[{DateTime.Now}] MainWindow Constructor Exception: {ex}\n");
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Exception: {ex.Message}\n");
                throw;
            }

            var cmdArgs = Environment.GetCommandLineArgs();
            File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] CommandLineArgs: {string.Join(" | ", cmdArgs)}\n");

            if (System.Linq.Enumerable.Any(cmdArgs, a => a.Equals("--screenshot-apps", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Found --screenshot-apps flag!\n");
                _mainWindow?.SwitchToAppsForDebugScreenshot();
                return;
            }

            if (System.Linq.Enumerable.Any(cmdArgs, a => a.Equals("--screenshot-hover", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Found --screenshot-hover flag!\n");
                _mainWindow?.SwitchToStartForHoverScreenshot();
                return;
            }

            if (System.Linq.Enumerable.Any(cmdArgs, a => a.Equals("--screenshot-start", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Found --screenshot-start flag!\n");
                _mainWindow?.SwitchToStartForStaticScreenshot();
                return;
            }

            if (System.Linq.Enumerable.Any(cmdArgs, a => a.Equals("--screenshot-live", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Found --screenshot-live flag!\n");
                _mainWindow?.SwitchToStartForLiveScreenshot();
                return;
            }

            if (System.Linq.Enumerable.Any(cmdArgs, a => a.Equals("--screenshot-user-flyout", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Found --screenshot-user-flyout flag!\n");
                _mainWindow?.SwitchToStartForUserFlyoutScreenshot();
                return;
            }

            if (System.Linq.Enumerable.Any(cmdArgs, a => a.Equals("--screenshot-group-naming", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Found --screenshot-group-naming flag!\n");
                _mainWindow?.SwitchToStartForGroupNamingScreenshot();
                return;
            }

            if (System.Linq.Enumerable.Any(cmdArgs, a => a.Equals("--screenshot-search", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Found --screenshot-search flag!\n");
                _mainWindow?.SwitchToSearchForDebugScreenshot();
                return;
            }

            if (System.Linq.Enumerable.Any(cmdArgs, a => a.Equals("--screenshot-settings", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Found --screenshot-settings flag!\n");
                _mainWindow?.SwitchToSettingsForDebugScreenshot("main");
                return;
            }

            if (System.Linq.Enumerable.Any(cmdArgs, a => a.Equals("--screenshot-settings-addtile", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Found --screenshot-settings-addtile flag!\n");
                _mainWindow?.SwitchToSettingsForDebugScreenshot("addtile");
                return;
            }

            if (System.Linq.Enumerable.Any(cmdArgs, a => a.Equals("--screenshot-settings-personalize", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Found --screenshot-settings-personalize flag!\n");
                _mainWindow?.SwitchToSettingsForDebugScreenshot("personalize");
                return;
            }

            if (System.Linq.Enumerable.Any(cmdArgs, a => a.Equals("--screenshot-settings-studio", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Found --screenshot-settings-studio flag!\n");
                _mainWindow?.SwitchToSettingsForDebugScreenshot("studio");
                return;
            }

            if (System.Linq.Enumerable.Any(cmdArgs, a => a.Equals("--screenshot-settings-monitors", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Found --screenshot-settings-monitors flag!\n");
                _mainWindow?.SwitchToSettingsForDebugScreenshot("monitors");
                return;
            }

            if (System.Linq.Enumerable.Any(cmdArgs, a => a.Equals("--screenshot-settings-language", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Found --screenshot-settings-language flag!\n");
                _mainWindow?.SwitchToSettingsForDebugScreenshot("language");
                return;
            }

            if (System.Linq.Enumerable.Any(cmdArgs, a => a.Equals("--screenshot-settings-founders", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Found --screenshot-settings-founders flag!\n");
                _mainWindow?.SwitchToSettingsForDebugScreenshot("founders");
                return;
            }

            if (System.Linq.Enumerable.Any(cmdArgs, a => a.Equals("--screenshot-settings-autostart", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Found --screenshot-settings-autostart flag!\n");
                _mainWindow?.SwitchToSettingsForDebugScreenshot("autostart");
                return;
            }

            if (System.Linq.Enumerable.Any(cmdArgs, a => a.Equals("--test-create-tile", StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Found --test-create-tile flag!\n");
                _mainWindow?.SwitchToSettingsForDebugScreenshot("addtile-create");
                return;
            }

            if (System.Linq.Enumerable.Any(cmdArgs, a => a.Equals("--screenshot-catalog", StringComparison.OrdinalIgnoreCase)))
            {
                var dlg = new Win8StartScreen.Views.MetroIconPickerDialog();
                dlg.Show();
                dlg.UpdateLayout();
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
                timer.Tick += (s, ev) =>
                {
                    timer.Stop();
                    int w = (int)dlg.ActualWidth;
                    int h = (int)dlg.ActualHeight;
                    if (w <= 0 || h <= 0) { w = 960; h = 650; }
                    var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(dlg);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(rtb));
                    string outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen", "Screenshots");
                    string brainDir = @"D:\Users\amir_\.gemini\antigravity\brain\261e44a7-c131-4bf3-bc4b-c8d690ed61a2\scratch";
                    if (Directory.Exists(brainDir)) outDir = brainDir;
                    Directory.CreateDirectory(outDir);
                    string path = Path.Combine(outDir, "metro_catalog_live.png");
                    using var fs = File.Create(path);
                    encoder.Save(fs);
                    System.Windows.Application.Current?.Shutdown();
                };
                timer.Start();
                return;
            }

            // Автоматическая регистрация автозагрузки в реестре при первом запуске
            AutostartHelper.EnsureAutostartRegistered();

            // Создаем иконку в системном трее
            System.Drawing.Icon? appIco = null;
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
                if (File.Exists(iconPath))
                {
                    appIco = new System.Drawing.Icon(iconPath);
                }
                else
                {
                    appIco = System.Drawing.Icon.ExtractAssociatedIcon(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "");
                }
            }
            catch { }

            _trayIcon = new NotifyIcon
            {
                Icon = appIco ?? SystemIcons.Application,
                Text = "WinMosaic",
                Visible = true
            };

            void RebuildTrayContextMenu()
            {
                var contextMenu = new ContextMenuStrip();
                contextMenu.Items.Add(LocalizationManager.Get("TrayOpenStart", "Открыть Пуск (Win)"), null, (s, a) => _mainWindow?.ToggleScreen());
                contextMenu.Items.Add(LocalizationManager.Get("TrayPersonalize", "Персонализация (Win+I)"), null, (s, a) => _mainWindow?.OpenPersonalizeDirectly());
                contextMenu.Items.Add(LocalizationManager.Get("TrayTileEditor", "Редактор плиток (Studio)"), null, (s, a) => _mainWindow?.OpenStudioEditor());
                contextMenu.Items.Add(new ToolStripSeparator());

                var autostartItem = new ToolStripMenuItem(LocalizationManager.Get("TrayAutostart", "Автозагрузка с Windows"))
                {
                    Checked = AutostartHelper.IsAutostartEnabled(),
                    CheckOnClick = true
                };
                autostartItem.Click += (s, a) =>
                {
                    AutostartHelper.SetAutostart(autostartItem.Checked);
                };
                contextMenu.Items.Add(autostartItem);

                var bootToStartItem = new ToolStripMenuItem(LocalizationManager.Get("TrayBootToStart", "Открывать «Пуск» сразу при включении ПК"))
                {
                    Checked = ThemeManager.CurrentTheme.LaunchStartScreenOnBoot,
                    CheckOnClick = true
                };
                bootToStartItem.Click += (s, a) =>
                {
                    ThemeManager.CurrentTheme.LaunchStartScreenOnBoot = bootToStartItem.Checked;
                    ThemeManager.SaveTheme();
                };
                contextMenu.Items.Add(bootToStartItem);

                contextMenu.Items.Add(new ToolStripSeparator());
                contextMenu.Items.Add(LocalizationManager.Get("TrayExit", "Выход"), null, (s, a) => ExitApplication());
                _trayIcon.ContextMenuStrip = contextMenu;
            }

            RebuildTrayContextMenu();
            LocalizationManager.LanguageChanged += () =>
            {
                Dispatcher.Invoke(() => RebuildTrayContextMenu());
            };
            _trayIcon.MouseClick += (s, a) =>
            {
                if (a.Button == MouseButtons.Left)
                {
                    _mainWindow?.ToggleScreen();
                }
            };

            // Инициализируем глобальный хук клавиатуры
            _hook = new GlobalKeyboardHook();
            _hook.WinKeyPressed += () =>
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, new Action(() =>
                {
                    if (_mainWindow == null) return;
                    var screen = _mainWindow.GetActiveOrConfiguredScreen();
                    _mainWindow.ToggleScreen(screen);
                }));
            };
            _hook.EscKeyPressed += () =>
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, new Action(() => _mainWindow?.CloseScreenAnimated()));
            };

            // Инициализируем глобальный хук клика на логотип Windows (кнопку Пуск на панели задач)
            _taskbarHook = new TaskbarHook();
            _taskbarHook.StartButtonClicked += (clickedScreen) =>
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, new Action(() =>
                {
                    if (_mainWindow == null) return;

                    // Если экран Пуск уже открыт
                    if (_mainWindow.CurrentState == ScreenState.Open)
                    {
                        // Если клик на том же мониторе, где открыт экран — закрываем (поведение Win 8.1 / Win 11)
                        if (_mainWindow.IsCurrentScreen(clickedScreen))
                        {
                            _mainWindow.CloseScreenAnimated();
                        }
                        else
                        {
                            // Если открыт на другом мониторе — переключаем на монитор, где кликнули
                            _mainWindow.OpenScreenAnimated(clickedScreen);
                        }
                    }
                    else
                    {
                        _mainWindow.OpenScreenAnimated(clickedScreen);
                    }
                }));
            };

            File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] Hooks & Tray initialized\n");

            // При старте системы приложение сразу работает в фоновом режиме (скрыто через DWM Cloak, готово к мгновенному открытию по клавише Win или кнопке Пуск)
            if (ThemeManager.CurrentTheme.LaunchStartScreenOnBoot)
            {
                // Если включена опция открывать экран сразу на весь экран:
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _mainWindow?.OpenScreenAnimated();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void ExitApplication()
        {
            _hook?.Dispose();
            _taskbarHook?.Dispose();
            _trayIcon?.Dispose();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen");
            File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] OnExit code {e.ApplicationExitCode}\n");
            _hook?.Dispose();
            _taskbarHook?.Dispose();
            _trayIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
