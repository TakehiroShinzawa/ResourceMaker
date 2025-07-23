
using System.Windows;

namespace ResourceMaker
{
    /// <summary>
    /// LanguageSelectionWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class LanguageSelectionWindow : Window
    {
        public LanguageSelectionWindow()
        {
            InitializeComponent();
        }

        private void ConfirmLanguage_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
