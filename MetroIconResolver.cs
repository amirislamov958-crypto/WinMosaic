using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Win8StartScreen
{
    public class MetroIconInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string CategoryIcon { get; set; } = "📁";
        public string FilePath { get; set; } = string.Empty;
    }

    public static class MetroIconResolver
    {
        private static readonly Dictionary<string, string> _iconCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<MetroIconInfo> _allIcons = new();
        private static bool _isInitialized = false;

        private static readonly Dictionary<string, string> _explicitAppIconMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Adobe Photoshop", "Adobe Photoshop" },
            { "Adobe Photoshop 2025", "Adobe Photoshop" },
            { "Adobe Premiere Pro", "Adobe Premiere Pro" },
            { "Adobe Premiere Pro 2026", "Adobe Premiere Pro" },
            { "Alarms & Clock", "Clock" },
            { "Blender", "Blender" },
            { "Blender 5.1", "Blender" },
            { "Calculator", "Calculator" },
            { "Calendar", "Calendar" },
            { "Camera", "Mac Photo Booth" },
            { "Chrome", "Google Chrome" },
            { "Clock", "Clock" },
            { "Command Prompt", "Command Prompt" },
            { "Control Panel", "Control Panel" },
            { "Desktop", "Desktop" },
            { "Excel", "Excel 2013" },
            { "Explorer", "Explorer" },
            { "Firefox", "Firefox" },
            { "Food & Drink", "dining" },
            { "Games", "Games" },
            { "Google Chrome", "Google Chrome" },
            { "Health & Fitness", "Health" },
            { "Help", "Help" },
            { "Help+Tips", "Help" },
            { "IE", "Internet Explorer 10" },
            { "Internet Explorer", "Internet Explorer 10" },
            { "Mail", "Live Mail" },
            { "Maps", "Windows 8 Maps" },
            { "Microsoft Excel", "Excel 2013" },
            { "Microsoft PowerPoint", "PowerPoint 2013" },
            { "Microsoft Word", "Word 2013" },
            { "Money", "Chart - Google Docs" },
            { "Music", "Music" },
            { "News", "Windows 8 News" },
            { "Notepad", "Notepad" },
            { "OneDrive", "Live SkyDrive" },
            { "OneNote", "OneNote 2013" },
            { "Paint", "Paint" },
            { "PC settings", "Control Panel" },
            { "People", "Contacts" },
            { "Photos", "Windows 8 Photos" },
            { "Photoshop", "Adobe Photoshop" },
            { "PowerPoint", "PowerPoint 2013" },
            { "Premiere", "Adobe Premiere Pro" },
            { "qBittorrent", "uTorrent" },
            { "Reading List", "Journal" },
            { "Settings", "Control Panel" },
            { "SkyDrive", "Live SkyDrive" },
            { "Skype", "Skype" },
            { "Sports", "sports_logo.scale-100" },
            { "Steam", "Steam" },
            { "Store", "Windows 8 Store" },
            { "Task Manager", "Task Manager" },
            { "Video", "Videos" },
            { "Videos", "Videos" },
            { "Visual Studio", "Visual Studio" },
            { "Visual Studio 2022", "Visual Studio" },
            { "Visual Studio Code", "Visual Studio" },
            { "VMware", "VMware" },
            { "VMware Workstation", "VMware" },
            { "VMware Workstation Pro", "VMware" },
            { "Weather", "The Weather Channel" },
            { "Windows Media Player", "Windows Media Player" },
            { "Windows Store", "Windows 8 Store" },
            { "Word", "Word 2013" },
            { "WordPad", "Wordpad" },
            { "Блокнот", "Notepad" },
            { "Будильники", "Clock" },
            { "Видео", "Videos" },
            { "Выполнить", "Run" },
            { "Диспетчер задач", "Task Manager" },
            { "Документы", "Explorer" },
            { "Журнал Windows", "Journal" },
            { "Записки", "Sticky Notes" },
            { "Защитник Windows", "Action Center" },
            { "Звукозапись", "Sound" },
            { "Здоровье и фитнес", "Health" },
            { "Игры", "Games" },
            { "Изображения", "Windows 8 Photos" },
            { "Календарь", "Calendar" },
            { "Калькулятор", "Calculator" },
            { "Камера", "Mac Photo Booth" },
            { "Карты", "Windows 8 Maps" },
            { "Командная строка", "Command Prompt" },
            { "Кулинария", "dining" },
            { "Люди", "Contacts" },
            { "Магазин", "Windows 8 Store" },
            { "Монитор ресурсов", "Performance Information and Tools" },
            { "Музыка", "Music" },
            { "Ножницы", "Snipping Tool" },
            { "Новости", "Windows 8 News" },
            { "Панель математического ввода", "Math" },
            { "Панель управления", "Control Panel" },
            { "Параметры", "Control Panel" },
            { "Параметры компьютера", "Control Panel" },
            { "Параметры ПК", "Control Panel" },
            { "Погода", "The Weather Channel" },
            { "Подключение к удаленному рабочему столу", "Remote Desktop" },
            { "Подключение к удаленному столу", "Remote Desktop" },
            { "Почта", "Live Mail" },
            { "Проводник", "Explorer" },
            { "Программы по умолчанию", "Control Panel" },
            { "Проигрыватель Windows Media", "Windows Media Player" },
            { "Путешествия Bing", "Windows 8 Maps" },
            { "Рабочий стол", "Desktop" },
            { "Распознавание речи Windows", "Speech Recognition" },
            { "Редактор реестра", "RegEdit" },
            { "Сканер", "Devices and Printers" },
            { "Список для чтения", "Journal" },
            { "Спорт", "sports_logo.scale-100" },
            { "Справка", "Help" },
            { "Справка и поддержка", "Help" },
            { "Справка+советы", "Help" },
            { "Средство записи действий", "Camera" },
            { "Средство переноса данных Windows", "Sync Center" },
            { "Средство просмотра", "Windows 8 Photos" },
            { "Средство просмотра XPS", "XPS Viewer" },
            { "Студия звукозаписи", "Sound" },
            { "Таблица символов", "Character Map" },
            { "Факсы и сканирование", "Fax and Scan" },
            { "Финансы", "Chart - Google Docs" },
            { "Фотографии", "Windows 8 Photos" },
            { "Часы", "Clock" },
            { "Экранная клавиатура", "Keyboard" },
            { "Экранная лупа", "Magnifier" },
            { "Экранный диктор", "Narrator" },
            { "Этот компьютер", "Computer" },
            { "youtube", "YouTube" },
            { "youtube.com", "YouTube" },
            { "youtu.be", "YouTube" },
            { "google", "Google Chrome" },
            { "google.com", "Google Chrome" },
            { "github", "Github" },
            { "github.com", "Github" },
            { "facebook", "Facebook" },
            { "facebook.com", "Facebook" },
            { "fb.com", "Facebook" },
            { "vk", "VKontakte" },
            { "vk.com", "VKontakte" },
            { "twitter", "Twitter" },
            { "twitter.com", "Twitter" },
            { "x.com", "Twitter" },
            { "twitch", "Twitch" },
            { "twitch.tv", "Twitch" },
            { "reddit", "Reddit" },
            { "reddit.com", "Reddit" },
            { "wikipedia", "Wikipedia" },
            { "wikipedia.org", "Wikipedia" },
            { "amazon", "Amazon" },
            { "amazon.com", "Amazon" },
            { "ebay", "Ebay NEW" },
            { "ebay.com", "Ebay NEW" },
            { "bing", "Bing" },
            { "bing.com", "Bing" },
            { "yahoo", "Yahoo" },
            { "yahoo.com", "Yahoo" },
            { "yandex", "Yandex" },
            { "yandex.ru", "Yandex" },
            { "ya.ru", "Yandex" },
            { "telegram", "Telegram" },
            { "t.me", "Telegram" },
            { "netflix", "Netflix" },
            { "netflix.com", "Netflix" },
            { "spotify", "Spotify" },
            { "spotify.com", "Spotify" },
            { "discord", "Discord" },
            { "discord.com", "Discord" },
            { "web", "Internet Explorer 10" },
            { "browser", "Internet Explorer 10" },
            { "internet", "Internet Explorer 10" },
            { "link", "Internet Explorer 10" },
            { "url", "Internet Explorer 10" }
        };

        public static void Initialize()
        {
            if (_isInitialized) return;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidateDirs = new List<string>
            {
                Path.Combine(baseDir, "Assets", "MetroIcons"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WinMosaic", "Assets", "MetroIcons"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Win8StartScreen", "Assets", "MetroIcons")
            };

            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen");
            foreach (var iconsDir in candidateDirs)
            {
                if (!Directory.Exists(iconsDir)) continue;

                var files = Directory.GetFiles(iconsDir, "*.png", SearchOption.AllDirectories);
                try { File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] MetroIconResolver scanned {iconsDir}, files={files.Length}\n"); } catch { }
                foreach (var file in files)
                {
                    string dirName = Path.GetDirectoryName(file) ?? "";
                    if (dirName.EndsWith("\\ICO", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Contains("\\ICO\\", StringComparison.OrdinalIgnoreCase) ||
                        dirName.EndsWith("\\Reflective", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Contains("\\Reflective\\", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string nameOnly = Path.GetFileNameWithoutExtension(file).Trim();

                    string category = "✨ Разное";
                    string catIcon = "✨";

                    if (dirName.Contains("Applications", StringComparison.OrdinalIgnoreCase))
                    {
                        category = "📱 Приложения";
                        catIcon = "📱";
                    }
                    else if (dirName.Contains("Devices", StringComparison.OrdinalIgnoreCase))
                    {
                        category = "💾 Устройства и диски";
                        catIcon = "💾";
                    }
                    else if (dirName.Contains("Folders", StringComparison.OrdinalIgnoreCase))
                    {
                        category = "📂 Папки и Windows";
                        catIcon = "📂";
                    }
                    else if (dirName.Contains("Google", StringComparison.OrdinalIgnoreCase))
                    {
                        category = "🔍 Сервисы Google";
                        catIcon = "🔍";
                    }
                    else if (dirName.Contains("Internet", StringComparison.OrdinalIgnoreCase))
                    {
                        category = "🌐 Интернет и ссылки";
                        catIcon = "🌐";
                    }
                    else if (dirName.Contains("Office", StringComparison.OrdinalIgnoreCase))
                    {
                        category = "📊 Офис и документы";
                        catIcon = "📊";
                    }
                    else if (dirName.Contains("System", StringComparison.OrdinalIgnoreCase))
                    {
                        category = "⚙ Системные значки";
                        catIcon = "⚙";
                    }
                    else if (dirName.Contains("Web Browsers", StringComparison.OrdinalIgnoreCase) || dirName.Contains("Browsers", StringComparison.OrdinalIgnoreCase))
                    {
                        category = "🌐 Браузеры";
                        catIcon = "🌐";
                    }

                    if (!_iconCache.ContainsKey(nameOnly))
                    {
                        _iconCache[nameOnly] = file;
                        _allIcons.Add(new MetroIconInfo
                        {
                            Name = nameOnly,
                            Category = category,
                            CategoryIcon = catIcon,
                            FilePath = file
                        });
                    }
                }
            }
            // Check custom icons directory
            var customDirs = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen", "CustomIcons"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen", "custom_icons")
            };

            foreach (var customDir in customDirs)
            {
                if (!Directory.Exists(customDir)) continue;
                foreach (var file in Directory.GetFiles(customDir, "*.png", SearchOption.TopDirectoryOnly))
                {
                    string nameOnly = Path.GetFileNameWithoutExtension(file).Trim();
                    if (!_iconCache.ContainsKey(nameOnly))
                    {
                        _iconCache[nameOnly] = file;
                        _allIcons.Add(new MetroIconInfo
                        {
                            Name = nameOnly,
                            Category = "🖼 Мои значки (PNG)",
                            CategoryIcon = "🖼",
                            FilePath = file
                        });
                    }
                }
            }

            _isInitialized = true;
        }

        public static List<MetroIconInfo> GetAllIcons()
        {
            if (!_isInitialized) Initialize();
            return _allIcons;
        }

        public static string ResolveIconPath(string appName)
        {
            if (!_isInitialized) Initialize();
            if (string.IsNullOrWhiteSpace(appName)) return string.Empty;

            string clean = appName.Trim();
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen");
            try { File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.Now}] ResolveIconPath looking for '{clean}', cache count={_iconCache.Count}\n"); } catch { }

            // 1. Explicit mapping
            if (_explicitAppIconMap.TryGetValue(clean, out string? mappedName))
            {
                if (_iconCache.TryGetValue(mappedName, out string? mappedPath))
                {
                    return mappedPath;
                }
                if (File.Exists(mappedName))
                {
                    return mappedName;
                }
            }

            // 2. Direct name match in cache
            if (_iconCache.TryGetValue(clean, out string? directPath))
            {
                return directPath;
            }

            // 3. Partial search
            if (clean.Length >= 3)
            {
                foreach (var kvp in _iconCache)
                {
                    if (kvp.Key.Length < 3) continue;

                    if (string.Equals(kvp.Key, clean, StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.Contains(clean, StringComparison.OrdinalIgnoreCase) ||
                        clean.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        return kvp.Value;
                    }
                }
            }

            return string.Empty;
        }
    }
}
