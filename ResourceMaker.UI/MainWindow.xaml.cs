using System.Collections.Generic;
using System.Windows;


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
            resWindow.EditorType = "code";
            resWindow.LineText = "await CreateLanguageCodeFoldersAsync(Path.Combine(BaseFolderPath, \"保存\"));";
            resWindow.BaseFolderPath = @"C:\temp\conB2WebApiSimulator\conB2WebApiSimulator\conB2WebApiSimulator";
            resWindow.ShowDialog();
        }



    }

}