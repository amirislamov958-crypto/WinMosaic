using System;
using System.Collections.Generic;

namespace Win8StartScreen
{
    public static class LocalizationManager
    {
        public static string CurrentLanguage { get; private set; } = "ru";

        public static event Action? LanguageChanged;

        private static readonly Dictionary<string, Dictionary<string, string>> Strings = new()
        {
            ["ru"] = new Dictionary<string, string>
            {
                // Главный экран Пуск
                ["StartTitle"] = "Пуск",
                ["SearchPlaceholder"] = "Поиск",
                ["AppsNavTooltip"] = "Все приложения",
                ["StartNavTooltip"] = "Пуск",

                // Названия плиток (Tile Titles)
                ["Tile_Mail"] = "Почта",
                ["Tile_Calendar"] = "Календарь",
                ["Tile_Store"] = "Магазин",
                ["Tile_Sports"] = "Спорт",
                ["Tile_Money"] = "Финансы",
                ["Tile_People"] = "Люди",
                ["Tile_Desktop"] = "Рабочий стол",
                ["Tile_Weather"] = "Погода",
                ["Tile_Photos"] = "Фотографии",
                ["Tile_News"] = "Новости",
                ["Tile_HelpTips"] = "Справка+советы",
                ["Tile_FoodDrink"] = "Кулинария",
                ["Tile_Maps"] = "Карты",
                ["Tile_ReadingList"] = "Список для чтения",
                ["Tile_PCSettings"] = "Параметры ПК",

                // Меню пользователя
                ["UserPersonalize"] = "Персонализация",
                ["UserChangeAvatar"] = "Сменить аватар",
                ["UserLock"] = "Заблокировать",
                ["UserSignOut"] = "Выйти",

                // Меню питания
                ["PowerSleep"] = "Спящий режим",
                ["PowerShutdown"] = "Завершение работы",
                ["PowerRestart"] = "Перезагрузка",

                // Панель Параметры (Settings Charm)
                ["SettingsTitle"] = "Параметры",
                ["SettingsSubtitle"] = "Начальный экран",
                ["SettingsPersonalize"] = "Персонализация",
                ["SettingsAddTile"] = "Добавить свою плитку",
                ["SettingsMonitors"] = "Экраны и мониторы",
                ["SettingsLanguage"] = "Язык интерфейса",
                ["SettingsAutostart"] = "Автозагрузка",
                ["SettingsFounders"] = "Основатели",
                ["SettingsControlPanel"] = "Панель управления",
                ["SettingsTaskManager"] = "Диспетчер задач",
                ["SettingsStudio"] = "Редактор экрана (Studio)",
                ["SettingsPCSettings"] = "Изменение параметров компьютера",

                // Вкладка Мониторы
                ["MonitorsTitle"] = "Экраны",
                ["MonitorsSubtitle"] = "Выбор монитора для начального экрана",
                ["MonitorsPrimary"] = "Основной монитор",
                ["MonitorsSecondary"] = "Второй монитор",
                ["MonitorsCurrent"] = "Текущий выбор",
                ["MonitorsDetected"] = "Обнаружено мониторов:",
                ["MonitorsNoSecond"] = "Второй монитор не обнаружен. Подключите дополнительный дисплей для отображения начального экрана на нём.",

                // Вкладка Языки
                ["LanguageTitle"] = "Язык",
                ["LanguageSubtitle"] = "Выберите язык интерфейса",
                ["LanguageRussian"] = "Русский",
                ["LanguageEnglish"] = "English",

                // Вкладка Автозагрузка
                ["AutostartTitle"] = "Автозагрузка",
                ["AutostartSubtitle"] = "Параметры запуска WinMosaic вместе с Windows",
                ["AutostartWindowsHeader"] = "АВТОЗАПУСК С WINDOWS",
                ["AutostartWindowsDesc"] = "Запускать оболочку начального экрана автоматически при входе в систему.",
                ["AutostartBootToStartHeader"] = "ОТКРЫВАТЬ «ПУСК» ПРИ ВКЛЮЧЕНИИ ПК",
                ["AutostartBootToStartDesc"] = "Сразу открывать начальный экран при загрузке системы (как в оригинальной Windows 8.1).",
                ["AutostartStatusEnabled"] = "Включено",
                ["AutostartStatusDisabled"] = "Отключено",

                // Вкладка Основатели
                ["FoundersTitle"] = "Основатели",
                ["FoundersSubtitle"] = "Создатели проекта WinMosaic",
                ["FoundersAuthorHeader"] = "СОЗДАТЕЛЬ ПРОЕКТА",
                ["FoundersAuthorSubtext"] = "Создал в одиночку by ПЕТЯ",
                ["FoundersStudioHeader"] = "СТУДИЯ РАЗРАБОТКИ",
                ["FoundersStudioSubtext"] = "Создано студией ChaosMaster - Studio®",
                ["FoundersAboutHeader"] = "О ПРОЕКТЕ",
                ["FoundersTagline"] = "Всё, что создано в этом проекте — создано студией ChaosMaster - Studio® и создал в одиночку by ПЕТЯ.",
                ["FoundersDesc"] = "WinMosaic — воссоздание легендарного начального экрана Windows 8.1 для Windows 10 и Windows 11 со всеми эффектами, живыми плитками и поддержкой нескольких мониторов.",
                ["FoundersCopyright"] = "© 2026 by ПЕТЯ & ChaosMaster - Studio®",

                // Вкладка Приложения
                ["AppsTitle"] = "Приложения",
                ["SortByName"] = "по имени",
                ["SortByDate"] = "по дате установки",
                ["SortByUsage"] = "по частоте",
                ["SortByCategory"] = "по категории",

                // Трей
                ["TrayOpenStart"] = "Открыть Пуск (Win)",
                ["TrayPersonalize"] = "Персонализация (Win+I)",
                ["TrayTileEditor"] = "Редактор плиток (Studio)",
                ["TrayAutostart"] = "Автозагрузка с Windows",
                ["TrayBootToStart"] = "Открывать «Пуск» сразу при включении ПК",
                ["TrayExit"] = "Выход",

                // Нижняя панель и группы
                ["AppBarNameGroups"] = "Назвать группы",
                ["GroupNamePlaceholder"] = "Назвать группу",
                ["AppBarUnpin"] = "Удалить из меню Пуск",
                ["AppBarResize"] = "Изменить размер",
                ["AppBarTurnLiveOff"] = "Отключить динамические плитки",
                ["AppBarTurnLiveOn"] = "Включить динамические плитки",

                // Живые плитки (Live Tiles)
                ["LiveSportsHeadline"] = "Роман Широков: Быть",
                ["LiveSportsSubheadline"] = "капитаном — это...",
                ["LiveNewsHeadline"] = "Последняя молитва",
                ["LiveFoodHeadline"] = "Паста с брокколи",
                ["LiveCalendarText"] = "14:00 - Встреча команды\n18:30 - Тренировка",
                ["LiveWeatherCity"] = "Москва",
                ["LiveWeatherCondition"] = "Преимущественно солнечно",
                ["LiveWeatherText"] = "Москва, Ясно\n+21°C | Влажность 45%",
                ["LiveStoreHeadline"] = "Войдите в Лабораторию...",
                ["LiveStoreAppName"] = "Гадкий Я: Minion Rush",
                ["LiveStoreRatingPrice"] = "Бесплатно ★★★★★ 12 369",
                ["LiveStoreTop1Sub"] = "Бесплатно ★★★★★ 18 420",
                ["LiveStoreTop2Name"] = "Asphalt 8: На взлёт",
                ["LiveStoreTop2Sub"] = "Бесплатно ★★★★★ 95 430",
                ["LiveStoreTop3Sub"] = "Бесплатно ★★★★★ 27 890"
            },
            ["en"] = new Dictionary<string, string>
            {
                // Main Start Screen
                ["StartTitle"] = "Start",
                ["SearchPlaceholder"] = "Search",
                ["AppsNavTooltip"] = "All apps",
                ["StartNavTooltip"] = "Start",

                // Tile Titles
                ["Tile_Mail"] = "Mail",
                ["Tile_Calendar"] = "Calendar",
                ["Tile_Store"] = "Store",
                ["Tile_Sports"] = "Sports",
                ["Tile_Money"] = "Money",
                ["Tile_People"] = "People",
                ["Tile_Desktop"] = "Desktop",
                ["Tile_Weather"] = "Weather",
                ["Tile_Photos"] = "Photos",
                ["Tile_News"] = "News",
                ["Tile_HelpTips"] = "Help+Tips",
                ["Tile_FoodDrink"] = "Food & Drink",
                ["Tile_Maps"] = "Maps",
                ["Tile_ReadingList"] = "Reading List",
                ["Tile_PCSettings"] = "PC settings",

                // User menu
                ["UserPersonalize"] = "Personalize",
                ["UserChangeAvatar"] = "Change account picture",
                ["UserLock"] = "Lock",
                ["UserSignOut"] = "Sign out",

                // Power menu
                ["PowerSleep"] = "Sleep",
                ["PowerShutdown"] = "Shut down",
                ["PowerRestart"] = "Restart",

                // Settings Charm
                ["SettingsTitle"] = "Settings",
                ["SettingsSubtitle"] = "Start screen",
                ["SettingsPersonalize"] = "Personalize",
                ["SettingsAddTile"] = "Add custom tile",
                ["SettingsMonitors"] = "Displays & Monitors",
                ["SettingsLanguage"] = "Display Language",
                ["SettingsAutostart"] = "Autostart",
                ["SettingsFounders"] = "Founders",
                ["SettingsControlPanel"] = "Control Panel",
                ["SettingsTaskManager"] = "Task Manager",
                ["SettingsStudio"] = "Screen Editor (Studio)",
                ["SettingsPCSettings"] = "Change PC settings",

                // Displays view
                ["MonitorsTitle"] = "Displays",
                ["MonitorsSubtitle"] = "Choose display for Start screen",
                ["MonitorsPrimary"] = "Primary Monitor",
                ["MonitorsSecondary"] = "Secondary Monitor",
                ["MonitorsCurrent"] = "Selected",
                ["MonitorsDetected"] = "Detected displays:",
                ["MonitorsNoSecond"] = "No second monitor detected. Connect an additional display to show the Start screen on it.",

                // Language view
                ["LanguageTitle"] = "Language",
                ["LanguageSubtitle"] = "Select interface language",
                ["LanguageRussian"] = "Русский",
                ["LanguageEnglish"] = "English",

                // Autostart view
                ["AutostartTitle"] = "Autostart",
                ["AutostartSubtitle"] = "WinMosaic startup options with Windows",
                ["AutostartWindowsHeader"] = "START WITH WINDOWS",
                ["AutostartWindowsDesc"] = "Launch Start Screen shell automatically when logging in to Windows.",
                ["AutostartBootToStartHeader"] = "OPEN START ON BOOT",
                ["AutostartBootToStartDesc"] = "Immediately display the Start Screen upon system startup (like original Windows 8.1).",
                ["AutostartStatusEnabled"] = "Enabled",
                ["AutostartStatusDisabled"] = "Disabled",

                // Founders view
                ["FoundersTitle"] = "Founders",
                ["FoundersSubtitle"] = "WinMosaic Project Creators",
                ["FoundersAuthorHeader"] = "PROJECT CREATOR",
                ["FoundersAuthorSubtext"] = "Created single-handedly by ПЕТЯ",
                ["FoundersStudioHeader"] = "DEVELOPMENT STUDIO",
                ["FoundersStudioSubtext"] = "Created by studio ChaosMaster - Studio®",
                ["FoundersAboutHeader"] = "ABOUT PROJECT",
                ["FoundersTagline"] = "Everything created in this project — created by studio ChaosMaster - Studio® and single-handedly by ПЕТЯ.",
                ["FoundersDesc"] = "WinMosaic — recreation of the legendary Windows 8.1 Start Screen shell for Windows 10 and Windows 11 with full animations, live tiles, and multi-monitor support.",
                ["FoundersCopyright"] = "© 2026 by ПЕТЯ & ChaosMaster - Studio®",

                // Apps Screen
                ["AppsTitle"] = "Apps",
                ["SortByName"] = "by name",
                ["SortByDate"] = "by date installed",
                ["SortByUsage"] = "by most used",
                ["SortByCategory"] = "by category",

                // Tray
                ["TrayOpenStart"] = "Open Start (Win)",
                ["TrayPersonalize"] = "Personalize (Win+I)",
                ["TrayTileEditor"] = "Tile Editor (Studio)",
                ["TrayAutostart"] = "Start with Windows",
                ["TrayBootToStart"] = "Open Start on PC boot",
                ["TrayExit"] = "Exit",

                // Bottom App Bar and Groups
                ["AppBarNameGroups"] = "Name groups",
                ["GroupNamePlaceholder"] = "Name group",
                ["AppBarUnpin"] = "Unpin from Start",
                ["AppBarResize"] = "Resize",
                ["AppBarTurnLiveOff"] = "Turn live tile off",
                ["AppBarTurnLiveOn"] = "Turn live tile on",

                // Live Tiles
                ["LiveSportsHeadline"] = "Premier League: Matchday Preview",
                ["LiveSportsSubheadline"] = "Title race heats up...",
                ["LiveNewsHeadline"] = "Global Tech Summit 2026",
                ["LiveFoodHeadline"] = "Creamy Broccoli Pasta",
                ["LiveCalendarText"] = "2:00 PM - Team Sync\n6:30 PM - Gym Workout",
                ["LiveWeatherCity"] = "London",
                ["LiveWeatherCondition"] = "Mostly Sunny",
                ["LiveWeatherText"] = "London, Clear\n+21°C | Humidity 45%",
                ["LiveStoreHeadline"] = "Enter the Laboratory...",
                ["LiveStoreAppName"] = "Despicable Me: Minion Rush",
                ["LiveStoreRatingPrice"] = "Free ★★★★★ 12,369",
                ["LiveStoreTop1Sub"] = "Free ★★★★★ 18,420",
                ["LiveStoreTop2Name"] = "Asphalt 8: Airborne",
                ["LiveStoreTop2Sub"] = "Free ★★★★★ 95,430",
                ["LiveStoreTop3Sub"] = "Free ★★★★★ 27,890"
            }
        };

        public static void SetLanguage(string lang)
        {
            if (lang != "ru" && lang != "en") lang = "ru";
            if (CurrentLanguage == lang) return;

            CurrentLanguage = lang;
            LanguageChanged?.Invoke();
        }

        public static string Get(string key, string fallback = "")
        {
            if (Strings.TryGetValue(CurrentLanguage, out var dict) && dict.TryGetValue(key, out var val))
            {
                return val;
            }
            if (Strings["ru"].TryGetValue(key, out var ruVal))
            {
                return ruVal;
            }
            return fallback;
        }
    }
}
