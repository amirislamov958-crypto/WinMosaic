using System.Collections.ObjectModel;

namespace Win8StartScreen.Models
{
    /// <summary>
    /// Кластер из НЕ БОЛЕЕ ЧЕМ 4 мелких (Small, 71x71) плиток, которые в
    /// подлинном Windows 8.1 занимают ровно ту же "клетку" сетки, что и
    /// одна Medium-плитка (150x150), располагаясь в решётке 2x2.
    ///
    /// ИСПРАВЛЕНИЕ БАГА С НАЛОЖЕНИЕМ:
    /// Раньше мелкие плитки, судя по всему, позиционировались вручную
    /// (Canvas.Top/Left, либо через Grid без явных Row/Column, либо
    /// фиксированная высота строки, которая не совпадала с реальной высотой
    /// контента) — отсюда наложение и обрезка.
    ///
    /// Теперь кластер — это ОТДЕЛЬНАЯ модель, которая рендерится через
    /// <see cref="System.Windows.Controls.UniformGrid"/> Rows="2" Columns="2"
    /// (см. Styles/MetroStyles.xaml). UniformGrid гарантированно и без
    /// исключений раскладывает 1-4 элемента в сетку 2x2 без пересечений,
    /// вне зависимости от контента.
    /// </summary>
    public class SmallTileCluster
    {
        public ObservableCollection<TileModel> SmallTiles { get; set; } = new();
    }
}
