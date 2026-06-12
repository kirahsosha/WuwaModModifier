using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WuwaModModifier.Model;
using WuwaModModifier.ViewModels;

namespace WuwaModModifier
{
    public partial class ManualPairingWindow : Window
    {
        public ManualPairingViewModel ViewModel { get; }

        public VersionSyncFolderCandidate? SelectedOldCandidate => ViewModel.SelectedOldCandidate;

        public VersionSyncFolderCandidate? SelectedNewCandidate => ViewModel.SelectedNewCandidate;

        public ManualPairingWindow(IReadOnlyList<VersionSyncFolderCandidate> candidates)
        {
            InitializeComponent();
            ViewModel = new ManualPairingViewModel(candidates);
            DataContext = ViewModel;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedOldCandidate == null || ViewModel.SelectedNewCandidate == null)
            {
                MessageBox.Show("请分别选择旧版和新版目录。", "手动配对", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ViewModel.SelectedOldCandidate.FullPath.Equals(ViewModel.SelectedNewCandidate.FullPath))
            {
                MessageBox.Show("旧版和新版不能选择同一个目录。", "手动配对", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }
    }
}