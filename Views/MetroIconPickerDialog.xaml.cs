using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Win8StartScreen.Views
{
    public class CategoryItem
    {
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = "📁";
        public int Count { get; set; } = 0;
        public string DisplayText => $"{Icon} {Title} ({Count})";

        public override string ToString() => DisplayText;
    }

    public partial class MetroIconPickerDialog : Window
    {
        public string SelectedIconPath { get; private set; } = string.Empty;
        public double ChosenIconSize { get; private set; } = 64.0;
        private List<MetroIconInfo> _allIcons = new();
        private Border? _selectedCard = null;

        public MetroIconPickerDialog()
        {
            InitializeComponent();
            LoadIcons();
        }

        private void LoadIcons()
        {
            MetroIconResolver.Initialize();
            _allIcons = MetroIconResolver.GetAllIcons();

            var categoryGroups = _allIcons.GroupBy(i => i.Category)
                .OrderBy(g => g.Key)
                .ToList();

            CategoriesListBox.Items.Clear();
            CategoriesListBox.Items.Add(new CategoryItem
            {
                Title = "Все значки",
                Icon = "🌟",
                Count = _allIcons.Count
            });

            foreach (var g in categoryGroups)
            {
                string icon = g.FirstOrDefault()?.CategoryIcon ?? "📁";
                CategoriesListBox.Items.Add(new CategoryItem
                {
                    Title = g.Key,
                    Icon = icon,
                    Count = g.Count()
                });
            }

            CategoriesListBox.SelectedIndex = 0;
            FilterIcons();
        }

        private void FilterIcons()
        {
            if (IconsWrapPanel == null) return;

            string query = SearchBox.Text?.Trim() ?? string.Empty;
            string selectedCat = "Все значки";

            if (CategoriesListBox.SelectedItem is CategoryItem catItem)
            {
                selectedCat = catItem.Title;
            }

            var filtered = _allIcons.Where(icon =>
            {
                bool matchQuery = string.IsNullOrEmpty(query) || icon.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
                bool matchCat = selectedCat == "Все значки" || icon.Category == selectedCat;
                return matchQuery && matchCat;
            }).Take(300).ToList();

            ActiveCategoryHeader.Text = selectedCat;
            CategoryCountText.Text = $"Найдено: {filtered.Count} из {_allIcons.Count}";

            IconsWrapPanel.Children.Clear();
            _selectedCard = null;

            foreach (var icon in filtered)
            {
                var card = new Border
                {
                    Width = 86,
                    Height = 86,
                    Margin = new Thickness(4),
                    Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 60)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Cursor = Cursors.Hand,
                    Tag = icon.FilePath,
                    ToolTip = icon.Name
                };

                var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };

                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(icon.FilePath, UriKind.Absolute);
                    bmp.DecodePixelWidth = 48;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();

                    var img = new Image
                    {
                        Source = bmp,
                        Width = 42,
                        Height = 42,
                        Margin = new Thickness(0, 0, 0, 4),
                        Stretch = Stretch.Uniform
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    sp.Children.Add(img);
                }
                catch { }

                var text = new TextBlock
                {
                    Text = icon.Name,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(210, 210, 210)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextAlignment = TextAlignment.Center,
                    MaxWidth = 78
                };
                sp.Children.Add(text);

                card.Child = sp;
                card.MouseDown += (s, e) =>
                {
                    if (_selectedCard != null)
                    {
                        _selectedCard.BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 60));
                        _selectedCard.Background = new SolidColorBrush(Color.FromRgb(35, 35, 38));
                    }
                    _selectedCard = card;
                    _selectedCard.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204));
                    _selectedCard.Background = new SolidColorBrush(Color.FromRgb(25, 65, 100));
                    SelectedIconPath = card.Tag?.ToString() ?? string.Empty;
                    StatusText.Text = $"Выбран значок: {icon.Name} ({icon.Category})";

                    if (e.ClickCount == 2)
                    {
                        DialogResult = true;
                        Close();
                    }
                };

                IconsWrapPanel.Children.Add(card);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => FilterIcons();
        private void CategoriesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => FilterIcons();

        private void PresetSize_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagStr && double.TryParse(tagStr, out double size))
            {
                ChosenIconSize = size;
                StatusText.Text = $"Размер значка будет установлен: {size:F0}px";
            }
        }

        private void BrowseCustomFile_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Изображения (*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.svg;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.svg;*.webp|PNG файлы (*.png)|*.png|Все файлы (*.*)|*.*",
                Title = "Выберите логотип для плитки"
            };

            if (ofd.ShowDialog() == true && File.Exists(ofd.FileName))
            {
                SelectedIconPath = ofd.FileName;
                DialogResult = true;
                Close();
            }
        }

        private void PasteFromClipboard_Click(object sender, RoutedEventArgs e)
        {
            HandleClipboardPaste();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                HandleClipboardPaste();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void HandleClipboardPaste()
        {
            try
            {
                if (Clipboard.ContainsImage())
                {
                    var imageSource = Clipboard.GetImage();
                    if (imageSource != null)
                    {
                        string savedFile = SaveBitmapSourceToCustomIcons(imageSource);
                        SelectedIconPath = savedFile;
                        DialogResult = true;
                        Close();
                        return;
                    }
                }
                else if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList();
                    foreach (string? file in files)
                    {
                        if (!string.IsNullOrEmpty(file) && IsImageFile(file))
                        {
                            SelectedIconPath = file;
                            DialogResult = true;
                            Close();
                            return;
                        }
                    }
                }

                MessageBox.Show("В буфере обмена нет изображения. Скопируйте картинку или скриншот и попробуйте снова.", "Буфер обмена", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка вставки из буфера: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var f in files)
                {
                    if (IsImageFile(f))
                    {
                        SelectedIconPath = f;
                        DialogResult = true;
                        Close();
                        return;
                    }
                }
            }
        }

        public static bool IsImageFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".ico" or ".svg" or ".webp" or ".gif";
        }

        public static string SaveBitmapSourceToCustomIcons(BitmapSource bmpSource)
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win8StartScreen", "CustomIcons");
            Directory.CreateDirectory(dir);
            string filename = $"custom_logo_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}.png";
            string fullPath = Path.Combine(dir, filename);

            using var fileStream = new FileStream(fullPath, FileMode.Create);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmpSource));
            encoder.Save(fileStream);

            return fullPath;
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(SelectedIconPath))
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите значок из списка или нажмите «Загрузить свой PNG».", "Выбор логотипа", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
