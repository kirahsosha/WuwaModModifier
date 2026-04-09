using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WuwaModModifier.Data.ViewModels
{
    public class DirectoryItemViewModel : INotifyPropertyChanged
    {
        private bool _isChecked;
        private bool _isSelected;
        private string _name = string.Empty;
        private string _id = string.Empty;
        private string _fullPath = string.Empty;
        private bool _isDirectory;
        private ObservableCollection<DirectoryItemViewModel> _children;

        public DirectoryItemViewModel()
        {
            _children = new ObservableCollection<DirectoryItemViewModel>();
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged();
                }
            }
        }

        public string FullPath
        {
            get => _fullPath;
            set
            {
                if (_fullPath != value)
                {
                    _fullPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsDirectory
        {
            get => _isDirectory;
            set
            {
                if (_isDirectory != value)
                {
                    _isDirectory = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged();

                    // 如果选中父节点，则选中所有子节点
                    if (value && _children != null)
                    {
                        foreach (var child in _children)
                        {
                            child.IsChecked = true;
                        }
                    }
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<DirectoryItemViewModel> Children
        {
            get => _children;
            set
            {
                if (_children != value)
                {
                    _children = value;
                    OnPropertyChanged();
                }
            }
        }

        public DirectoryItemViewModel? Parent { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
