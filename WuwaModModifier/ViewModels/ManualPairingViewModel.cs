using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WuwaModModifier.Model;

namespace WuwaModModifier.ViewModels
{
    public class ManualPairingViewModel : ViewModelBase
    {
        private VersionSyncFolderCandidate? _selectedOldCandidate;
        private VersionSyncFolderCandidate? _selectedNewCandidate;

        public IReadOnlyList<VersionSyncFolderCandidate> Candidates { get; }

        public VersionSyncFolderCandidate? SelectedOldCandidate
        {
            get => _selectedOldCandidate;
            set => SetProperty(ref _selectedOldCandidate, value);
        }

        public VersionSyncFolderCandidate? SelectedNewCandidate
        {
            get => _selectedNewCandidate;
            set => SetProperty(ref _selectedNewCandidate, value);
        }

        public ICommand ConfirmCommand { get; }

        public ManualPairingViewModel(IReadOnlyList<VersionSyncFolderCandidate> candidates)
        {
            Candidates = candidates
                .OrderBy(candidate => candidate.FolderName)
                .ToList();

            ConfirmCommand = new RelayCommand(ExecuteConfirm, CanConfirm);

            if (Candidates.Count > 0)
            {
                SelectedOldCandidate = Candidates[0];
            }

            if (Candidates.Count > 1)
            {
                SelectedNewCandidate = Candidates[1];
            }
            else if (Candidates.Count > 0)
            {
                SelectedNewCandidate = Candidates[0];
            }
        }

        private bool CanConfirm()
        {
            return SelectedOldCandidate != null &&
                   SelectedNewCandidate != null &&
                   !SelectedOldCandidate.FullPath.Equals(SelectedNewCandidate.FullPath);
        }

        private void ExecuteConfirm()
        {
            if (SelectedOldCandidate == null || SelectedNewCandidate == null)
            {
                MessageBox.Show(Properties.Resources.DialogManualPairingSelectBoth, Properties.Resources.DialogCaptionManualPairing, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedOldCandidate.FullPath.Equals(SelectedNewCandidate.FullPath))
            {
                MessageBox.Show(Properties.Resources.DialogManualPairingSameDir, Properties.Resources.DialogCaptionManualPairing, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Dialog result is set by the Window's code-behind
        }
    }
}
