using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace Win8StartScreen.Models
{
    public enum TileSize
    {
        Small,   // 71x71
        Medium,  // 150x150
        Wide,    // 308x150
        Large    // 308x308
    }

    public enum AnimationPreset
    {
        FlipRoll3D,
        SpringTilt3D,
        SlideVertical,
        SlideHorizontal,
        PulseBounce,
        GlitchShimmer,
        FadeBadge
    }

    public enum EasingType
    {
        CubicEaseInOut,
        BackEaseOut,
        ElasticEaseOut,
        BounceEaseOut,
        SineEaseInOut,
        Linear
    }

    public class TileModel : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _title = "Tile";
        private string _groupName = "";
        private TileSize _size = TileSize.Medium;
        private double _x = 0;
        private double _y = 0;
        private string _backgroundColor = "#FF0078D7";
        private string _iconImagePath = string.Empty;
        private string _iconVectorPath = string.Empty;
        private string _iconGlyph = string.Empty;
        private string _liveText = string.Empty;
        private string _liveTemplate = "Auto";
        private string _liveImagePath = string.Empty;
        private string _liveHeadline = string.Empty;
        private string _liveSubheadline = string.Empty;
        private string _liveBannerColor = string.Empty;
        private string _liveBannerIcon = string.Empty;
        private string _liveTemperature = "+21°";
        private string _liveCondition = "Ясно";
        private string _liveCity = "Москва";
        private string _liveQuote1Name = "DOW";
        private string _liveQuote1Value = "16 556,82";
        private string _liveQuote1Change = "-16,18";
        private bool _liveQuote1IsUp = false;
        private string _liveQuote2Name = "FTSE 100";
        private string _liveQuote2Value = "6 655,20";
        private string _liveQuote2Change = "-3,84";
        private bool _liveQuote2IsUp = false;
        private string _liveQuote3Name = "NIKKEI 225";
        private string _liveQuote3Value = "15 071,88";
        private string _liveQuote3Change = "+125,56";
        private bool _liveQuote3IsUp = true;
        private string _storeAppIcon = string.Empty;
        private string _storeHeadline = "Войдите в Лаборат...";
        private string _storeAppName = "Гадкий Я: Minion Rush";
        private string _storeRatingPrice = "Бесплатно ★★★★★ 12 369";
        private string _storeBadgeCount = "11";
        private string _storeTopApp1Icon = string.Empty;
        private string _storeTopApp1Name = "Minecraft: Pocket Edition";
        private string _storeTopApp1Sub = "Бесплатно ★★★★★ 18 420";
        private string _storeTopApp2Icon = string.Empty;
        private string _storeTopApp2Name = "Asphalt 8: На взлёт";
        private string _storeTopApp2Sub = "Бесплатно ★★★★★ 95 430";
        private string _storeTopApp3Icon = string.Empty;
        private string _storeTopApp3Name = "Jetpack Joyride";
        private string _storeTopApp3Sub = "Бесплатно ★★★★★ 27 890";
        private string _gameIcon = string.Empty;
        private string _gameTitle = "Angry Birds";
        private string _gameSubtitle = "Xbox Live";
        private ImageSource? _storeAppIconSource = null;
        private ImageSource? _gameIconSource = null;
        private ImageSource? _desktopWallpaperSource = null;
        private string _executablePath = string.Empty;
        private bool _isLiveTileEnabled = true;
        private bool _isSelected = false;
        private bool _isLocked = false;
        private bool _isVisible = true;
        private double _iconScale = 1.0;
        private double _iconSize = 64.0;
        private double _iconOffsetX = 0.0;
        private double _iconOffsetY = 0.0;
        private string _iconStretch = "Uniform";

        // Анимационные параметры (Blender/C4D timeline)
        private AnimationPreset _animationType = AnimationPreset.FlipRoll3D;
        private double _animationDuration = 0.5; // в секундах
        private double _animationDelay = 0.0;
        private double _animationInterval = 4.0; // повтор каждые X сек
        private EasingType _easing = EasingType.CubicEaseInOut;
        private double _tiltIntensity = 15.0; // градусы

        private double _customWidth = 0;
        private double _customHeight = 0;

        // Кастомные ключевые кадры (Photoshop/Blender keyframes)
        private double _animRotation = 0;
        private double _animTiltX = 0;
        private double _animTiltY = 0;
        private double _animScale = 1.0;
        private double _animOffsetX = 0;
        private double _animOffsetY = 0;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Title
        {
            get => _title;
            set 
            { 
                _title = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(DisplayTitle)); 
            }
        }

        [JsonIgnore]
        public string DisplayTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_title)) return _title;
                if (!string.IsNullOrWhiteSpace(_iconImagePath))
                {
                    try
                    {
                        var fname = System.IO.Path.GetFileNameWithoutExtension(_iconImagePath).ToLowerInvariant();
                        if (fname.Contains("camera")) return "Камера (Camera)";
                        if (fname.Contains("video") || fname.Contains("win8icons_32")) return "Видео (Videos)";
                        if (fname.Contains("music")) return "Музыка (Music)";
                        if (fname.Contains("sports")) return "Спорт (Sports)";
                        var clean = System.IO.Path.GetFileNameWithoutExtension(_iconImagePath);
                        if (!string.IsNullOrEmpty(clean))
                            return char.ToUpper(clean[0]) + clean.Substring(1);
                    }
                    catch { }
                }
                return $"Плитка ({_size})";
            }
        }

        public string GroupName
        {
            get => _groupName;
            set { _groupName = value; OnPropertyChanged(); }
        }

        public TileSize Size
        {
            get => _size;
            set
            {
                _size = value;
                _customWidth = 0;
                _customHeight = 0;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CustomWidth));
                OnPropertyChanged(nameof(CustomHeight));
                OnPropertyChanged(nameof(PixelWidth));
                OnPropertyChanged(nameof(PixelHeight));
            }
        }

        public double CustomWidth
        {
            get => _customWidth;
            set
            {
                _customWidth = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PixelWidth));
            }
        }

        public double CustomHeight
        {
            get => _customHeight;
            set
            {
                _customHeight = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PixelHeight));
            }
        }

        public double X
        {
            get => _x;
            set { _x = value; OnPropertyChanged(); }
        }

        public double Y
        {
            get => _y;
            set { _y = value; OnPropertyChanged(); }
        }

        [JsonIgnore]
        public double PixelWidth => _customWidth > 0 ? _customWidth : Size switch
        {
            TileSize.Small => 72,
            TileSize.Medium => 150,
            TileSize.Wide => 306,
            TileSize.Large => 306,
            _ => 150
        };

        [JsonIgnore]
        public double PixelHeight => _customHeight > 0 ? _customHeight : Size switch
        {
            TileSize.Small => 72,
            TileSize.Medium => 150,
            TileSize.Wide => 150,
            TileSize.Large => 306,
            _ => 150
        };

        public double AnimRotation
        {
            get => _animRotation;
            set { _animRotation = value; OnPropertyChanged(); }
        }

        public double AnimTiltX
        {
            get => _animTiltX;
            set { _animTiltX = value; OnPropertyChanged(); }
        }

        public double AnimTiltY
        {
            get => _animTiltY;
            set { _animTiltY = value; OnPropertyChanged(); }
        }

        public double AnimScale
        {
            get => _animScale;
            set { _animScale = value; OnPropertyChanged(); }
        }

        public double AnimOffsetX
        {
            get => _animOffsetX;
            set { _animOffsetX = value; OnPropertyChanged(); }
        }

        public double AnimOffsetY
        {
            get => _animOffsetY;
            set { _animOffsetY = value; OnPropertyChanged(); }
        }

        public double IconScale
        {
            get => _iconScale <= 0 ? 1.0 : _iconScale;
            set { _iconScale = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectiveIconWidth)); OnPropertyChanged(nameof(EffectiveIconHeight)); }
        }

        public double IconSize
        {
            get => _iconSize <= 0 ? GetDefaultIconSize(Size) : _iconSize;
            set { _iconSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectiveIconWidth)); OnPropertyChanged(nameof(EffectiveIconHeight)); }
        }

        public static double GetDefaultIconSize(TileSize size) => size switch
        {
            TileSize.Large => 120.0,
            TileSize.Wide => 72.0,
            TileSize.Medium => 64.0,
            TileSize.Small => 36.0,
            _ => 64.0
        };

        public double IconOffsetX
        {
            get => _iconOffsetX;
            set { _iconOffsetX = value; OnPropertyChanged(); }
        }

        public double IconOffsetY
        {
            get => _iconOffsetY;
            set { _iconOffsetY = value; OnPropertyChanged(); }
        }

        public string IconStretch
        {
            get => string.IsNullOrEmpty(_iconStretch) ? "Uniform" : _iconStretch;
            set { _iconStretch = value; OnPropertyChanged(); }
        }

        [JsonIgnore]
        public double EffectiveIconWidth => IconSize * IconScale;

        [JsonIgnore]
        public double EffectiveIconHeight => IconSize * IconScale;

        public string BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                _backgroundColor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TileBrush));
            }
        }

        [JsonIgnore]
        public Brush TileBrush
        {
            get
            {
                try
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(BackgroundColor));
                }
                catch
                {
                    return new SolidColorBrush(Color.FromRgb(0, 120, 215));
                }
            }
            set
            {
                if (value is SolidColorBrush scb)
                {
                    BackgroundColor = scb.Color.ToString();
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackgroundBrush));
            }
        }

        [JsonIgnore]
        public Brush BackgroundBrush
        {
            get => TileBrush;
            set => TileBrush = value;
        }


        public string IconImagePath
        {
            get => _iconImagePath;
            set
            {
                _iconImagePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasIconImage));
            }
        }

        [JsonIgnore]
        public bool HasIconImage => !string.IsNullOrEmpty(IconImagePath) && (System.IO.File.Exists(IconImagePath) || IconImagePath.StartsWith("pack://"));

        public string IconVectorPath
        {
            get => _iconVectorPath;
            set
            {
                _iconVectorPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasVectorPath));
            }
        }

        [JsonIgnore]
        public bool HasVectorPath => !string.IsNullOrEmpty(IconVectorPath);

        public string IconGlyph
        {
            get => _iconGlyph;
            set { _iconGlyph = value; OnPropertyChanged(); }
        }

        public string LiveText
        {
            get => _liveText;
            set { _liveText = value; OnPropertyChanged(); }
        }

        public string LiveTemplate
        {
            get => string.IsNullOrEmpty(_liveTemplate) ? "Auto" : _liveTemplate;
            set
            {
                _liveTemplate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPhotoBannerTemplate));
                OnPropertyChanged(nameof(IsFinanceTemplate));
                OnPropertyChanged(nameof(IsWeatherTemplate));
                OnPropertyChanged(nameof(IsStoreTemplate));
                OnPropertyChanged(nameof(IsGamesTemplate));
                OnPropertyChanged(nameof(IsStandardTextTemplate));
            }
        }

        public string LiveImagePath
        {
            get => _liveImagePath;
            set { _liveImagePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasLiveImage)); }
        }

        [JsonIgnore]
        public bool HasLiveImage => !string.IsNullOrEmpty(LiveImagePath);

        public string LiveHeadline
        {
            get => _liveHeadline;
            set { _liveHeadline = value; OnPropertyChanged(); }
        }

        public string LiveSubheadline
        {
            get => _liveSubheadline;
            set { _liveSubheadline = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasLiveSubheadline)); }
        }

        [JsonIgnore]
        public bool HasLiveSubheadline => !string.IsNullOrEmpty(LiveSubheadline);

        public string LiveBannerColor
        {
            get => _liveBannerColor;
            set { _liveBannerColor = value; OnPropertyChanged(); OnPropertyChanged(nameof(LiveBannerBrush)); }
        }

        [JsonIgnore]
        public Brush LiveBannerBrush
        {
            get
            {
                if (!string.IsNullOrEmpty(_liveBannerColor))
                {
                    try
                    {
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(_liveBannerColor));
                    }
                    catch { }
                }
                return BackgroundBrush;
            }
        }

        public string LiveBannerIcon
        {
            get => _liveBannerIcon;
            set { _liveBannerIcon = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasLiveBannerIcon)); }
        }

        [JsonIgnore]
        public bool HasLiveBannerIcon => !string.IsNullOrEmpty(LiveBannerIcon);

        public string LiveTemperature
        {
            get => _liveTemperature;
            set { _liveTemperature = value; OnPropertyChanged(); }
        }

        public string LiveCondition
        {
            get => _liveCondition;
            set { _liveCondition = value; OnPropertyChanged(); }
        }

        public string LiveCity
        {
            get => _liveCity;
            set { _liveCity = value; OnPropertyChanged(); }
        }

        public string LiveQuote1Name { get => _liveQuote1Name; set { _liveQuote1Name = value; OnPropertyChanged(); } }
        public string LiveQuote1Value { get => _liveQuote1Value; set { _liveQuote1Value = value; OnPropertyChanged(); } }
        public string LiveQuote1Change { get => _liveQuote1Change; set { _liveQuote1Change = value; OnPropertyChanged(); } }
        public bool LiveQuote1IsUp { get => _liveQuote1IsUp; set { _liveQuote1IsUp = value; OnPropertyChanged(); } }

        public string LiveQuote2Name { get => _liveQuote2Name; set { _liveQuote2Name = value; OnPropertyChanged(); } }
        public string LiveQuote2Value { get => _liveQuote2Value; set { _liveQuote2Value = value; OnPropertyChanged(); } }
        public string LiveQuote2Change { get => _liveQuote2Change; set { _liveQuote2Change = value; OnPropertyChanged(); } }
        public bool LiveQuote2IsUp { get => _liveQuote2IsUp; set { _liveQuote2IsUp = value; OnPropertyChanged(); } }

        public string LiveQuote3Name { get => _liveQuote3Name; set { _liveQuote3Name = value; OnPropertyChanged(); } }
        public string LiveQuote3Value { get => _liveQuote3Value; set { _liveQuote3Value = value; OnPropertyChanged(); } }
        public string LiveQuote3Change { get => _liveQuote3Change; set { _liveQuote3Change = value; OnPropertyChanged(); } }
        public bool LiveQuote3IsUp { get => _liveQuote3IsUp; set { _liveQuote3IsUp = value; OnPropertyChanged(); } }

        [JsonIgnore]
        public string LiveQuote1Line => $"{(_liveQuote1IsUp ? "▲" : "▼")} {_liveQuote1Value}  {_liveQuote1Change}";

        [JsonIgnore]
        public string LiveQuote2Line => $"{(_liveQuote2IsUp ? "▲" : "▼")} {_liveQuote2Value}  {_liveQuote2Change}";

        [JsonIgnore]
        public string LiveQuote3Line => $"{(_liveQuote3IsUp ? "▲" : "▼")} {_liveQuote3Value}  {_liveQuote3Change}";

        [JsonIgnore]
        public bool IsPhotoBannerTemplate =>
            LiveTemplate == "PhotoBanner" || 
            ((string.IsNullOrEmpty(LiveTemplate) || LiveTemplate.Equals("Auto", StringComparison.OrdinalIgnoreCase)) && HasLiveImage && !string.IsNullOrEmpty(LiveHeadline));

        [JsonIgnore]
        public bool IsFinanceTemplate =>
            LiveTemplate == "FinanceQuotes" || 
            ((string.IsNullOrEmpty(LiveTemplate) || LiveTemplate.Equals("Auto", StringComparison.OrdinalIgnoreCase)) && (Title.Equals("Money", StringComparison.OrdinalIgnoreCase) || Title.Equals("Финансы", StringComparison.OrdinalIgnoreCase)));

        [JsonIgnore]
        public bool IsWeatherTemplate =>
            LiveTemplate == "Weather" || 
            ((string.IsNullOrEmpty(LiveTemplate) || LiveTemplate.Equals("Auto", StringComparison.OrdinalIgnoreCase)) && (Title.Equals("Weather", StringComparison.OrdinalIgnoreCase) || Title.Equals("Погода", StringComparison.OrdinalIgnoreCase)));

        public string StoreAppIcon { get => _storeAppIcon; set { _storeAppIcon = value; OnPropertyChanged(); } }
        public string StoreHeadline { get => _storeHeadline; set { _storeHeadline = value; OnPropertyChanged(); } }
        public string StoreAppName { get => _storeAppName; set { _storeAppName = value; OnPropertyChanged(); } }
        public string StoreRatingPrice { get => _storeRatingPrice; set { _storeRatingPrice = value; OnPropertyChanged(); } }
        public string StoreBadgeCount { get => _storeBadgeCount; set { _storeBadgeCount = value; OnPropertyChanged(); } }
        public string StoreTopApp1Icon { get => _storeTopApp1Icon; set { _storeTopApp1Icon = value; OnPropertyChanged(); } }
        public string StoreTopApp1Name { get => _storeTopApp1Name; set { _storeTopApp1Name = value; OnPropertyChanged(); } }
        public string StoreTopApp1Sub { get => _storeTopApp1Sub; set { _storeTopApp1Sub = value; OnPropertyChanged(); } }
        public string StoreTopApp2Icon { get => _storeTopApp2Icon; set { _storeTopApp2Icon = value; OnPropertyChanged(); } }
        public string StoreTopApp2Name { get => _storeTopApp2Name; set { _storeTopApp2Name = value; OnPropertyChanged(); } }
        public string StoreTopApp2Sub { get => _storeTopApp2Sub; set { _storeTopApp2Sub = value; OnPropertyChanged(); } }
        public string StoreTopApp3Icon { get => _storeTopApp3Icon; set { _storeTopApp3Icon = value; OnPropertyChanged(); } }
        public string StoreTopApp3Name { get => _storeTopApp3Name; set { _storeTopApp3Name = value; OnPropertyChanged(); } }
        public string StoreTopApp3Sub { get => _storeTopApp3Sub; set { _storeTopApp3Sub = value; OnPropertyChanged(); } }

        public string GameIcon { get => _gameIcon; set { _gameIcon = value; OnPropertyChanged(); } }
        public string GameTitle { get => _gameTitle; set { _gameTitle = value; OnPropertyChanged(); } }
        public string GameSubtitle { get => _gameSubtitle; set { _gameSubtitle = value; OnPropertyChanged(); } }

        [JsonIgnore]
        public ImageSource? StoreAppIconSource { get => _storeAppIconSource; set { _storeAppIconSource = value; OnPropertyChanged(); } }

        [JsonIgnore]
        public ImageSource? GameIconSource { get => _gameIconSource; set { _gameIconSource = value; OnPropertyChanged(); } }

        [JsonIgnore]
        public ImageSource? DesktopWallpaperSource
        {
            get
            {
                if (_desktopWallpaperSource == null && IsDesktopTile)
                {
                    _desktopWallpaperSource = MainWindow.LoadDesktopWallpaperBitmap();
                }
                return _desktopWallpaperSource;
            }
            set
            {
                _desktopWallpaperSource = value;
                OnPropertyChanged();
            }
        }

        [JsonIgnore]
        public bool IsStoreTemplate =>
            LiveTemplate == "Store" || 
            ((string.IsNullOrEmpty(LiveTemplate) || LiveTemplate.Equals("Auto", StringComparison.OrdinalIgnoreCase)) && 
             (Title.Equals("Store", StringComparison.OrdinalIgnoreCase) || 
              Title.Equals("Магазин", StringComparison.OrdinalIgnoreCase) || 
              (!string.IsNullOrEmpty(IconImagePath) && IconImagePath.IndexOf("Store", StringComparison.OrdinalIgnoreCase) >= 0)));

        [JsonIgnore]
        public bool IsGamesTemplate =>
            LiveTemplate == "Games" || 
            ((string.IsNullOrEmpty(LiveTemplate) || LiveTemplate.Equals("Auto", StringComparison.OrdinalIgnoreCase)) && 
             (Title.Equals("Games", StringComparison.OrdinalIgnoreCase) || 
              Title.Equals("Игры", StringComparison.OrdinalIgnoreCase) || 
              (!string.IsNullOrEmpty(IconImagePath) && (IconImagePath.IndexOf("XBL_GAMES", StringComparison.OrdinalIgnoreCase) >= 0 || IconImagePath.IndexOf("Games", StringComparison.OrdinalIgnoreCase) >= 0))));

        [JsonIgnore]
        public bool IsStandardTextTemplate => !IsPhotoBannerTemplate && !IsFinanceTemplate && !IsWeatherTemplate && !IsStoreTemplate && !IsGamesTemplate;

        [JsonIgnore]
        public bool IsSportsTile =>
            Title.Equals("Sports", StringComparison.OrdinalIgnoreCase) || 
            Title.Equals("Спорт", StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(IconImagePath) && IconImagePath.IndexOf("sports", StringComparison.OrdinalIgnoreCase) >= 0);

        [JsonIgnore]
        public bool IsNewsTile =>
            Title.Equals("News", StringComparison.OrdinalIgnoreCase) || 
            Title.Equals("Новости", StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(IconImagePath) && (IconImagePath.IndexOf("news", StringComparison.OrdinalIgnoreCase) >= 0 || IconImagePath.IndexOf("новости", StringComparison.OrdinalIgnoreCase) >= 0));

        [JsonIgnore]
        public bool IsDesktopTile =>
            Title.Equals("Desktop", StringComparison.OrdinalIgnoreCase) || 
            Title.Equals("Рабочий стол", StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(ExecutablePath) && ExecutablePath.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) && (Title.IndexOf("Desktop", StringComparison.OrdinalIgnoreCase) >= 0 || Title.IndexOf("Рабочий", StringComparison.OrdinalIgnoreCase) >= 0));

        public string ExecutablePath
        {
            get => _executablePath;
            set { _executablePath = value; OnPropertyChanged(); }
        }

        public bool IsLiveTileEnabled
        {
            get => _isLiveTileEnabled;
            set { _isLiveTileEnabled = value; OnPropertyChanged(); }
        }

        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public bool IsLocked
        {
            get => _isLocked;
            set { _isLocked = value; OnPropertyChanged(); }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        // Анимации
        public AnimationPreset AnimationType
        {
            get => _animationType;
            set { _animationType = value; OnPropertyChanged(); }
        }

        public double AnimationDuration
        {
            get => _animationDuration;
            set { _animationDuration = value; OnPropertyChanged(); }
        }

        public double AnimationDelay
        {
            get => _animationDelay;
            set { _animationDelay = value; OnPropertyChanged(); }
        }

        public double AnimationInterval
        {
            get => _animationInterval;
            set { _animationInterval = value; OnPropertyChanged(); }
        }

        public EasingType Easing
        {
            get => _easing;
            set { _easing = value; OnPropertyChanged(); }
        }

        public double TiltIntensity
        {
            get => _tiltIntensity;
            set { _tiltIntensity = value; OnPropertyChanged(); }
        }

        public TileModel Clone()
        {
            return new TileModel
            {
                Title = this.Title + " (Copy)",
                GroupName = this.GroupName,
                Size = this.Size,
                X = this.X + 20,
                Y = this.Y + 20,
                BackgroundColor = this.BackgroundColor,
                IconImagePath = this.IconImagePath,
                IconVectorPath = this.IconVectorPath,
                IconGlyph = this.IconGlyph,
                LiveText = this.LiveText,
                LiveTemplate = this.LiveTemplate,
                LiveImagePath = this.LiveImagePath,
                LiveHeadline = this.LiveHeadline,
                LiveSubheadline = this.LiveSubheadline,
                LiveBannerColor = this.LiveBannerColor,
                LiveBannerIcon = this.LiveBannerIcon,
                LiveTemperature = this.LiveTemperature,
                LiveCondition = this.LiveCondition,
                LiveCity = this.LiveCity,
                LiveQuote1Name = this.LiveQuote1Name,
                LiveQuote1Value = this.LiveQuote1Value,
                LiveQuote1Change = this.LiveQuote1Change,
                LiveQuote1IsUp = this.LiveQuote1IsUp,
                LiveQuote2Name = this.LiveQuote2Name,
                LiveQuote2Value = this.LiveQuote2Value,
                LiveQuote2Change = this.LiveQuote2Change,
                LiveQuote2IsUp = this.LiveQuote2IsUp,
                LiveQuote3Name = this.LiveQuote3Name,
                LiveQuote3Value = this.LiveQuote3Value,
                LiveQuote3Change = this.LiveQuote3Change,
                LiveQuote3IsUp = this.LiveQuote3IsUp,
                StoreAppIcon = this.StoreAppIcon,
                StoreHeadline = this.StoreHeadline,
                StoreAppName = this.StoreAppName,
                StoreRatingPrice = this.StoreRatingPrice,
                StoreBadgeCount = this.StoreBadgeCount,
                StoreTopApp1Icon = this.StoreTopApp1Icon,
                StoreTopApp1Name = this.StoreTopApp1Name,
                StoreTopApp1Sub = this.StoreTopApp1Sub,
                StoreTopApp2Icon = this.StoreTopApp2Icon,
                StoreTopApp2Name = this.StoreTopApp2Name,
                StoreTopApp2Sub = this.StoreTopApp2Sub,
                StoreTopApp3Icon = this.StoreTopApp3Icon,
                StoreTopApp3Name = this.StoreTopApp3Name,
                StoreTopApp3Sub = this.StoreTopApp3Sub,
                GameIcon = this.GameIcon,
                GameTitle = this.GameTitle,
                GameSubtitle = this.GameSubtitle,
                ExecutablePath = this.ExecutablePath,
                IsLiveTileEnabled = this.IsLiveTileEnabled,
                AnimationType = this.AnimationType,
                AnimationDuration = this.AnimationDuration,
                AnimationDelay = this.AnimationDelay,
                AnimationInterval = this.AnimationInterval,
                Easing = this.Easing,
                TiltIntensity = this.TiltIntensity,
                IconScale = this.IconScale,
                IconSize = this.IconSize,
                IconOffsetX = this.IconOffsetX,
                IconStretch = this.IconStretch,
            };
        }

        [JsonIgnore]
        public string Glyph
        {
            get => IconGlyph;
            set => IconGlyph = value;
        }

        [JsonIgnore]
        public double GlyphSize { get; set; } = 40;

        [JsonIgnore]
        public Brush Background
        {
            get => BackgroundBrush;
            set => BackgroundBrush = value;
        }

        [JsonIgnore]
        public bool ShowTitle { get; set; } = true;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class InstalledAppItem
    {
        public string Name { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string IconGlyph { get; set; } = "📁";
        public string IconVectorPath { get; set; } = string.Empty;
        public string IconImagePath { get; set; } = string.Empty;
        public Brush TileBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0, 120, 215));

        public bool HasIconImage => !string.IsNullOrEmpty(IconImagePath);
    }

    public class AppAlphabetGroup
    {
        public string Letter { get; set; } = string.Empty;
        public System.Collections.ObjectModel.ObservableCollection<InstalledAppItem> Apps { get; set; } = new();
        public bool HasApps => Apps.Count > 0;
    }

    public class AppsViewItem
    {
        public bool IsHeader { get; set; } = false;
        public bool IsApp => !IsHeader;
        public bool IsLetterHeader { get; set; } = false;
        public bool IsFolderHeader { get; set; } = false;
        public string HeaderTitle { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string IconGlyph { get; set; } = "📁";
        public string IconVectorPath { get; set; } = string.Empty;
        public string IconImagePath { get; set; } = string.Empty;
        public Brush TileBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0, 120, 215));
        public string Category { get; set; } = string.Empty;

        public bool HasIconImage => !string.IsNullOrEmpty(IconImagePath);
    }

    public class SearchResultItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Name { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string IconGlyph { get; set; } = "⚙️";
        public string IconVectorPath { get; set; } = string.Empty;
        public string IconImagePath { get; set; } = string.Empty;
        public Brush TileBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
        public bool IsSetting { get; set; } = false;

        public bool HasIconImage => !string.IsNullOrEmpty(IconImagePath) && (System.IO.File.Exists(IconImagePath) || IconImagePath.StartsWith("pack://"));
        public bool HasVectorPath => !string.IsNullOrEmpty(IconVectorPath) && !HasIconImage;
        public bool HasGlyph => !HasIconImage && !HasVectorPath;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}

