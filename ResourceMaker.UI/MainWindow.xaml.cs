using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ResourceMaker.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        private void OpenLanguageWindow_Click(object sender, RoutedEventArgs e)
        {
            var langWindow = new LanguageSelectionWindow();
            langWindow.BaseFolderPath = @"C:\temp\conB2WebApiSimulator\conB2WebApiSimulator\conB2WebApiSimulator";
            langWindow.EditorType = "code";
            langWindow.LineText = "await CreateLanguageCodeFoldersAsync(Path.Combine(BaseFolderPath, \"Strings\"));";
            var result = langWindow.ShowDialog();
            if (result == true)
            {

            }
        }
        private void OpenResourceWindow_Click(object sender, RoutedEventArgs e)
        {
            var resWindow =  new ResourceEditWindow();
            resWindow.LineText = "await CreateLanguageCodeFoldersAsync(Path.Combine(BaseFolderPath, \"Strings\"));";
            resWindow.BaseFolderPath = @"C:\temp\conB2WebApiSimulator\conB2WebApiSimulator\conB2WebApiSimulator";
            resWindow.ShowDialog();
        }
    }

}