using System.Windows;
using System.Windows.Controls;
using Win8StartScreen.ViewModels;

namespace Win8StartScreen.Views
{
    public partial class StartScreenView : UserControl
    {
        public StartScreenView()
        {
            InitializeComponent();
            DataContext = new StartScreenViewModel();
        }

        /// <summary>
        /// Обработчик клика по любой плитке (кроме мелких в кластере — при
        /// необходимости подключите Click так же в шаблоне SmallTileCluster).
        /// Tag плитки хранит Id — используйте его для запуска нужного действия
        /// / навигации, не полагаясь на порядковый индекс в списке.
        /// </summary>
        private void Tile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tileId)
            {
                // TODO: подключить реальную навигацию/запуск приложения по tileId
                MessageBox.Show($"Открыть: {tileId}");
            }
        }
    }
}
