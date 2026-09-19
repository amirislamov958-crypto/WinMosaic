using System.Collections.ObjectModel;
using System.Windows.Media;
using Win8StartScreen.Models;

namespace Win8StartScreen.ViewModels
{
    /// <summary>
    /// Демонстрационные данные. Обратите внимание: у каждой плитки Glyph
    /// назначается ЯВНО и ИНДИВИДУАЛЬНО в момент создания объекта — никакой
    /// иконки "по индексу в массиве" или "из общего словаря по умолчанию".
    /// Именно это устраняет баг вида "Mail показывает иконку Store".
    ///
    /// Глифы взяты из шрифта Segoe UI Symbol (использовался в Windows 8/8.1
    /// для системных плиток). Если в вашем реальном проекте иконки — это
    /// PNG/SVG-ассеты, замените свойство Glyph на ImageSource и в шаблоне
    /// (MetroStyles.xaml) TextBlock на Image — сам принцип "1 плитка = 1 явно
    /// назначенный ассет" остаётся тем же.
    /// </summary>
    public class StartScreenViewModel
    {
        public ObservableCollection<TileGroup> Groups { get; } = new();

        public StartScreenViewModel()
        {
            BuildSampleData();
        }

        private void BuildSampleData()
        {
            // ---------- Группа 1: "Games" (демонстрирует фикс наложения мелких плиток) ----------
            var gamesGroup = new TileGroup { Header = "Games" };

            gamesGroup.Items.Add(new TileModel
            {
                Id = "xbox",
                Title = "Xbox",
                Size = TileSize.Wide,
                Background = new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10)),
                Glyph = "\uE1CF", // трофей — корректно принадлежит именно Xbox/Games
                GlyphSize = 44
            });

            // Кластер 2x2 мелких системных плиток — раньше здесь были коллизии
            gamesGroup.Items.Add(new SmallTileCluster
            {
                SmallTiles = new ObservableCollection<TileModel>
                {
                    new TileModel { Id="help",     Title="Help",     Size=TileSize.Small, Glyph="\uE11B", Background=new SolidColorBrush(Color.FromRgb(0x64,0x76,0x87)) },
                    new TileModel { Id="settings", Title="PC Settings", Size=TileSize.Small, Glyph="\uE115", Background=new SolidColorBrush(Color.FromRgb(0x64,0x76,0x87)) },
                    new TileModel { Id="camera",   Title="Camera",   Size=TileSize.Small, Glyph="\uE114", Background=new SolidColorBrush(Color.FromRgb(0x82,0x5A,0x2C)) },
                    new TileModel { Id="alarms",   Title="Alarms",   Size=TileSize.Small, Glyph="\uE121", Background=new SolidColorBrush(Color.FromRgb(0x82,0x5A,0x2C)) },
                }
            });

            gamesGroup.Items.Add(new TileModel
            {
                Id = "store",
                Title = "Store",
                Size = TileSize.Medium,
                Background = new SolidColorBrush(Color.FromRgb(0x00, 0x50, 0xEF)),
                Glyph = "\uE14D", // сумка/пакет — корректно принадлежит Store
                GlyphSize = 40
            });

            Groups.Add(gamesGroup);

            // ---------- Группа 2: "Life at a glance" (демонстрирует фикс Mail / IE) ----------
            var lifeGroup = new TileGroup { Header = "Life at a glance" };

            lifeGroup.Items.Add(new TileModel
            {
                Id = "mail",
                Title = "Mail",
                Size = TileSize.Wide,
                Background = new SolidColorBrush(Color.FromRgb(0x1B, 0xA1, 0xE2)),
                Glyph = "\uE119", // конверт — корректно принадлежит Mail
                GlyphSize = 40
            });

            lifeGroup.Items.Add(new TileModel
            {
                Id = "ie",
                Title = "Internet Explorer",
                Size = TileSize.Medium,
                Background = new SolidColorBrush(Color.FromRgb(0x1B, 0xA1, 0xE2)),
                Glyph = "\uE128", // глобус — корректно принадлежит IE, а не трофей
                GlyphSize = 40
            });

            lifeGroup.Items.Add(new TileModel
            {
                Id = "calendar",
                Title = "Calendar",
                Size = TileSize.Medium,
                Background = new SolidColorBrush(Color.FromRgb(0xFA, 0x68, 0x00)),
                Glyph = "\uE163",
                GlyphSize = 40
            });

            lifeGroup.Items.Add(new TileModel
            {
                Id = "people",
                Title = "People",
                Size = TileSize.Medium,
                Background = new SolidColorBrush(Color.FromRgb(0xAA, 0x00, 0xFF)),
                Glyph = "\uE125",
                GlyphSize = 40
            });

            lifeGroup.Items.Add(new TileModel
            {
                Id = "photos",
                Title = "Photos",
                Size = TileSize.Large,
                Background = new SolidColorBrush(Color.FromRgb(0xD8, 0x00, 0x73)),
                Glyph = "\uE114",
                GlyphSize = 48
            });

            Groups.Add(lifeGroup);

            // ---------- Группа 3: "Productivity" ----------
            var workGroup = new TileGroup { Header = "Productivity" };

            workGroup.Items.Add(new TileModel { Id="maps",  Title="Maps",  Size=TileSize.Medium, Glyph="\uE1C4", Background=new SolidColorBrush(Color.FromRgb(0x60,0xA9,0x17)) });
            workGroup.Items.Add(new TileModel { Id="music", Title="Music", Size=TileSize.Medium, Glyph="\uE142", Background=new SolidColorBrush(Color.FromRgb(0xE5,0x14,0x00)) });
            workGroup.Items.Add(new TileModel { Id="video", Title="Video", Size=TileSize.Medium, Glyph="\uE116", Background=new SolidColorBrush(Color.FromRgb(0x82,0x5A,0x2C)) });
            workGroup.Items.Add(new TileModel { Id="weather", Title="Weather", Size=TileSize.Medium, Glyph="\uE1BB", Background=new SolidColorBrush(Color.FromRgb(0x1B,0xA1,0xE2)) });

            Groups.Add(workGroup);
        }
    }
}
