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
            langWindow.EditorType = "xaml";
            langWindow.DevelopType = "vsix";
            langWindow.ProjectName = "ResourceMaker";

            langWindow.LineText = "<Button Content=\"言語選択ウィンドウを開く\" Click=\"OpenLanguageWindow_Click\" Margin=\"0,0,0,10\"/>";
            langWindow.BaseFolderPath = @"C:\temp\ResourceMaker";
            var result = langWindow.ShowDialog();
            if (result == true)
            {

            }
        }


        private void OpenResourceWindow_Click(object sender, RoutedEventArgs e)
        {
            var resWindow = new ResourceEditWindow();
            resWindow.EditorType = "code";
            resWindow.DevelopType = "vsix";
            resWindow.ProjectName = "ResourceMaker";
            resWindow.LineText = "string fileName = \"Resources.resw\";";
            resWindow.BaseFolderPath = @"C:\temp\ResourceMaker";
            resWindow.ShowDialog();
        }



    }

}