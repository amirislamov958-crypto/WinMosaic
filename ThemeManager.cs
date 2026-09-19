using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace Win8StartScreen
{
    public class UserThemeConfig
    {
        public string PatternId { get; set; } = "waves_01";
        public string BackgroundPrimary { get; set; } = "#FF1E023D";
        public string BackgroundSecondary { get; set; } = "#FF290153";
        public string AccentColor { get; set; } = "#FF0078D7";
        public string LineColor { get; set; } = "#FF602FCE";
        public string CustomWallpaperPath { get; set; } = "";
        public double CustomWallpaperDarkness { get; set; } = 0.45; // затемнение от 0.0 до 1.0 (по умолчанию черноватый оттенок 0.45)
        public bool LaunchStartScreenOnBoot { get; set; } = false; // По умолчанию тихо работает в фоновом режиме
    }

    public static class ThemeManager
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Win8StartScreen", "theme.json");

        public static UserThemeConfig CurrentTheme { get; private set; } = new();

        public static void LoadTheme()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    CurrentTheme = JsonSerializer.Deserialize<UserThemeConfig>(json) ?? new();
                }
            }
            catch { CurrentTheme = new(); }

            ApplyTheme(CurrentTheme, persist: false);
        }

        public static void ApplyTheme(UserThemeConfig theme, bool persist = true)
        {
            CurrentTheme = theme;

            var app = Application.Current;
            if (app != null)
            {
                try
                {
                    var primColor = (Color)ColorConverter.ConvertFromString(theme.BackgroundPrimary);
                    var secColor = (Color)ColorConverter.ConvertFromString(theme.BackgroundSecondary);
                    var accColor = (Color)ColorConverter.ConvertFromString(theme.AccentColor);
                    var lineColor = (Color)ColorConverter.ConvertFromString(string.IsNullOrEmpty(theme.LineColor) ? "#FF602FCE" : theme.LineColor);

                    app.Resources["ThemeBackgroundPrimary"] = new SolidColorBrush(primColor);
                    app.Resources["ThemeBackgroundSecondary"] = new SolidColorBrush(secColor);
                    app.Resources["ThemeAccentBrush"] = new SolidColorBrush(accColor);
                    app.Resources["ThemeLineColorBrush"] = new SolidColorBrush(lineColor);

                    var hoverColor = !string.IsNullOrEmpty(theme.LineColor) ? lineColor : primColor;
                    app.Resources["ThemeTileHoverBorderBrush"] = new SolidColorBrush(hoverColor);

                    var gradBrush = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(1, 1)
                    };
                    gradBrush.GradientStops.Add(new GradientStop(primColor, 0.0));
                    gradBrush.GradientStops.Add(new GradientStop(secColor, 0.5));
                    gradBrush.GradientStops.Add(new GradientStop(primColor, 1.0));

                    app.Resources["ThemeWallpaperBrush"] = gradBrush;

                    MainWindow.Instance?.UpdateWallpaperTheme(primColor, secColor, accColor, lineColor);
                    MainWindow.Instance?.ApplyCustomWallpaper(theme.CustomWallpaperPath, theme.CustomWallpaperDarkness);
                }
                catch { }
            }

            if (persist) SaveTheme();
        }

        public static void SaveTheme()
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                string json = JsonSerializer.Serialize(CurrentTheme, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
    }
}
