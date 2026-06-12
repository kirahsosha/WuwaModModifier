using System;

namespace WuwaModModifier.Common
{
    /// <summary>
    /// Default implementation of <see cref="ISystemVariableStrategy"/> that
    /// encodes the WWMI-specific system-variable naming conventions.
    /// Registered as a singleton in the DI container.
    /// </summary>
    public class DefaultSystemVariableStrategy : ISystemVariableStrategy
    {
        public bool IsInternalSystemVariable(string variableName)
        {
            return variableName.StartsWith("$required_", StringComparison.OrdinalIgnoreCase) ||
                   variableName.StartsWith("$object_", StringComparison.OrdinalIgnoreCase) ||
                   variableName.StartsWith("$mesh_", StringComparison.OrdinalIgnoreCase) ||
                   variableName.StartsWith("$shapekey_", StringComparison.OrdinalIgnoreCase) ||
                   variableName.StartsWith("$merge_status", StringComparison.OrdinalIgnoreCase) ||
                   variableName.IndexOf("\\WWMIv1\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   variableName.Equals("$mod_id", StringComparison.OrdinalIgnoreCase) ||
                   variableName.Equals("$state_id", StringComparison.OrdinalIgnoreCase) ||
                   variableName.Equals("$mod_enabled", StringComparison.OrdinalIgnoreCase);
        }
    }
}
