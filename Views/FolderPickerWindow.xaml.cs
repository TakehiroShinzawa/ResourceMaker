using System.IO;
using System.Resources;
using System.Windows;
using System.Windows.Input;

namespace ResourceMaker.UI
{
    /// <summary>
    /// Interaction logic for FolderPickerWindow.xaml
    /// </summary>
    public partial class FolderPickerWindow : Window
    {

        public string SelectedFolderPath { get; private set; }
        System.Resources.ResourceManager loader;

        public FolderPickerWindow()
        {
            InitializeComponent();
            LocalizeScreenXaml();
        }

        private void LocalizeScreenXaml()
        {
            loader = new ResourceManager("ResourceMaker.Resources.Strings", GetType().Assembly);
            LblSelectFolder.Title = loader.GetString("LblSelectFolder.Title");
            BtnMoveUpFolder.Content = loader.GetString("BtnMoveUpFolder.Content");
            BtnCancelFolderSelection.Content = loader.GetString("BtnCancelFolderSelection.Content");
            BtnConfirmFolderSelection.Content = loader.GetString("BtnConfirmFolderSelection.Content");

        }

        private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is FolderItem item)
            {
                if (item.Children.Count == 0)
                {
                    item.LoadChildren();
                }
                item.IsExpanded = true;
            }
        }
        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is FolderPickerViewModel vm)
            {
                vm.MoveUp();
            }
        }

        private void FolderPathText_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyFolderPath();
            }
        }

        private void FolderPathText_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyFolderPath();
        }

        private void ApplyFolderPath()
        {
            var vm = DataContext as FolderPickerViewModel;
            var inputPath = FolderPathText.Text;

            if (vm != null && !string.IsNullOrWhiteSpace(inputPath))
            {
                var validPath = FindExistingAncestor(inputPath);
                if (validPath != null)
                {
                    vm.SetRootFolder(validPath);
                }
            }
        }

        private string FindExistingAncestor(string path)
        {
            while (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
            {
                path = Path.GetDirectoryName(path);
            }
            return Directory.Exists(path) ? path : null;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            if (FolderTree.SelectedItem is FolderItem item)
                SelectedFolderPath = item.Path;

            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

    }
}
