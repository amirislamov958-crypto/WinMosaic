using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Win8StartScreen
{
    public static class AutostartHelper
    {
        private const string RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "WinMosaic";
        private const string LEGACY_APP_NAME = "Win8StartScreen";

        public static bool IsAutostartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RUN_KEY, false);
                var val = (key?.GetValue(APP_NAME) ?? key?.GetValue(LEGACY_APP_NAME)) as string;
                return !string.IsNullOrEmpty(val);
            }
            catch
            {
                return false;
            }
        }

        public static void SetAutostart(bool enable, string? targetExePath = null)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RUN_KEY, true);
                if (key == null) return;

                // Clean legacy key if present
                if (key.GetValue(LEGACY_APP_NAME) != null)
                {
                    try { key.DeleteValue(LEGACY_APP_NAME, false); } catch { }
                }

                if (enable)
                {
                    string localProgNew = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WinMosaic", "WinMosaic.exe");
                    string localProg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Win8StartScreen", "WinMosaic.exe");
                    string localProgLegacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Win8StartScreen", "Win8StartScreen.exe");

                    string exePath = targetExePath ?? "";
                    if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                    {
                        if (File.Exists(localProgNew)) exePath = localProgNew;
                        else if (File.Exists(localProg)) exePath = localProg;
                        else if (File.Exists(localProgLegacy)) exePath = localProgLegacy;
                        else exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    }

                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(APP_NAME, $"\"{exePath}\"");
                    }
                }
                else
                {
                    if (key.GetValue(APP_NAME) != null)
                    {
                        key.DeleteValue(APP_NAME, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutostartHelper] Error: {ex.Message}");
            }
        }

        public static void EnsureAutostartRegistered()
        {
            try
            {
                string localProgNew = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WinMosaic", "WinMosaic.exe");
                string localProg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Win8StartScreen", "WinMosaic.exe");
                string? targetExe = File.Exists(localProgNew) ? localProgNew : (File.Exists(localProg) ? localProg : null);

                using var key = Registry.CurrentUser.OpenSubKey(RUN_KEY, true);
                if (key != null)
                {
                    var legacyVal = key.GetValue(LEGACY_APP_NAME) as string;
                    if (!string.IsNullOrEmpty(legacyVal))
                    {
                        try { key.DeleteValue(LEGACY_APP_NAME, false); } catch { }
                    }

                    var val = key.GetValue(APP_NAME) as string;
                    if (string.IsNullOrEmpty(val) || (targetExe != null && !val.Contains(targetExe, StringComparison.OrdinalIgnoreCase)))
                    {
                        SetAutostart(true, targetExe);
                    }
                }
            }
            catch { }
        }
    }
}
