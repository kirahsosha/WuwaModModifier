using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WuwaModModifier.Model;

namespace WuwaModModifier
{
    public partial class ManualPairingWindow : Window
    {
        public IReadOnlyList<VersionSyncFolderCandidate> Candidates { get; }

        public VersionSyncFolderCandidate? SelectedOldCandidate => cmbOldCandidate.SelectedItem as VersionSyncFolderCandidate;

        public VersionSyncFolderCandidate? SelectedNewCandidate => cmbNewCandidate.SelectedItem as VersionSyncFolderCandidate;

        public ManualPairingWindow(IReadOnlyList<VersionSyncFolderCandidate> candidates)
        {
            InitializeComponent();

            Candidates = candidates
                .OrderBy(candidate => candidate.FolderName)
                .ToList();

            DataContext = this;

            if (Candidates.Count > 0)
            {
                cmbOldCandidate.SelectedIndex = 0;
            }

            if (Candidates.Count > 1)
            {
                cmbNewCandidate.SelectedIndex = 1;
            }
            else if (Candidates.Count > 0)
            {
                cmbNewCandidate.SelectedIndex = 0;
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedOldCandidate == null || SelectedNewCandidate == null)
            {
                MessageBox.Show("请分别选择旧版和新版目录。", "手动配对", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedOldCandidate.FullPath.Equals(SelectedNewCandidate.FullPath))
            {
                MessageBox.Show("旧版和新版不能选择同一个目录。", "手动配对", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }
    }
}