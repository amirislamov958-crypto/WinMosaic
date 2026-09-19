using System.Collections.ObjectModel;

namespace Win8StartScreen.Models
{
    /// <summary>
    /// Именованная группа плиток (аналог групп на реальном Start Screen —
    /// "Games", "Life at a glance" и т.д.).
    ///
    /// Items может содержать вперемешку TileModel (обычная плитка) и
    /// SmallTileCluster (кластер 2x2 мелких плиток) — WPF сам подберёт
    /// нужный DataTemplate по типу объекта (см. неявные DataTemplate
    /// в MetroStyles.xaml), поэтому никакого ручного if/switch на
    /// приведение типов не требуется.
    /// </summary>
    public class TileGroup
    {
        public string Header { get; set; } = string.Empty;

        public ObservableCollection<object> Items { get; set; } = new();
    }
}
