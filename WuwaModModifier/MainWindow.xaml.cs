using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WuwaModModifier.Data.ViewModels;
using WuwaModModifier.ViewModels;

namespace WuwaModModifier
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            DataContext = ViewModel;
        }

        /// <summary>
        /// 右键时选中当前 TreeViewItem，并更新 ViewModel.SelectedDirectoryItem
        /// </summary>
        private void tvModDirectory_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject);
            if (treeViewItem != null)
            {
                treeViewItem.Focus();
                treeViewItem.IsSelected = true;

                if (treeViewItem.DataContext is DirectoryItemViewModel item)
                {
                    ViewModel.SelectedDirectoryItem = item;
                }
            }
        }

        private static T? VisualUpwardSearch<T>(DependencyObject? source) where T : DependencyObject
        {
            while (source != null && source is not T)
            {
                source = VisualTreeHelper.GetParent(source);
            }
            return source as T;
        }

        private void btnRandomLoadAllMods_Click()
        {

        }
    }
}
