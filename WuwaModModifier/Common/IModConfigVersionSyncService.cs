using System.Collections.Generic;
using WuwaModModifier.Model;

namespace WuwaModModifier.Common
{
    public interface IModConfigVersionSyncService
    {
        IReadOnlyList<VersionSyncFolderCandidate> DiscoverModCandidates(string modRootPath);
        IReadOnlyList<VersionSyncPairingJob> CreatePairingJobs(
            IReadOnlyList<VersionSyncFolderCandidate> oldCandidates,
            IReadOnlyList<VersionSyncFolderCandidate> newCandidates);
        VersionSyncComparisonResult BuildComparison(VersionSyncPairingJob job);
        VersionSyncComparisonResult BuildComparison(VersionSyncPairingJob job, ModConfigEditBuffer newBuffer);
        VersionSyncApplyResult ApplyComparison(VersionSyncComparisonResult comparison);
    }
}