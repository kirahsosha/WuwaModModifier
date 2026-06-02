using System.Collections.Generic;

namespace WuwaModModifier.Common
{
    public interface IModConfigDiscoveryService
    {
        IReadOnlyList<string> GetConfigCandidates(string modDirectory);
        string? GetPrimaryConfigPath(string modDirectory);
    }
}