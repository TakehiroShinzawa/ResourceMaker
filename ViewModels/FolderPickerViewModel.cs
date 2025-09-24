using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace ResourceMaker.UI
{
    public class FolderPickerViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<FolderItem> RootFolders { get; } = new ObservableCollection<FolderItem>();

        private string _currentFolderPath;
        public string CurrentFolderPath
        {
            get => _currentFolderPath;
            set
            {
                if (_currentFolderPath != value)
                {
                    _currentFolderPath = value;
                    OnPropertyChanged(nameof(CurrentFolderPath));
                }
            }
        }

        public FolderPickerViewModel(string initialPath)
        {
            var root = new FolderItem(initialPath);
            RootFolders.Add(root);
            root.IsExpanded = true;
            root.LoadChildren();
            CurrentFolderPath = root.Path;
        }

        public void SetRootFolder(string path)
        {
            if (Directory.Exists(path))
            {
                var root = new FolderItem(path);
                root.IsExpanded = true;
                root.LoadChildren();

                RootFolders.Clear();
                RootFolders.Add(root);
                CurrentFolderPath = path;
            }
        }

        public void MoveUp()
        {
            if (RootFolders.Count == 1)
            {
                var current = RootFolders[0];
                var parentPath = System.IO.Path.GetDirectoryName(current.Path);

                if (!string.IsNullOrEmpty(parentPath) && Directory.Exists(parentPath))
                {
                    var parent = new FolderItem(parentPath);
                    parent.IsExpanded = true;
                    RootFolders.Clear();
                    RootFolders.Add(parent);
                    CurrentFolderPath = parent.Path;
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class FolderItem : INotifyPropertyChanged
    {
        public string Path { get; }
        public string Name => System.IO.Path.GetFileName(Path);
        public override string ToString() => Name;

        public ObservableCollection<FolderItem> Children { get; } = new ObservableCollection<FolderItem>();

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        public FolderItem(string path)
        {
            Path = path;
            //LoadChildren(); // 初期読み込み
        }

        public void LoadChildren()
        {
            Children.Clear();
            try
            {
                foreach (var dir in Directory.GetDirectories(Path))
                {
                    var child = new FolderItem(dir);
                    Children.Add(child);
                }
            }
            catch { /* アクセス拒否などは無視 */ }
        }
    }
}
