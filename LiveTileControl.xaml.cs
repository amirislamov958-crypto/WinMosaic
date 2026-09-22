using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    public partial class LiveTileControl : UserControl
    {
        private bool _isFrontVisible = true;

        // Drag & Drop tracking
        private bool _isMouseDown = false;
        private bool _isDragging = false;
        private Point _mouseDownPos;
        private Point _mouseDownScreenPos;
        private double _startLeft = 0;
        private double _startTop = 0;
        private double _originalModelX = 0;
        private double _originalModelY = 0;
        private LiveTileControl? _hoveredPartner = null;
        private readonly Dictionary<LiveTileControl, Point> _initialPositions = new();

        public bool IsDragging => _isDragging;

        public LiveTileControl()
        {
            InitializeComponent();
            Loaded += LiveTileControl_Loaded;
            Unloaded += (s, e) => LiveTileCoordinator.Unregister(this);
            DataContextChanged += LiveTileControl_DataContextChanged;
            MouseEnter += (s, e) =>
            {
                if (!_isDragging)
                {
                    if (Application.Current?.Resources["ThemeTileHoverBorderBrush"] is Brush b)
                        HoverBorder.BorderBrush = b;
                    HoverBorder.BorderThickness = new Thickness(2);
                }
            };
            MouseLeave += (s, e) => { HoverBorder.BorderThickness = new Thickness(0); };
        }

        private void LiveTileControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateSelectionDisplay();
            if (DataContext is TileModel model)
            {
                model.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(TileModel.IsSelected))
                    {
                        UpdateSelectionDisplay();
                    }
                };
            }
        }

        private void UpdateSelectionDisplay()
        {
            if (DataContext is TileModel model)
            {
                SelectionBadge.Visibility = model.IsSelected ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static readonly Random _rnd = new Random(Guid.NewGuid().GetHashCode());

        private static readonly Dictionary<string, ImageSource> _liveAssetCache = new(StringComparer.OrdinalIgnoreCase);

        public static ImageSource? GetPreloadedAsset(string relativeFile, int decodeWidth = 0)
        {
            if (string.IsNullOrEmpty(relativeFile)) return null;
            if (_liveAssetCache.TryGetValue(relativeFile, out var cached)) return cached;
            try
            {
                string path = MainWindow.GetAssetPath("LiveTiles\\" + relativeFile);
                if (File.Exists(path))
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.UriSource = new Uri(path, UriKind.Absolute);
                    if (decodeWidth > 0) bi.DecodePixelWidth = decodeWidth;
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.EndInit();
                    bi.Freeze();
                    _liveAssetCache[relativeFile] = bi;
                    return bi;
                }
            }
            catch { }
            return null;
        }

        private int _storeSlideIndex = 0;
        private static readonly (string IconFile, string HeadlineRu, string HeadlineEn, string AppNameRu, string AppNameEn, string RatingPriceRu, string RatingPriceEn)[] _storeFeaturedItems = new[]
        {
            // Слайд 1: Minion Rush
            (
                "game_minion.png",
                "Войдите в Лабораторию...", "Enter the Laboratory...",
                "Гадкий Я: Minion Rush", "Despicable Me: Minion Rush",
                "Бесплатно ★★★★★ 12 369", "Free ★★★★★ 12,369"
            ),
            // Слайд 2: Dawn of Steel (HD)
            (
                "app_dawn_of_steel.png",
                "Командуйте механи...", "Command the mechs...",
                "Dawn of Steel", "Dawn of Steel",
                "Бесплатно ★★★★★ 14 280", "Free ★★★★★ 14,280"
            ),
            // Слайд 3: Angry Birds Star Wars
            (
                "game_angry_birds.png",
                "Новые герои и битвы", "New heroes and battles",
                "Angry Birds Star Wars", "Angry Birds Star Wars",
                "Бесплатно ★★★★★ 48 120", "Free ★★★★★ 48,120"
            ),
            // Слайд 4: Fruit Ninja
            (
                "game_fruit_ninja.png",
                "Рубите сочные фрукты", "Slice juicy fruits",
                "Fruit Ninja", "Fruit Ninja",
                "Бесплатно ★★★★★ 34 200", "Free ★★★★★ 34,200"
            ),
            // Слайд 5: Asphalt 8
            (
                "game_asphalt_8.png",
                "Скорость и адреналин", "Speed and adrenaline",
                "Asphalt 8: На взлёт", "Asphalt 8: Airborne",
                "Бесплатно ★★★★★ 95 430", "Free ★★★★★ 95,430"
            ),
            // Слайд 6: Minecraft Pocket Edition
            (
                "app_minecraft.png",
                "Стройте и исследуйте...", "Build and explore...",
                "Minecraft: Pocket Edition", "Minecraft: Pocket Edition",
                "Бесплатно ★★★★★ 18 420", "Free ★★★★★ 18,420"
            ),
            // Слайд 7: Jetpack Joyride
            (
                "game_jetpack_joyride.jpg",
                "Пора надеть ранец!", "Suit up with a jetpack!",
                "Jetpack Joyride", "Jetpack Joyride",
                "Бесплатно ★★★★★ 27 890", "Free ★★★★★ 27,890"
            )
        };

        private void UpdateStoreSlide(TileModel model)
        {
            var item = _storeFeaturedItems[_storeSlideIndex % _storeFeaturedItems.Length];
            _storeSlideIndex = (_storeSlideIndex + 1) % _storeFeaturedItems.Length;

            var asset = GetPreloadedAsset(item.IconFile, 0);
            model.StoreAppIconSource = asset;
            model.StoreAppIcon = MainWindow.GetAssetPath("LiveTiles\\" + item.IconFile);
            bool isEn = LocalizationManager.CurrentLanguage == "en";
            model.StoreHeadline = isEn ? item.HeadlineEn : item.HeadlineRu;
            model.StoreAppName = isEn ? item.AppNameEn : item.AppNameRu;
            model.StoreRatingPrice = isEn ? item.RatingPriceEn : item.RatingPriceRu;
            model.StoreBadgeCount = "11";
        }

        private int _gameSlideIndex = 0;
        private static readonly (string IconFile, string Title, string Subtitle)[] _gameSlideItems = new[]
        {
            ("game_minion.png", "Minion Rush", "Gameloft"),
            ("game_fruit_ninja.png", "Fruit Ninja", "Xbox Live"),
            ("game_angry_birds.png", "Angry Birds", "Xbox Live"),
            ("game_jetpack_joyride.jpg", "Jetpack Joyride", "Xbox Live"),
            ("game_asphalt_8.png", "Asphalt 8", "Gameloft")
        };

        private void UpdateGameSlide(TileModel model)
        {
            var item = _gameSlideItems[_gameSlideIndex % _gameSlideItems.Length];
            _gameSlideIndex++;

            var asset = GetPreloadedAsset(item.IconFile, 168);
            model.GameIconSource = asset;
            model.GameIcon = MainWindow.GetAssetPath("LiveTiles\\" + item.IconFile);
            model.GameTitle = item.Title;
            model.GameSubtitle = item.Subtitle;
        }

        private int _sportsSlideIndex = 0;
        private static readonly (string ImageFile, string HeadlineRu, string HeadlineEn, string SubheadlineRu, string SubheadlineEn)[] _sportsSlideItems = new[]
        {
            ("sports_shirokov.png", "Роман Широков: Быть", "Premier League: Matchday Preview", "капитаном — это...", "Title race heats up..."),
            ("sports_basketball.jpg", "Помочь друг другу", "Championship Highlights", "", "Final score updates")
        };

        private void UpdateSportsSlide(TileModel model)
        {
            var item = _sportsSlideItems[_sportsSlideIndex % _sportsSlideItems.Length];
            _sportsSlideIndex = (_sportsSlideIndex + 1) % _sportsSlideItems.Length;

            string path = MainWindow.GetAssetPath("LiveTiles\\" + item.ImageFile);
            if (File.Exists(path))
            {
                model.LiveImagePath = path;
            }
            bool isEn = LocalizationManager.CurrentLanguage == "en";
            model.LiveHeadline = isEn ? item.HeadlineEn : item.HeadlineRu;
            model.LiveSubheadline = isEn ? item.SubheadlineEn : item.SubheadlineRu;
            model.LiveBannerColor = "#5C2D91";
            model.LiveBannerIcon = "M19 5h-2V3H7v2H5c-1.1 0-2 .9-2 2v1c0 2.55 1.92 4.63 4.39 4.94A5.01 5.01 0 0 0 11 15.9V19H7v2h10v-2h-4v-3.1a5.01 5.01 0 0 0 3.61-2.96C19.08 12.63 21 10.55 21 8V7c0-1.1-.9-2-2-2zM5 8V7h2v3.82C5.84 10.4 5 9.3 5 8zm14 0c0 1.3-.84 2.4-2 2.82V7h2v1z";
        }

        private int _newsSlideIndex = 0;
        private static readonly (string ImageFile, string HeadlineRu, string HeadlineEn, string SubheadlineRu, string SubheadlineEn)[] _newsSlideItems = new[]
        {
            ("news_pope.png", "Последняя молитва", "Global Tech Summit 2026", "", "Key announcements unveiled"),
            ("news_kuril.png", "Волна дошла до Курил", "Pacific Weather Advisory", "", "Tsunami warning lifted")
        };

        private void UpdateNewsSlide(TileModel model)
        {
            var item = _newsSlideItems[_newsSlideIndex % _newsSlideItems.Length];
            _newsSlideIndex = (_newsSlideIndex + 1) % _newsSlideItems.Length;

            string path = MainWindow.GetAssetPath("LiveTiles\\" + item.ImageFile);
            if (File.Exists(path))
            {
                model.LiveImagePath = path;
            }
            bool isEn = LocalizationManager.CurrentLanguage == "en";
            model.LiveHeadline = isEn ? item.HeadlineEn : item.HeadlineRu;
            model.LiveSubheadline = isEn ? item.SubheadlineEn : item.SubheadlineRu;
            model.LiveBannerColor = "#A20025";
            model.LiveBannerIcon = "M20,11H4V8H20M20,15H13V13H20M20,19H13V17H20M11,19H4V13H11M20,3H4C2.89,3 2,3.89 2,5V19A2,2 0 0,0 4,21H20A2,2 0 0,0 22,19V5C22,3.89 21.1,3 20,3Z";
        }

        private void LiveTileControl_Loaded(object sender, RoutedEventArgs e)
        {
            double h = ActualHeight > 0 ? ActualHeight : (DataContext is TileModel m ? m.PixelHeight : 150);
            BackTranslate.Y = h;

            if (DataContext is TileModel tm)
            {
                if (tm.IsStoreTemplate)
                {
                    foreach (var s in _storeFeaturedItems) GetPreloadedAsset(s.IconFile, 168);
                    if (tm.StoreAppIconSource == null) UpdateStoreSlide(tm);
                }
                if (tm.IsGamesTemplate)
                {
                    foreach (var g in _gameSlideItems) GetPreloadedAsset(g.IconFile, 168);
                    if (tm.GameIconSource == null) UpdateGameSlide(tm);
                }
                if (tm.IsSportsTile)
                {
                    foreach (var s in _sportsSlideItems) GetPreloadedAsset(s.ImageFile, 0);
                    if (string.IsNullOrEmpty(tm.LiveSubheadline)) UpdateSportsSlide(tm);
                }
                if (tm.IsNewsTile)
                {
                    foreach (var n in _newsSlideItems) GetPreloadedAsset(n.ImageFile, 0);
                    if (string.IsNullOrEmpty(tm.LiveHeadline)) UpdateNewsSlide(tm);
                }
            }

            LiveTileCoordinator.Register(this);
        }

        public void TriggerLiveRoll()
        {
            ExecuteLiveRoll(null);
        }

        public void ExecuteLiveRoll(Action? onCompleted)
        {
            // Не анимировать, если окно скрыто, идет перетаскивание или экран не полностью открыт
            if (MainWindow.Instance == null || MainWindow.Instance.Visibility != Visibility.Visible || MainWindow.Instance.CurrentState != ScreenState.Open)
            {
                onCompleted?.Invoke();
                return;
            }

            if (_isDragging || _isMouseDown)
            {
                onCompleted?.Invoke();
                return;
            }

            if (DataContext is not TileModel model || !model.IsLiveTileEnabled)
            {
                onCompleted?.Invoke();
                return;
            }

            if (model.Title.Equals("Mail", StringComparison.OrdinalIgnoreCase) || model.Title.Equals("Почта", StringComparison.OrdinalIgnoreCase))
            {
                model.LiveText = string.Empty;
                model.IsLiveTileEnabled = false;
                onCompleted?.Invoke();
                return;
            }

            if (model.LiveText != null && (model.LiveText.Contains("3 новых письма") || model.LiveText.Contains("Отчет по проекту")))
            {
                model.LiveText = string.Empty;
                model.IsLiveTileEnabled = false;
                onCompleted?.Invoke();
                return;
            }

            // Проверяем наличие любого живого контента (фото-баннер, котировки, погода, магазин, игры или текст)
            bool hasLiveContent = model.IsPhotoBannerTemplate || model.IsFinanceTemplate || model.IsWeatherTemplate || model.IsStoreTemplate || model.IsGamesTemplate || !string.IsNullOrEmpty(model.LiveText);
            if (!hasLiveContent)
            {
                onCompleted?.Invoke();
                return;
            }

            double h = ActualHeight > 0 ? ActualHeight : model.PixelHeight;
            if (h <= 0) h = 150;

            // Анимация 720–800 мс со сглаживанием CubicEase EaseInOut
            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
            var duration = TimeSpan.FromMilliseconds(_rnd.Next(720, 800));

            // Защитный таймер: гарантирует вызов onCompleted даже при непредвиденном сбросе анимаций WPF
            var safetyTimer = new DispatcherTimer { Interval = duration + TimeSpan.FromMilliseconds(300) };
            bool completedCalled = false;
            Action completeAction = () =>
            {
                if (completedCalled) return;
                completedCalled = true;
                safetyTimer.Stop();
                onCompleted?.Invoke();
            };
            safetyTimer.Tick += (s, ev) => completeAction();
            safetyTimer.Start();

            if (_isFrontVisible)
            {
                // Фаза 1: Неспешный сдвиг лицевой части вверх (0 -> -h), появление живого контента снизу (h -> 0)
                BackTranslate.Y = h;

                var animFront = new DoubleAnimation(0, -h, duration) { EasingFunction = ease };
                var animBack = new DoubleAnimation(h, 0, duration) { EasingFunction = ease };

                animBack.Completed += (s, e) =>
                {
                    BackTranslate.Y = 0;
                    FrontTranslate.Y = -h;
                    BackTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                    FrontTranslate.BeginAnimation(TranslateTransform.YProperty, null);

                    if (model.IsGamesTemplate && !string.IsNullOrEmpty(model.GameIcon))
                    {
                        model.IconImagePath = model.GameIcon;
                    }

                    completeAction();
                };

                FrontTranslate.BeginAnimation(TranslateTransform.YProperty, animFront);
                BackTranslate.BeginAnimation(TranslateTransform.YProperty, animBack);

                _isFrontVisible = false;
            }
            else
            {
                // Фаза 2: Аутентичный непрерывный ролл Windows 8.1 (конвейерный сдвиг снизу вверх)
                // Живой контент уходит вверх (0 -> -h), лицевая сторона входит снизу (h -> 0)
                FrontTranslate.Y = h;

                var animBack = new DoubleAnimation(0, -h, duration) { EasingFunction = ease };
                var animFront = new DoubleAnimation(h, 0, duration) { EasingFunction = ease };

                animFront.Completed += (s, e) =>
                {
                    FrontTranslate.Y = 0;
                    BackTranslate.Y = h;
                    FrontTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                    BackTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                    completeAction();

                    // Обновляем следующий слайд, пока обратная сторона полностью скрыта за экраном (0 лагов и 0 дропов кадров)
                    if (model.IsStoreTemplate) UpdateStoreSlide(model);
                    if (model.IsGamesTemplate) UpdateGameSlide(model);
                    if (model.IsSportsTile) UpdateSportsSlide(model);
                    if (model.IsNewsTile) UpdateNewsSlide(model);
                };

                BackTranslate.BeginAnimation(TranslateTransform.YProperty, animBack);
                FrontTranslate.BeginAnimation(TranslateTransform.YProperty, animFront);

                _isFrontVisible = true;
            }
        }

        // =================== Authentic Metro Visual State Reset ===================
        public void ResetVisualState()
        {
            HoverBorder.BorderThickness = new Thickness(0);
            BeginAnimation(OpacityProperty, null);
            EntranceTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            EntranceTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            Opacity = 1.0;
            EntranceTranslate.X = 0;
            EntranceTranslate.Y = 0;

            // Сброс положения живых панелей
            FrontTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            BackTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            FrontTranslate.Y = 0;
            double h = ActualHeight > 0 ? ActualHeight : (DataContext is TileModel m ? m.PixelHeight : 150);
            BackTranslate.Y = h;
            _isFrontVisible = true;

            TileScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            TileScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            TileScale.ScaleX = 1.0;
            TileScale.ScaleY = 1.0;

            TileSkew.BeginAnimation(SkewTransform.AngleXProperty, null);
            TileSkew.BeginAnimation(SkewTransform.AngleYProperty, null);
            TileSkew.AngleX = 0;
            TileSkew.AngleY = 0;
        }

        public void TriggerEntranceAnimation(int delayMs = 0, double distance = 48.0, int durationMs = 200)
        {
            EntranceTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            BeginAnimation(OpacityProperty, null);
            Opacity = 1.0;

            EntranceTranslate.X = distance;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            // Аппаратный сверхплавный сдвиг плитки справа налево (48px -> 0px)
            // Без анимации UserControl.Opacity, что полностью исключает выделение промежуточных текстур D3D и лаги
            var slideAnim = new DoubleAnimation
            {
                From = distance,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                EasingFunction = ease,
                FillBehavior = FillBehavior.HoldEnd
            };

            slideAnim.Completed += (s, e) =>
            {
                EntranceTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                EntranceTranslate.X = 0;
            };

            EntranceTranslate.BeginAnimation(TranslateTransform.XProperty, slideAnim);
        }

        // =================== 3D Perspective Tilt Physics & Dragging ===================
        private void Tile_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            _isMouseDown = true;
            _isDragging = false;
            _hoveredPartner = null;
            _mouseDownPos = e.GetPosition(this);
            try
            {
                _mouseDownScreenPos = PointToScreen(_mouseDownPos);
            }
            catch
            {
                _mouseDownScreenPos = _mouseDownPos;
            }
            _startLeft = Canvas.GetLeft(this);
            _startTop = Canvas.GetTop(this);

            if (DataContext is TileModel m)
            {
                _originalModelX = m.X;
                _originalModelY = m.Y;
            }
            else
            {
                _originalModelX = _startLeft;
                _originalModelY = _startTop;
            }

            // Запоминаем исходные позиции всех плиток на холсте
            _initialPositions.Clear();
            if (Parent is Canvas canvas)
            {
                foreach (var child in canvas.Children.OfType<LiveTileControl>())
                {
                    if (child.DataContext is TileModel cm)
                    {
                        _initialPositions[child] = new Point(cm.X, cm.Y);
                    }
                    else
                    {
                        _initialPositions[child] = new Point(Canvas.GetLeft(child), Canvas.GetTop(child));
                    }
                }
            }

            double relX = (_mouseDownPos.X / Math.Max(1, ActualWidth)) - 0.5;
            double relY = (_mouseDownPos.Y / Math.Max(1, ActualHeight)) - 0.5;

            var scaleAnim = new DoubleAnimation(0.96, TimeSpan.FromMilliseconds(70));
            var skewXAnim = new DoubleAnimation(relY * -6.0, TimeSpan.FromMilliseconds(70));
            var skewYAnim = new DoubleAnimation(relX * 6.0, TimeSpan.FromMilliseconds(70));

            TileScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            TileScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            TileSkew.BeginAnimation(SkewTransform.AngleXProperty, skewXAnim);
            TileSkew.BeginAnimation(SkewTransform.AngleYProperty, skewYAnim);
        }

        private void Tile_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isMouseDown || e.LeftButton != MouseButtonState.Pressed) return;

            Point curScreen;
            try
            {
                curScreen = PointToScreen(e.GetPosition(this));
            }
            catch
            {
                curScreen = e.GetPosition(this);
            }

            Vector diff = curScreen - _mouseDownScreenPos;

            if (!_isDragging && (Math.Abs(diff.X) > 7 || Math.Abs(diff.Y) > 7))
            {
                _isDragging = true;
                HoverBorder.BorderThickness = new Thickness(0);
                CaptureMouse();

                // Сброс активных анимаций Canvas
                BeginAnimation(Canvas.LeftProperty, null);
                BeginAnimation(Canvas.TopProperty, null);

                // Сброс наклона при перетаскивании
                TileSkew.BeginAnimation(SkewTransform.AngleXProperty, null);
                TileSkew.AngleX = 0;
                TileSkew.BeginAnimation(SkewTransform.AngleYProperty, null);
                TileSkew.AngleY = 0;

                // Плавное приподнимание плитки (масштаб 1.06x, легкая прозрачность 0.90)
                var liftScale = new DoubleAnimation(1.06, TimeSpan.FromMilliseconds(140))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                TileScale.BeginAnimation(ScaleTransform.ScaleXProperty, liftScale);
                TileScale.BeginAnimation(ScaleTransform.ScaleYProperty, liftScale);

                var liftOpacity = new DoubleAnimation(0.90, TimeSpan.FromMilliseconds(140));
                BeginAnimation(OpacityProperty, liftOpacity);

                Panel.SetZIndex(this, 10000);
            }

            if (_isDragging)
            {
                double tileW = (DataContext is TileModel m) ? m.PixelWidth : (ActualWidth > 0 ? ActualWidth : 150);
                double tileH = (DataContext is TileModel m2) ? m2.PixelHeight : (ActualHeight > 0 ? ActualHeight : 150);
                // Строгий лимит сверху (0) и снизу (адаптивно под доступную высоту холста)
                double maxAllowedY = Math.Max(0, MainWindow.GetAvailableCanvasHeight() - tileH);

                double newX = Math.Max(0, _startLeft + diff.X);
                double newY = Math.Clamp(_startTop + diff.Y, 0, maxAllowedY);
                Canvas.SetLeft(this, newX);
                Canvas.SetTop(this, newY);

                // Живое вытеснение соседа (Live displacement с защитой от наложений)
                if (Parent is Canvas canvas)
                {
                    Rect dragRect = new Rect(newX, newY, tileW, tileH);
                    Point dragCenter = new Point(newX + tileW / 2.0, newY + tileH / 2.0);

                    LiveTileControl? candidate = null;
                    double bestOverlap = 0;

                    foreach (var child in canvas.Children.OfType<LiveTileControl>())
                    {
                        if (child == this) continue;
                        if (!_initialPositions.TryGetValue(child, out var initPos)) continue;

                        double cw = (child.DataContext is TileModel cm) ? cm.PixelWidth : (child.ActualWidth > 0 ? child.ActualWidth : 150);
                        double ch = (child.DataContext is TileModel cm2) ? cm2.PixelHeight : (child.ActualHeight > 0 ? child.ActualHeight : 150);

                        Rect restingRect = new Rect(initPos.X, initPos.Y, cw, ch);

                        bool isOver = restingRect.Contains(dragCenter);
                        if (!isOver)
                        {
                            Rect overlap = Rect.Intersect(dragRect, restingRect);
                            if (!overlap.IsEmpty)
                            {
                                double area = overlap.Width * overlap.Height;
                                double minArea = Math.Min(tileW * tileH, cw * ch);
                                if (area > 0.50 * minArea && area > bestOverlap)
                                {
                                    bestOverlap = area;
                                    isOver = true;
                                }
                            }
                        }

                        // Проверяем, могут ли плитки физически поменяться местами без конфликтов
                        if (isOver && CanSwap(this, child, new Point(_originalModelX, _originalModelY), initPos, canvas, _initialPositions))
                        {
                            candidate = child;
                            break;
                        }
                    }

                    if (candidate != _hoveredPartner)
                    {
                        // Возвращаем старого партнера на его исходное место
                        if (_hoveredPartner != null && _initialPositions.TryGetValue(_hoveredPartner, out var oldPos))
                        {
                            _hoveredPartner.AnimateTo(oldPos.X, oldPos.Y, 200);
                        }

                        _hoveredPartner = candidate;

                        // Смещаем нового соседа на наше освободившееся исходное место
                        if (_hoveredPartner != null)
                        {
                            _hoveredPartner.AnimateTo(_originalModelX, _originalModelY, 220);
                        }
                    }
                }
            }
        }

        private void Tile_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            if (_isDragging)
            {
                _isDragging = false;
                _isMouseDown = false;
                ReleaseMouseCapture();

                double curX = Canvas.GetLeft(this);
                double curY = Canvas.GetTop(this);

                double tileW = (DataContext is TileModel tm) ? tm.PixelWidth : (ActualWidth > 0 ? ActualWidth : 150);
                double tileH = (DataContext is TileModel tm2) ? tm2.PixelHeight : (ActualHeight > 0 ? ActualHeight : 150);

                double targetX = _originalModelX;
                double targetY = _originalModelY;

                Canvas? parentCanvas = Parent as Canvas;

                if (DataContext is TileModel myModel)
                {
                    Point snapped = SnapTargetPosition(curX, curY, myModel);
                    Rect snappedRect = new Rect(snapped.X, snapped.Y, tileW, tileH);

                    // Проверяем, занят ли целевой слот (snapped) плиткой
                    LiveTileControl? targetSlotTile = null;
                    if (parentCanvas != null)
                    {
                        foreach (var child in parentCanvas.Children.OfType<LiveTileControl>())
                        {
                            if (child == this) continue;
                            if (child.DataContext is not TileModel cm) continue;

                            Point initPos = _initialPositions.TryGetValue(child, out var p) ? p : new Point(cm.X, cm.Y);
                            Rect childRect = new Rect(initPos.X, initPos.Y, cm.PixelWidth, cm.PixelHeight);

                            Rect inter = Rect.Intersect(snappedRect, childRect);
                            if (!inter.IsEmpty && inter.Width > 15 && inter.Height > 15)
                            {
                                targetSlotTile = child;
                                break;
                            }
                        }
                    }

                    if (targetSlotTile != null && targetSlotTile.DataContext is TileModel partnerModel)
                    {
                        // Целевой слот занят -> производим обмен местами, если допустимо
                        Point partnerOrigPos = _initialPositions.TryGetValue(targetSlotTile, out var p) ? p : new Point(partnerModel.X, partnerModel.Y);

                        if (CanSwap(this, targetSlotTile, new Point(_originalModelX, _originalModelY), partnerOrigPos, parentCanvas!, _initialPositions))
                        {
                            myModel.X = partnerOrigPos.X;
                            myModel.Y = partnerOrigPos.Y;
                            partnerModel.X = _originalModelX;
                            partnerModel.Y = _originalModelY;

                            targetSlotTile.AnimateTo(_originalModelX, _originalModelY, 200);
                        }
                        else
                        {
                            targetSlotTile.AnimateTo(partnerOrigPos.X, partnerOrigPos.Y, 200);
                            myModel.X = _originalModelX;
                            myModel.Y = _originalModelY;
                        }
                    }
                    else
                    {
                        // Целевой слот СВОБОДЕН!
                        // Возвращаем временно сдвинутого соседа на его исходную позицию
                        if (_hoveredPartner != null && _initialPositions.TryGetValue(_hoveredPartner, out var pOrig))
                        {
                            _hoveredPartner.AnimateTo(pOrig.X, pOrig.Y, 200);
                        }

                        // Размещаем плитку точно в свободный слот
                        myModel.X = snapped.X;
                        myModel.Y = snapped.Y;
                    }

                    if (MainWindow.Instance != null)
                    {
                        MainWindow.Instance.SanitizeTileLayout(MainWindow.Instance.StartTiles);
                        MainWindow.Instance.RelayoutTileControls(true);
                        MainWindow.Instance.SaveLayoutConfig();
                    }

                    targetX = myModel.X;
                    targetY = myModel.Y;
                }

                _hoveredPartner = null;
                _initialPositions.Clear();

                // Плавное возвращение масштаба и прозрачности
                var dropScale = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                TileScale.BeginAnimation(ScaleTransform.ScaleXProperty, dropScale);
                TileScale.BeginAnimation(ScaleTransform.ScaleYProperty, dropScale);

                var dropOpacity = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200));
                BeginAnimation(OpacityProperty, dropOpacity);

                // Плавное приземление (glide) на финальную позицию
                AnimateTo(targetX, targetY, 200, () =>
                {
                    Panel.SetZIndex(this, 1);
                    HoverBorder.BorderThickness = IsMouseOver ? new Thickness(2) : new Thickness(0);
                    MainWindow.Instance?.SaveLayoutConfig();
                });

                e.Handled = true;
                return;
            }

            if (_isMouseDown)
            {
                _isMouseDown = false;

                var spring = new ElasticEase { Oscillations = 1, Springiness = 4, EasingMode = EasingMode.EaseOut };
                var resetScale = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = spring };
                var resetSkew = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200));

                TileScale.BeginAnimation(ScaleTransform.ScaleXProperty, resetScale);
                TileScale.BeginAnimation(ScaleTransform.ScaleYProperty, resetScale);
                TileSkew.BeginAnimation(SkewTransform.AngleXProperty, resetSkew);
                TileSkew.BeginAnimation(SkewTransform.AngleYProperty, resetSkew);

                HoverBorder.BorderThickness = IsMouseOver ? new Thickness(2) : new Thickness(0);

                LaunchTileTarget();
                e.Handled = true;
            }
        }

        public void CancelDrag()
        {
            if (!_isDragging) return;

            _isDragging = false;
            _isMouseDown = false;
            ReleaseMouseCapture();

            if (_hoveredPartner != null && _initialPositions.TryGetValue(_hoveredPartner, out var pPos))
            {
                _hoveredPartner.AnimateTo(pPos.X, pPos.Y, 200);
            }
            _hoveredPartner = null;

            foreach (var kvp in _initialPositions)
            {
                if (kvp.Key != this)
                {
                    kvp.Key.AnimateTo(kvp.Value.X, kvp.Value.Y, 200);
                }
            }
            _initialPositions.Clear();

            var resetScale = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            TileScale.BeginAnimation(ScaleTransform.ScaleXProperty, resetScale);
            TileScale.BeginAnimation(ScaleTransform.ScaleYProperty, resetScale);

            var resetOpacity = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200));
            BeginAnimation(OpacityProperty, resetOpacity);

            AnimateTo(_originalModelX, _originalModelY, 200, () =>
            {
                Panel.SetZIndex(this, 1);
                HoverBorder.BorderThickness = IsMouseOver ? new Thickness(2) : new Thickness(0);
            });
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            if (_isDragging)
            {
                CancelDrag();
            }
        }

        public void AnimateTo(double targetX, double targetY, double durationMs = 200, Action? onCompleted = null)
        {
            if (durationMs <= 0)
            {
                BeginAnimation(Canvas.LeftProperty, null);
                BeginAnimation(Canvas.TopProperty, null);
                Canvas.SetLeft(this, targetX);
                Canvas.SetTop(this, targetY);
                onCompleted?.Invoke();
                return;
            }

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            double currentX = Canvas.GetLeft(this);
            if (double.IsNaN(currentX)) currentX = 0;
            double currentY = Canvas.GetTop(this);
            if (double.IsNaN(currentY)) currentY = 0;

            BeginAnimation(Canvas.LeftProperty, null);
            BeginAnimation(Canvas.TopProperty, null);
            Canvas.SetLeft(this, currentX);
            Canvas.SetTop(this, currentY);

            var animX = new DoubleAnimation
            {
                From = currentX,
                To = targetX,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };

            var animY = new DoubleAnimation
            {
                From = currentY,
                To = targetY,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };

            int completedCount = 0;
            void CheckCompleted()
            {
                completedCount++;
                if (completedCount >= 2)
                {
                    BeginAnimation(Canvas.LeftProperty, null);
                    BeginAnimation(Canvas.TopProperty, null);
                    Canvas.SetLeft(this, targetX);
                    Canvas.SetTop(this, targetY);
                    onCompleted?.Invoke();
                }
            }

            animX.Completed += (s, e) => CheckCompleted();
            animY.Completed += (s, e) => CheckCompleted();

            BeginAnimation(Canvas.LeftProperty, animX);
            BeginAnimation(Canvas.TopProperty, animY);
        }

        private static bool CanSwap(LiveTileControl a, LiveTileControl b, Point posA, Point posB, Canvas canvas, Dictionary<LiveTileControl, Point> initialPositions)
        {
            if (a.DataContext is not TileModel modelA || b.DataContext is not TileModel modelB)
                return false;

            double wA = modelA.PixelWidth;
            double hA = modelA.PixelHeight;
            double wB = modelB.PixelWidth;
            double hB = modelB.PixelHeight;

            // 1. Проверка допустимости размещения A на месте B (posB)
            double maxCanvasH = MainWindow.GetAvailableCanvasHeight();
            if (posB.X < 0 || posB.Y < 0 || posB.Y + hA > maxCanvasH + 8) return false;
            if (modelA.Size == TileSize.Wide || modelA.Size == TileSize.Large || wA >= 250)
            {
                int blockIdx = MainWindow.GetNearestBlockIndex(posB.X);
                if (Math.Abs(posB.X - MainWindow.GetBlockX(blockIdx)) > 15) return false;
            }
            if (modelA.Size == TileSize.Large || hA >= 250)
            {
                double rem = posB.Y % 156.0;
                if (rem > 10 && rem < 146) return false;
            }

            // 2. Проверка допустимости размещения B на месте A (posA)
            if (posA.X < 0 || posA.Y < 0 || posA.Y + hB > maxCanvasH + 8) return false;
            if (modelB.Size == TileSize.Wide || modelB.Size == TileSize.Large || wB >= 250)
            {
                int blockIdx = MainWindow.GetNearestBlockIndex(posA.X);
                if (Math.Abs(posA.X - MainWindow.GetBlockX(blockIdx)) > 15) return false;
            }
            if (modelB.Size == TileSize.Large || hB >= 250)
            {
                double rem = posA.Y % 156.0;
                if (rem > 10 && rem < 146) return false;
            }

            // 3. Полная проверка наложений на ВСЕ остальные плитки холста
            Rect rectAAtB = new Rect(posB.X, posB.Y, wA, hA);
            Rect rectBAtA = new Rect(posA.X, posA.Y, wB, hB);

            foreach (var child in canvas.Children.OfType<LiveTileControl>())
            {
                if (child == a || child == b) continue;
                if (child.DataContext is not TileModel cm) continue;

                Point childPos = initialPositions.TryGetValue(child, out var p) ? p : new Point(cm.X, cm.Y);
                Rect childRect = new Rect(childPos.X, childPos.Y, cm.PixelWidth, cm.PixelHeight);

                Rect interA = Rect.Intersect(rectAAtB, childRect);
                if (!interA.IsEmpty && interA.Width > 4 && interA.Height > 4) return false;

                Rect interB = Rect.Intersect(rectBAtA, childRect);
                if (!interB.IsEmpty && interB.Width > 4 && interB.Height > 4) return false;
            }

            return true;
        }

        private static Point SnapTargetPosition(double curX, double curY, TileModel model)
        {
            double w = model.PixelWidth;
            double h = model.PixelHeight;

            double snappedX;
            double snappedY;

            int maxRows = MainWindow.GetMaxStandardRows();
            double maxStandardY = Math.Max(0, (maxRows - 1) * 156.0);
            double maxLargeY = Math.Max(0, (maxRows - 2) * 156.0);
            double maxSmallY = Math.Max(0, (maxRows * 2 - 1) * 78.0);

            snappedX = MainWindow.SnapXForTileSize(curX, model.Size);

            if (model.Size == TileSize.Large || h >= 250)
            {
                snappedY = Math.Clamp(Math.Round(curY / 156.0) * 156.0, 0, maxLargeY);
            }
            else if (model.Size == TileSize.Small)
            {
                snappedY = Math.Clamp(Math.Round(curY / 78.0) * 78.0, 0, maxSmallY);
            }
            else
            {
                snappedY = Math.Clamp(Math.Round(curY / 156.0) * 156.0, 0, maxStandardY);
            }

            return new Point(snappedX, snappedY);
        }

        private void LaunchTileTarget()
        {
            if (DataContext is not TileModel model) return;

            string title = model.Title.Trim().ToLowerInvariant();

            // Если это плитка Desktop - просто скрываем начальный экран
            if (title == "desktop" || title == "рабочий стол")
            {
                MainWindow.Instance?.CloseScreenAnimated();
                return;
            }

            // Прямой путь к приложению, файлу или интернет-ссылке (URL)
            if (!string.IsNullOrEmpty(model.ExecutablePath))
            {
                string target = model.ExecutablePath.Trim();
                try
                {
                    // Если ссылка начинается с www., добавляем https://
                    if (target.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                    {
                        target = "https://" + target;
                    }

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = target,
                        UseShellExecute = true
                    });
                    MainWindow.Instance?.CloseScreenAnimated();
                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LaunchTileTarget] Error executing '{target}': {ex.Message}");
                }
            }

            // Аутентичные Metro протоколы Windows для системных плиток
            string? protocol = title switch
            {
                "mail" or "почта" => "mailto:",
                "calendar" or "календарь" => "outlookcal:",
                "store" or "магазин" or "windows store" => "ms-windows-store:",
                "weather" or "погода" => "bingweather:",
                "maps" or "карты" => "bingmaps:",
                "news" or "новости" => "https://www.msn.com/ru-ru/news",
                "photos" or "фотографии" => "ms-photos:",
                "sports" or "спорт" => "https://www.msn.com/ru-ru/sports",
                "money" or "финансы" => "https://www.msn.com/ru-ru/money",
                "music" or "музыка" => "mswindowsmusic:",
                "videos" or "video" or "видео" => "mswindowsvideo:",
                "onenote" => "onenote:",
                "settings" or "pc settings" or "параметры" or "параметры пк" => "ms-settings:",
                "help+tips" or "справка+советы" => "ms-get-started:",
                "onedrive" => "onedrive:",
                "skype" => "skype:",
                "people" or "люди" => "ms-people:",
                "calculator" or "калькулятор" => "calc.exe",
                "desktop" or "рабочий стол" => "explorer.exe",
                "internet explorer" or "ie" => "https://www.google.com",
                "camera" or "камера" => "microsoft.windows.camera:",
                "games" or "игры" => @"C:\Program Files (x86)\Steam\steam.exe",
                "reading list" or "список для чтения" => "microsoft-edge:",
                "food & drink" or "кулинария" => "https://www.msn.com/ru-ru/foodanddrink",
                "health & fitness" or "здоровье и фитнес" => "https://www.msn.com/ru-ru/health",
                _ => null
            };

            if (!string.IsNullOrEmpty(protocol))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(protocol) { UseShellExecute = true });
                    MainWindow.Instance?.CloseScreenAnimated();
                    return;
                }
                catch
                {
                    // Fallbacks if UWP protocol is not supported on this Windows version
                    try
                    {
                        if (title.Contains("weather") || title.Contains("погода"))
                            Process.Start(new ProcessStartInfo("https://www.msn.com/ru-ru/weather") { UseShellExecute = true });
                        else if (title.Contains("maps") || title.Contains("карты"))
                            Process.Start(new ProcessStartInfo("https://maps.google.com") { UseShellExecute = true });
                        else if (title.Contains("music") || title.Contains("video") || title.Contains("музыка") || title.Contains("видео"))
                            Process.Start(new ProcessStartInfo("wmplayer.exe") { UseShellExecute = true });
                        else if (title.Contains("photo") || title.Contains("фото"))
                            Process.Start(new ProcessStartInfo("mspaint.exe") { UseShellExecute = true });
                        else
                            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
                    }
                    catch { }
                    MainWindow.Instance?.CloseScreenAnimated();
                }
            }
            else
            {
                MainWindow.Instance?.CloseScreenAnimated();
            }
        }

        // =================== Правый клик / Контекстное меню ===================
        private void Tile_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not TileModel model) return;

            // Создаем всплывающее Metro контекстное меню
            var cm = new ContextMenu
            {
                Background = new SolidColorBrush(Color.FromRgb(27, 58, 87)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                BorderThickness = new Thickness(1)
            };

            // 1. Подменю «Изменить размер»
            var resizeItem = new MenuItem { Header = "Изменить размер", Foreground = Brushes.White };

            var smallItem = new MenuItem { Header = "◽ Мелкий (71×71)", Foreground = Brushes.White };
            smallItem.Click += (s, ev) => SetSizeAndSave(TileSize.Small);
            resizeItem.Items.Add(smallItem);

            var medItem = new MenuItem { Header = "◻ Средний (150×150)", Foreground = Brushes.White };
            medItem.Click += (s, ev) => SetSizeAndSave(TileSize.Medium);
            resizeItem.Items.Add(medItem);

            var wideItem = new MenuItem { Header = "▭ Широкий (308×150)", Foreground = Brushes.White };
            wideItem.Click += (s, ev) => SetSizeAndSave(TileSize.Wide);
            resizeItem.Items.Add(wideItem);

            var largeItem = new MenuItem { Header = "◼ Большой (308×308)", Foreground = Brushes.White };
            largeItem.Click += (s, ev) => SetSizeAndSave(TileSize.Large);
            resizeItem.Items.Add(largeItem);

            cm.Items.Add(resizeItem);

            // 2. Открепить от начального экрана
            var unpinItem = new MenuItem { Header = "Открепить от начального экрана", Foreground = Brushes.White };
            unpinItem.Click += (s, ev) =>
            {
                MainWindow.Instance?.UnpinTile(model);
            };
            cm.Items.Add(unpinItem);

            // 3. Динамические плитки
            var liveItem = new MenuItem
            {
                Header = model.IsLiveTileEnabled ? "Отключить динамические плитки" : "Включить динамические плитки",
                Foreground = Brushes.White
            };
            liveItem.Click += (s, ev) =>
            {
                model.IsLiveTileEnabled = !model.IsLiveTileEnabled;
                MainWindow.Instance?.SaveLayoutConfig();
            };
            cm.Items.Add(liveItem);

            cm.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)) });

            // 4. Сменить значок из каталога Metro
            var changeIconItem = new MenuItem { Header = "Сменить значок (Каталог Metro)...", Foreground = Brushes.White };
            changeIconItem.Click += (s, ev) =>
            {
                try
                {
                    var dlg = new Win8StartScreen.Views.MetroIconPickerDialog
                    {
                        Owner = Application.Current?.MainWindow
                    };
                    if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.SelectedIconPath))
                    {
                        model.IconImagePath = dlg.SelectedIconPath;
                        model.IconVectorPath = string.Empty;
                        model.IconGlyph = string.Empty;
                        if (dlg.ChosenIconSize > 0) model.IconSize = dlg.ChosenIconSize;
                        MainWindow.Instance?.SaveLayoutConfig();
                    }
                }
                catch { }
            };
            cm.Items.Add(changeIconItem);

            // 5. Выбрать файл значка
            var browseIconItem = new MenuItem { Header = "Выбрать файл значка...", Foreground = Brushes.White };
            browseIconItem.Click += (s, ev) =>
            {
                try
                {
                    var ofd = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "Выберите значок для плитки",
                        Filter = "Изображения и значки (*.png;*.ico;*.jpg;*.jpeg;*.bmp;*.webp;*.svg)|*.png;*.ico;*.jpg;*.jpeg;*.bmp;*.webp;*.svg|Все файлы (*.*)|*.*",
                        CheckFileExists = true
                    };
                    if (ofd.ShowDialog() == true && System.IO.File.Exists(ofd.FileName))
                    {
                        model.IconImagePath = ofd.FileName;
                        model.IconVectorPath = string.Empty;
                        model.IconGlyph = string.Empty;
                        MainWindow.Instance?.SaveLayoutConfig();
                    }
                }
                catch { }
            };
            cm.Items.Add(browseIconItem);

            cm.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)) });

            // 6. Открыть в редакторе (Studio)
            var studioItem = new MenuItem { Header = "Редактировать в Studio...", Foreground = Brushes.White };
            studioItem.Click += (s, ev) =>
            {
                MainWindow.Instance?.OpenStudioForTile(model);
            };
            cm.Items.Add(studioItem);

            cm.PlacementTarget = this;
            cm.IsOpen = true;
            e.Handled = true;
        }

        private void SetSizeAndSave(TileSize size)
        {
            if (DataContext is TileModel model)
            {
                model.Size = size;
                if (MainWindow.Instance != null)
                {
                    MainWindow.Instance.SanitizeTileLayout(MainWindow.Instance.StartTiles);
                    MainWindow.Instance.RelayoutTileControls(true);
                    MainWindow.Instance.SaveLayoutConfig();
                }
            }
        }
    }

    /// <summary>
    /// Централизованный планировщик живых плиток Windows 8.1.
    /// Переворачивает плитки с динамической паузой и легким наложением (до 2 плиток одновременно в разных частях экрана),
    /// создавая живой, аутентичный рабочий стол Metro UI.
    /// </summary>
    public static class LiveTileCoordinator
    {
        private static DispatcherTimer? _cycleTimer;
        private static readonly List<LiveTileControl> _registeredTiles = new();
        private static readonly List<LiveTileControl> _cycleQueue = new();
        private static readonly Random _rnd = new Random(Guid.NewGuid().GetHashCode());
        private static int _animatingCount = 0;

        public static void Register(LiveTileControl tile)
        {
            if (!_registeredTiles.Contains(tile))
            {
                _registeredTiles.Add(tile);
            }
        }

        public static void Unregister(LiveTileControl tile)
        {
            _registeredTiles.Remove(tile);
            _cycleQueue.Remove(tile);
        }

        public static void Start()
        {
            if (_cycleTimer == null)
            {
                _cycleTimer = new DispatcherTimer();
                _cycleTimer.Tick += CycleTimer_Tick;
            }
            _cycleTimer.Stop();
            _animatingCount = 0;
            _cycleQueue.Clear();

            // Первая плитка переворачивается через 1.2–1.8 секунды после открытия экрана
            _cycleTimer.Interval = TimeSpan.FromSeconds(1.2 + _rnd.NextDouble() * 0.6);
            _cycleTimer.Start();
        }

        public static void Stop()
        {
            _cycleTimer?.Stop();
            _animatingCount = 0;
            _cycleQueue.Clear();
        }

        private static void CycleTimer_Tick(object? sender, EventArgs e)
        {
            _cycleTimer?.Stop();

            if (MainWindow.Instance == null || MainWindow.Instance.CurrentState == ScreenState.Closed || MainWindow.Instance.CurrentState == ScreenState.Closing)
            {
                return;
            }

            if (MainWindow.Instance.CurrentState == ScreenState.Opening)
            {
                _cycleTimer!.Interval = TimeSpan.FromMilliseconds(400);
                _cycleTimer.Start();
                return;
            }

            // Допускаем 1 переворачивающуюся плитку за раз для абсолютной плавности 60-144 FPS без микрофризов
            if (_animatingCount >= 1)
            {
                _cycleTimer!.Interval = TimeSpan.FromMilliseconds(400);
                _cycleTimer.Start();
                return;
            }

            // Если очередь пуста, собираем все активные живые плитки на экране и перемешиваем
            if (_cycleQueue.Count == 0)
            {
                var eligible = _registeredTiles.Where(t => 
                    (t.IsLoaded || t.ActualWidth > 0) && 
                    t.Visibility == Visibility.Visible && 
                    !t.IsDragging &&
                    t.DataContext is TileModel m && 
                    m.IsLiveTileEnabled && 
                    (m.IsPhotoBannerTemplate || m.IsFinanceTemplate || m.IsWeatherTemplate || m.IsStoreTemplate || m.IsGamesTemplate || !string.IsNullOrEmpty(m.LiveText))
                ).ToList();

                if (eligible.Count == 0)
                {
                    _cycleTimer!.Interval = TimeSpan.FromSeconds(2.0);
                    _cycleTimer.Start();
                    return;
                }

                // Тасование Фишера-Йейтса
                for (int i = eligible.Count - 1; i > 0; i--)
                {
                    int k = _rnd.Next(i + 1);
                    var temp = eligible[i];
                    eligible[i] = eligible[k];
                    eligible[k] = temp;
                }

                _cycleQueue.AddRange(eligible);
            }

            var selectedTile = _cycleQueue[0];
            _cycleQueue.RemoveAt(0);

            _animatingCount++;
            selectedTile.ExecuteLiveRoll(() =>
            {
                if (_animatingCount > 0) _animatingCount--;
            });

            // Динамичный интервал до запуска следующей плитки: 2.0 – 3.2 секунды
            if (_cycleTimer != null)
            {
                _cycleTimer.Interval = TimeSpan.FromSeconds(2.0 + _rnd.NextDouble() * 1.2);
                _cycleTimer.Start();
            }
        }
    }
}

